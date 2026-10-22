class MainForm : Form
{
    readonly Settings settings = Settings.Load();
    readonly TextBox apiKey = new() { Width = 360, UseSystemPasswordChar = true };
    readonly TextBox search = new() { Width = 280, PlaceholderText = "Nom, lien CurseForge ou Project ID" };
    readonly Button searchBtn = new() { Text = "Rechercher", AutoSize = true };
    readonly ComboBox packs = new() { Width = 500, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox versions = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Button createBtn = new() { Text = "Créer le serveur", AutoSize = true };
    readonly CheckBox buildBox = new() { Text = "Sans server pack (construire depuis la dernière version joueur)", AutoSize = true };
    readonly Button power = new()
    {
        Size = new Size(220, 220), FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
        Font = new Font("Segoe UI", 22, FontStyle.Bold), Anchor = AnchorStyles.None, Cursor = Cursors.Hand,
    };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    readonly TextBox log = new() { Dock = DockStyle.Bottom, Height = 170, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5f) };
    readonly NotifyIcon tray = new() { Icon = SystemIcons.Application, Visible = true };
    readonly ToolStripMenuItem trayToggle = new();
    Process? server;
    bool quitting, busy, online;

    bool IsRunning => server is { HasExited: false };

    public MainForm()
    {
        Text = "Minecraft Server Maker";
        Icon = SystemIcons.Application;
        ClientSize = new Size(560, 640);
        MinimumSize = new Size(560, 600);
        apiKey.Text = settings.ApiKey;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        top.Controls.AddRange([Lbl("Clé API CurseForge :"), apiKey, Lbl("Modpack :"), search, searchBtn, packs, Lbl("Version Minecraft :"), versions, createBtn, buildBox]);
        top.SetFlowBreak(createBtn, true);
        top.SetFlowBreak(apiKey, true);
        top.SetFlowBreak(searchBtn, true);
        top.SetFlowBreak(packs, true);

        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        center.Controls.Add(power);
        // Recalculé à chaque redimensionnement : la mise à l'échelle DPI ne garde pas le bouton carré.
        power.SizeChanged += (_, _) =>
        {
            int d = Math.Min(power.Width, power.Height);
            using var round = new GraphicsPath();
            round.AddEllipse((power.Width - d) / 2, (power.Height - d) / 2, d, d);
            power.Region = new Region(round);
        };
        power.FlatAppearance.BorderSize = 0;

        Controls.AddRange([center, status, log, top]); // ordre inverse du docking

        var menu = new ContextMenuStrip();
        menu.Items.Add("Ouvrir", null, (_, _) => ShowWindow());
        menu.Items.Add(trayToggle);
        menu.Items.Add("Quitter", null, (_, _) => { quitting = true; Close(); });
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => ShowWindow();
        trayToggle.Click += (_, _) => Toggle();
        power.Click += (_, _) => Toggle();

        searchBtn.Click += async (_, _) => await Run(SearchPacks);
        search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; searchBtn.PerformClick(); } };
        createBtn.Click += async (_, _) => await Run(CreateServer);
        packs.SelectedIndexChanged += (_, _) =>
        {
            versions.Items.Clear();
            if (packs.SelectedItem is Pack p) versions.Items.AddRange(p.Versions);
            if (versions.Items.Count > 0) versions.SelectedIndex = 0;
        };


        if (settings.ServerDir != "") Log($"Serveur sélectionné : {settings.ServerDir}");
        UpdateUi();
    }

    static Label Lbl(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 3) };

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
                    await ServerFiles.Install(CurseForge.DownloadUrl(sp), dir);
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
        Log($"Serveur prêt : {root}");
    }

    void Toggle()
    {
        if (!busy) _ = Run(IsRunning ? StopServer : StartServer);
    }

    async Task StartServer()
    {
        var script = ServerFiles.FindStartScript(settings.ServerDir) ?? throw new Exception("Créez d'abord un serveur.");
        // Anciennes installs sans version connue : Java du système.
        string? javaBin = settings.McVersion == "" ? null : await Java.Ensure(Java.MajorFor(settings.McVersion));
        online = false;
        server = Launch(script, javaBin, () => Ui(() => { Log("Serveur arrêté."); UpdateUi(); }));
        Log($"Démarrage : {script}");
    }

    // Lance un .bat en arrière-plan avec le bon Java en tête du PATH ; sa sortie va dans le journal.
    Process Launch(string script, string? javaBin, Action? onExit = null)
    {
        var p = new Process { StartInfo = ServerFiles.StartInfo(script, javaBin), EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => OnServerLine(e.Data);
        p.ErrorDataReceived += (_, e) => OnServerLine(e.Data);
        if (onExit != null) p.Exited += (_, _) => onExit();
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    // Pas d'accès UI ici + ConfigureAwait(false) : on peut faire .Wait() dessus à la fermeture.
    async Task StopServer()
    {
        if (server is not { HasExited: false } p) return;
        try { p.StandardInput.WriteLine("stop"); p.StandardInput.Close(); } catch (IOException) { }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await p.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { p.Kill(entireProcessTree: true); }
    }

    void OnServerLine(string? line)
    {
        if (line == null) return;
        if (ServerFiles.IsCrash(line) && server is { HasExited: false } crashed)
        {
            try { crashed.StandardInput.Close(); } catch (IOException) { } // débloque le "pause" final du .bat
            // La JVM peut rester bloquée après un crash ; on lui laisse le temps de sauvegarder puis on la ferme.
            _ = Task.Delay(TimeSpan.FromSeconds(60)).ContinueWith(_ => { if (!crashed.HasExited) crashed.Kill(true); });
            Ui(() => Log("Le serveur a planté : voir le dossier crash-reports du serveur."));
        }
        Ui(() =>
        {
            Log(line);
            if (!online && line.Contains("Done (") && line.Contains("help"))
            {
                online = true;
                tray.ShowBalloonTip(3000, "Serveur Minecraft", "Le serveur est en ligne.", ToolTipIcon.Info);
                UpdateUi();
            }
        });
    }

    void UpdateUi()
    {
        bool running = IsRunning, hasServer = ServerFiles.FindStartScript(settings.ServerDir) != null;
        power.Enabled = !busy && hasServer;
        power.Text = running ? "ARRÊTER" : "LANCER";
        power.BackColor = !power.Enabled ? Color.Gainsboro : running ? Color.Firebrick : Color.ForestGreen;
        searchBtn.Enabled = createBtn.Enabled = buildBox.Enabled = !busy;
        trayToggle.Text = running ? "Arrêter le serveur" : "Lancer le serveur";
        trayToggle.Enabled = power.Enabled;
        status.Text = busy && running ? "Arrêt en cours…"
            : busy ? "Patientez…"
            : running ? (online ? "En ligne ✔" : "Démarrage…")
            : hasServer ? $"Arrêté — {Path.GetFileName(settings.ServerDir)}"
            : "Aucun serveur : choisissez un modpack et créez-le";
        var tip = "Minecraft : " + status.Text;
        tray.Text = tip.Length > 63 ? tip[..63] : tip;
    }

    async Task Run(Func<Task> action)
    {
        busy = true;
        UpdateUi();
        try { await action(); }
        catch (Exception ex)
        {
            Log("ERREUR : " + ex.Message);
            MessageBox.Show(this, ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { busy = false; UpdateUi(); }
    }

    void Log(string line)
    {
        if (log.TextLength > 200_000) log.Text = log.Text[^100_000..];
        log.AppendText(line + Environment.NewLine);
    }

    void Ui(Action a)
    {
        try { BeginInvoke(a); } catch (InvalidOperationException) { } // fenêtre déjà fermée
    }

    void ShowWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && !quitting)
        {
            e.Cancel = true;
            Hide();
            tray.ShowBalloonTip(2000, "Serveur Minecraft", "L'application continue dans la barre des tâches.", ToolTipIcon.Info);
            return;
        }
        StopServer().Wait(); // sauvegarde le monde avant de quitter / éteindre Windows
        tray.Visible = false;
        base.OnFormClosing(e);
    }
}
