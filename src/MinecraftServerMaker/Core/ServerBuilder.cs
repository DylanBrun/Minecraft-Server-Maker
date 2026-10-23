// Construit un serveur à partir de la version "joueur" d'un modpack (manifest.json + overrides), quand il n'y a pas de server pack.
static class ServerBuilder
{
    public static async Task Build(CurseForge cf, JsonNode packFile, string dir, Action<string> log)
    {
        log("Téléchargement de la version joueur…");
        await ServerFiles.Install(CurseForge.DownloadUrl(packFile), dir, ServerFiles.Sha1Of(packFile));
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, "manifest.json")))!;

        // overrides/ (config, kubejs, scripts…) va à la racine du serveur
        var overrides = Path.Combine(dir, "overrides");
        if (Directory.Exists(overrides))
        {
            foreach (var d in Directory.GetDirectories(overrides)) Directory.Move(d, Path.Combine(dir, Path.GetFileName(d)));
            foreach (var f in Directory.GetFiles(overrides)) File.Move(f, Path.Combine(dir, Path.GetFileName(f)), true);
            Directory.Delete(overrides, true);
        }

        var entries = manifest["files"]!.AsArray();
        var files = await cf.FilesById(entries.Select(e => e!["fileID"]!.GetValue<int>()));
        var classOf = (await cf.ModsById(entries.Select(e => e!["projectID"]!.GetValue<int>())))
            .ToDictionary(m => m!["id"]!.GetValue<int>(), m => m!["classId"]!.GetValue<int>());
        var keep = new List<JsonNode>();
        foreach (var f in files.Select(f => f!))
        {
            if (classOf.GetValueOrDefault(f["modId"]!.GetValue<int>()) != 6) continue; // packs de textures / shaders : inutiles côté serveur
            if (ClientOnly(f)) log($"  ignoré (mod côté joueur uniquement) : {f["fileName"]}");
            else keep.Add(f);
        }

        log($"Téléchargement de {keep.Count} mods…");
        await ServerFiles.DownloadMods(keep, Path.Combine(dir, "mods"));

        var mc = manifest["minecraft"]!;
        string mcVersion = mc["version"]!.GetValue<string>();
        var loaders = mc["modLoaders"]!.AsArray();
        string loader = (loaders.FirstOrDefault(l => l!["primary"]?.GetValue<bool>() == true) ?? loaders[0])!["id"]!.GetValue<string>();
        int ram = mc["recommendedRam"]?.GetValue<int>() is int r and > 0 ? r : 6144;
        log($"Installation de {loader} pour Minecraft {mcVersion}…");
        await InstallLoader(loader, mcVersion, dir, $"-Xmx{ram}M");
    }

    // Démarre le serveur à blanc ; à chaque plantage au chargement, met de côté les mods nommés
    // dans le crash report (souvent des mods côté joueur non étiquetés) et réessaie.
    public static async Task TrialBoot(string script, string javaBin, Action<string> log)
    {
        var dir = Path.GetDirectoryName(script)!;
        var mods = Path.Combine(dir, "mods");
        var disabled = Path.Combine(dir, "mods-desactives");
        for (int round = 1; round <= 10; round++)
        {
            log($"Essai de démarrage n°{round} (1 à 3 minutes)…");
            var started = DateTime.Now;
            using var p = Process.Start(ServerFiles.StartInfo(script, javaBin, nogui: true))!;
            var done = new TaskCompletionSource();
            var crashed = new TaskCompletionSource();
            DataReceivedEventHandler onLine = (_, e) =>
            {
                if (e.Data?.Contains("Done (") == true) done.TrySetResult();
                if (ServerFiles.IsCrash(e.Data))
                {
                    try { p.StandardInput.Close(); } catch (IOException) { } // débloque le "pause" final du .bat
                    crashed.TrySetResult();
                }
            };
            p.OutputDataReceived += onLine;
            p.ErrorDataReceived += onLine; // Forge écrit ses FATAL sur stderr
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            var exited = p.WaitForExitAsync();
            var first = await Task.WhenAny(done.Task, exited, crashed.Task, Task.Delay(TimeSpan.FromMinutes(10)));
            if (first == done.Task)
            {
                p.StandardInput.WriteLine("stop");
                p.StandardInput.Close();
                if (await Task.WhenAny(exited, Task.Delay(TimeSpan.FromMinutes(2))) != exited) p.Kill(true);
                log("Le serveur démarre correctement.");
                return;
            }
            if (first == crashed.Task)
            {
                // La JVM reste parfois bloquée après un crash au chargement (threads de mods) : le rapport est écrit, on la ferme.
                if (await Task.WhenAny(exited, Task.Delay(TimeSpan.FromSeconds(15))) != exited) p.Kill(true);
                await exited;
                await Task.Delay(2000); // laisse Windows libérer les .jar avant de les déplacer
            }
            else if (first != exited) { p.Kill(true); throw new Exception("Le serveur ne démarre pas (plus de 10 minutes)."); }

            var crashDir = new DirectoryInfo(Path.Combine(dir, "crash-reports"));
            var report = crashDir.Exists ? crashDir.GetFiles("*.txt").Where(f => f.LastWriteTime >= started).MaxBy(f => f.LastWriteTime) : null;
            var culprits = report == null ? [] : Regex.Matches(File.ReadAllText(report.FullName), @"Mod File: (.+\.jar)")
                .Select(m => Path.GetFileName(m.Groups[1].Value.Trim())).Distinct()
                .Where(f => File.Exists(Path.Combine(mods, f))).ToList();
            if (culprits.Count == 0)
                throw new Exception($"Le serveur plante au démarrage sans désigner de mod.\nVoir : {report?.FullName ?? Path.Combine(dir, "logs", "latest.log")}");
            Directory.CreateDirectory(disabled);
            foreach (var f in culprits)
            {
                File.Move(Path.Combine(mods, f), Path.Combine(disabled, f), true);
                log($"  mod mis de côté (fait planter le serveur) : {f}");
            }
        }
        throw new Exception("Le serveur plante toujours après 10 essais.");
    }

    // Limite : se fie aux étiquettes Client/Server de CurseForge ; les mods côté joueur non étiquetés sont rattrapés par TrialBoot.
    static bool ClientOnly(JsonNode f)
    {
        var tags = f["gameVersions"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
        return tags.Contains("Client") && !tags.Contains("Server");
    }

    public static bool LoaderPartsValid(string ver, string mc) =>
        Regex.IsMatch(ver, @"^[0-9A-Za-z.+_-]{1,40}$") && !ver.Contains("..") && Regex.IsMatch(mc, @"^\d+(\.\d+){1,2}$");

    static async Task InstallLoader(string loader, string mc, string dir, string xmx)
    {
        // Valeurs lues dans le manifest du modpack : on n'accepte qu'un format de version avant de les mettre dans une URL.
        int dash = loader.IndexOf('-');
        if (dash < 1 || !LoaderPartsValid(loader[(dash + 1)..], mc)) throw new Exception($"Version de loader invalide dans le modpack : « {loader} » (Minecraft « {mc} »).");
        string kind = loader[..dash], ver = loader[(dash + 1)..];
        var javaBin = await Java.Ensure(Java.MajorFor(mc));
        using var http = ServerFiles.Downloader();

        if (kind == "fabric")
        {
            var installers = JsonNode.Parse(await http.GetStringAsync("https://meta.fabricmc.net/v2/versions/installer"))!.AsArray();
            var installer = installers.First(i => i!["stable"]!.GetValue<bool>())!["version"]!.GetValue<string>();
            await File.WriteAllBytesAsync(Path.Combine(dir, "fabric-server-launch.jar"),
                await http.GetByteArrayAsync($"https://meta.fabricmc.net/v2/versions/loader/{mc}/{ver}/{installer}/server/jar"));
            File.WriteAllText(Path.Combine(dir, "start.bat"), $"java {xmx} -jar fabric-server-launch.jar nogui\r\n");
            return;
        }

        // Limite : Forge très ancien (≤ 1.7.10, autre nommage) et Quilt non gérés.
        string url = kind switch
        {
            "forge" => $"https://maven.minecraftforge.net/net/minecraftforge/forge/{mc}-{ver}/forge-{mc}-{ver}-installer.jar",
            "neoforge" when mc == "1.20.1" => $"https://maven.neoforged.net/releases/net/neoforged/forge/1.20.1-{ver}/forge-1.20.1-{ver}-installer.jar",
            "neoforge" => $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{ver}/neoforge-{ver}-installer.jar",
            _ => throw new Exception($"Loader « {loader} » non géré pour la construction sans server pack."),
        };
        var installerJar = Path.Combine(dir, "loader-installer.jar");
        await File.WriteAllBytesAsync(installerJar, await http.GetByteArrayAsync(url));

        var psi = new ProcessStartInfo(Path.Combine(javaBin, "java.exe"))
        {
            ArgumentList = { "-jar", installerJar, "--installServer" },
            WorkingDirectory = dir, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEndAsync();
        var errors = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        if (p.ExitCode != 0) throw new Exception($"L'installation de {loader} a échoué :\n{string.Join("\n", ((await output) + (await errors)).Split('\n').TakeLast(8))}");
        File.Delete(installerJar);

        var jvmArgs = Path.Combine(dir, "user_jvm_args.txt");
        if (File.Exists(Path.Combine(dir, "run.bat"))) File.AppendAllText(jvmArgs, $"\n{xmx}\n"); // Forge/NeoForge 1.17+ : run.bat lit ce fichier
        else
        {
            var jar = Directory.GetFiles(dir, "forge-*.jar").FirstOrDefault(f => !f.Contains("installer"))
                ?? throw new Exception("Jar Forge introuvable après installation.");
            File.WriteAllText(Path.Combine(dir, "start.bat"), $"java {xmx} -jar \"{Path.GetFileName(jar)}\" nogui\r\n");
        }
    }
}
