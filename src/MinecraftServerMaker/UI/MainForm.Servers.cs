record ServerItem(string Dir)
{
    public override string ToString() => Path.GetFileName(Dir);
}

// Recherche de modpacks, création, liste et suppression des serveurs.
partial class MainForm
{
    // Remplit la liste avec les dossiers de servers\ et resélectionne le serveur courant (sinon le premier).
    void RefreshServers()
    {
        var dir = Path.Combine(Settings.DataDir, "servers");
        servers.Items.Clear();
        if (Directory.Exists(dir)) servers.Items.AddRange(Directory.GetDirectories(dir).Select(d => new ServerItem(d)).ToArray());
        var current = servers.Items.Cast<ServerItem>().FirstOrDefault(s => ServerFiles.Unwrap(s.Dir) == settings.ServerDir);
        if (current != null) servers.SelectedItem = current;
        else if (servers.Items.Count > 0) servers.SelectedIndex = 0;
        else { settings.ServerDir = settings.McVersion = ""; settings.Save(); }
    }

    Task OpenMods()
    {
        int loader = ServerFiles.LoaderOf(settings.ServerDir);
        if (settings.McVersion == "") throw new Exception("Version de Minecraft de ce serveur inconnue.");
        if (loader == 0) throw new Exception("Loader de ce serveur non reconnu (Forge, NeoForge, Fabric ou Quilt attendu).");
        using var form = new ModsForm(Api(), settings.ServerDir, settings.McVersion, loader);
        form.ShowDialog(this);
        return Task.CompletedTask;
    }

    async Task DeleteServer()
    {
        if (servers.SelectedItem is not ServerItem s) return;
        if (IsRunning) throw new Exception("Arrêtez d'abord le serveur.");
        if (MessageBox.Show(this, $"Supprimer définitivement le serveur « {s} » ?\n\nSon monde, ses mods et ses réglages seront effacés.",
                "Supprimer le serveur", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
        await Task.Run(() => Directory.Delete(s.Dir, true));
        Log($"Serveur supprimé : {s.Dir}");
        RefreshServers();
    }

    CurseForge Api()
    {
        if (string.IsNullOrWhiteSpace(apiKey.Text)) throw new Exception("Entrez votre clé API CurseForge (https://console.curseforge.com).");
        settings.ApiKey = apiKey.Text.Trim();
        settings.Save();
        return new CurseForge(settings.ApiKey);
    }

    async Task SearchPacks()
    {
        var results = await Api().Find(search.Text.Trim());
        packs.Items.Clear();
        versions.Items.Clear();
        packs.Items.AddRange(results.ToArray());
        if (results.Count > 0) packs.SelectedIndex = 0;
        Log($"{results.Count} modpack(s) trouvé(s).");
    }

    async Task CreateServer()
    {
        if (packs.SelectedItem is not Pack pack || versions.SelectedItem is not string version) throw new Exception("Choisissez un modpack et une version.");
        if (IsRunning) throw new Exception("Arrêtez d'abord le serveur en cours.");

        var api = Api();
        var files = await api.FilesFor(pack.Id, version);
        if (files.Count == 0) throw new Exception($"Aucune version de ce modpack pour Minecraft {version}.");
        bool build = buildBox.Checked;
        var serverFile = build ? null : files.FirstOrDefault(CurseForge.HasServerPack)
            ?? throw new Exception($"Ce modpack n'a pas de server pack pour Minecraft {version}.\nCochez « Sans server pack » pour construire le serveur depuis la version joueur.");

        var dir = Path.Combine(Settings.DataDir, "servers", build ? $"{pack.Slug}-{version}-{files[0]["id"]}" : $"{pack.Slug}-{version}");
        bool fresh = !Directory.Exists(dir);
        if (!fresh) Log($"Serveur déjà installé, réutilisé (supprimez le dossier pour le réinstaller) : {dir}");
        else
        {
            if (MessageBox.Show(this, "Un serveur Minecraft nécessite d'accepter le CLUF (EULA) de Minecraft :\nhttps://aka.ms/MinecraftEULA\n\nL'acceptez-vous ?",
                    "EULA Minecraft", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            if (serverFile != null && serverFile != files[0])
            {
                var warning = $"La dernière version du modpack ({files[0]["displayName"]}) n'a pas de server pack.\n" +
                              $"Le serveur sera en « {serverFile["displayName"]} » : les joueurs doivent installer cette même version dans CurseForge.\n" +
                              "(Ou cochez « Sans server pack » pour un serveur dans la dernière version.)";
                Log("ATTENTION : " + warning.Replace("\n", " "));
                if (MessageBox.Show(this, warning + "\n\nContinuer ?", "Version du modpack", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            }

            try
            {
                if (serverFile == null)
                {
                    Log($"Construction du serveur « {files[0]["displayName"]} » sans server pack…");
                    await ServerBuilder.Build(api, files[0], dir, s => Ui(() => Log(s)));
                }
                else
                {
                    var sp = await api.ServerPackOf(serverFile);
                    Log($"Téléchargement et extraction de {sp["fileName"]}…");
                    await ServerFiles.Install(CurseForge.DownloadUrl(sp), dir, ServerFiles.Sha1Of(sp));
                }
            }
            catch
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true); // installation partielle, créée par nous
                throw;
            }
            Log($"Les joueurs doivent avoir la version « {(serverFile ?? files[0])["displayName"]} » du modpack.");
        }

        var root = ServerFiles.Unwrap(dir);
        if (fresh) File.WriteAllText(Path.Combine(root, "eula.txt"), "eula=true\n");
        if (ServerFiles.FindStartScript(root) == null) throw new Exception($"Aucun script de démarrage (.bat) trouvé dans {root}");

        int major = Java.MajorFor(version);
        Log($"Préparation de Java {major}…");
        var javaBin = await Java.Ensure(major);
        if (fresh)
            foreach (var installer in Directory.GetFiles(root, "install*.bat"))
            {
                Log($"Installation : {Path.GetFileName(installer)} (peut prendre quelques minutes)…");
                using var p = Launch(installer, javaBin);
                p.StandardInput.Close();
                await p.WaitForExitAsync();
            }
        if (fresh && build) await ServerBuilder.TrialBoot(ServerFiles.FindStartScript(root)!, javaBin, s => Ui(() => Log(s)));

        settings.ServerDir = root;
        settings.McVersion = version;
        settings.Save();
        RefreshServers();
        Log($"Serveur prêt : {root}");
    }
}
