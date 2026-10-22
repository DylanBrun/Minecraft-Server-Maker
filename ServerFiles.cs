static class ServerFiles
{
    public static async Task DownloadMods(IEnumerable<JsonNode> files, string modsDir)
    {
        Directory.CreateDirectory(modsDir);
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        await Parallel.ForEachAsync(files, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (f, ct) =>
        {
            await using var src = await http.GetStreamAsync(CurseForge.DownloadUrl(f), ct);
            await using var dst = File.Create(Path.Combine(modsDir, Path.GetFileName(f["fileName"]!.GetValue<string>())));
            await src.CopyToAsync(dst, ct);
        });
    }

    // Forge : "Crash report saved to", vanilla : "This crash report has been saved to".
    public static bool IsCrash(string? line) =>
        line != null && (line.Contains("rash report") && line.Contains("saved to") || line.Contains("Failed to start the minecraft server"));

    public static ProcessStartInfo StartInfo(string script, string? javaBin)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c \"{Path.GetFileName(script)}\"")
        {
            WorkingDirectory = Path.GetDirectoryName(script),
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
        };
        psi.Environment.Remove("NoDefaultCurrentDirectoryInExePath"); // sinon cmd ne trouve ni start.bat ni les "call settings.bat"
        if (javaBin != null)
        {
            psi.Environment["PATH"] = javaBin + ";" + psi.Environment["PATH"];
            psi.Environment["JAVA_HOME"] = Path.GetDirectoryName(javaBin);
        }
        return psi;
    }

    public static async Task Install(string url, string dir)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(dir)!);
        var zip = dir + ".zip";
        try
        {
            using (var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan })
            using (var src = await http.GetStreamAsync(url))
            using (var dst = File.Create(zip))
                await src.CopyToAsync(dst);
            await Task.Run(() => ZipFile.ExtractToDirectory(zip, dir));
        }
        catch
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true); // installation partielle, créée par nous
            throw;
        }
        finally { File.Delete(zip); }
    }

    // Version de MC d'après le nom de dossier créé par l'app : "<slug>-<version>" ou "<slug>-<version>-<fileId>".
    public static string VersionOf(string dir) =>
        Regex.Match(Path.GetFileName(dir), @"-(\d+\.\d+(?:\.\d+)?)(?:-\d+)?$").Groups[1].Value;

    // Les server packs mettent souvent tout dans un unique dossier racine.
    public static string Unwrap(string dir)
    {
        while (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir) is [var only]) dir = only;
        return dir;
    }

    // shortcut: seuls les lanceurs .bat sont gérés ; un pack livré avec juste un jar/installeur demande un script fait main.
    public static string? FindStartScript(string dir)
    {
        if (!Directory.Exists(dir)) return null;
        var bats = Directory.GetFiles(dir, "*.bat");
        string[] preferred = ["startserver.bat", "start-server.bat", "start_server.bat", "start.bat", "run.bat", "serverstart.bat", "launchserver.bat"];
        return preferred.Select(p => bats.FirstOrDefault(b => Path.GetFileName(b).Equals(p, StringComparison.OrdinalIgnoreCase))).FirstOrDefault(b => b != null)
            ?? bats.FirstOrDefault(b => Regex.IsMatch(Path.GetFileName(b), "start|run|launch", RegexOptions.IgnoreCase));
    }
}
