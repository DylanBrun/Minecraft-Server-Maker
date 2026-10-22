record ServerItem(string Dir)
{
    public override string ToString() => Path.GetFileName(Dir);
}

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
    readonly ComboBox servers = new() { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Button deleteBtn = new() { Text = "Supprimer", AutoSize = true };
    readonly Button modsBtn = new() { Text = "Ajouter des mods…", AutoSize = true };
    readonly Button power = new()
    {
        Size = new Size(220, 220), FlatStyle = FlatStyle.Flat, ForeColor = Color.White,
        Font = new Font("Segoe UI", 22, FontStyle.Bold), Anchor = AnchorStyles.None, Cursor = Cursors.Hand,
    };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 28, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    // TextBox en lecture seule plutôt qu'un Label : on peut sélectionner et copier une adresse.
    readonly TextBox addresses = new()
    {
        Dock = DockStyle.Bottom, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
        TextAlign = HorizontalAlignment.Center, Visible = false, TabStop = false,
    };
    readonly TextBox log = new() { Dock = DockStyle.Bottom, Height = 170, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 8.5f) };
    readonly TextBox command = new() { Dock = DockStyle.Bottom, Font = new Font("Consolas", 9f), PlaceholderText = "Commande serveur (ex : op Pseudo, list, say Salut) puis Entrée" };
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
        top.Controls.AddRange([Lbl("Clé API CurseForge :"), apiKey, Lbl("Modpack :"), search, searchBtn, packs, Lbl("Version Minecraft :"), versions, createBtn, buildBox, Lbl("Serveur :"), servers, deleteBtn, modsBtn]);
        top.SetFlowBreak(buildBox, true);
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
        // Le bouton suit la place disponible (la liste d'adresses en prend quand le serveur tourne).
        center.SizeChanged += (_, _) =>
        {
            int d = Math.Clamp(Math.Min(center.ClientSize.Width, center.ClientSize.Height) - 16, 90, 240);
            power.Size = new Size(d, d);
            var old = power.Font;
            power.Font = new Font(old.FontFamily, d / 7.5f, FontStyle.Bold, GraphicsUnit.Pixel);
            old.Dispose();
        };

        var file = new ToolStripMenuItem("&Fichier");
        file.DropDownItems.Add("Ouvrir le dossier du serveur", null, (_, _) =>
        {
            if (Directory.Exists(settings.ServerDir)) Process.Start(new ProcessStartInfo(settings.ServerDir) { UseShellExecute = true });
        });
        var hideOnStart = new ToolStripMenuItem("Réduire dans la barre des tâches au lancement du serveur")
            { CheckOnClick = true, Checked = settings.HideOnStart };
        hideOnStart.CheckedChanged += (_, _) => { settings.HideOnStart = hideOnStart.Checked; settings.Save(); };
        var autoRadmin = new ToolStripMenuItem("Lancer et activer Radmin VPN au démarrage du serveur")
            { CheckOnClick = true, Checked = settings.AutoRadmin };
        autoRadmin.CheckedChanged += (_, _) => { settings.AutoRadmin = autoRadmin.Checked; settings.Save(); };
        var prefs = new ToolStripMenuItem("&Paramètres");
        prefs.DropDownItems.AddRange([hideOnStart, autoRadmin]);
        file.DropDownItems.Add(prefs);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Quitter", null, (_, _) => Quit());
        var menuBar = new MenuStrip();
        menuBar.Items.Add(file);
        MainMenuStrip = menuBar;

        Controls.AddRange([center, addresses, status, log, command, top, menuBar]); // ordre inverse du docking
        command.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter || command.Text.Trim() == "" || server is not { HasExited: false } p) return;
            e.SuppressKeyPress = true;
            var cmd = command.Text.Trim().TrimStart('/'); // la console du serveur n'attend pas de "/"
            try { p.StandardInput.WriteLine(cmd); Log("> " + cmd); command.Clear(); }
            catch (IOException) { Log("Le serveur n'accepte plus de commandes (il s'arrête)."); }
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Ouvrir", null, (_, _) => ShowWindow());
        menu.Items.Add(trayToggle);
        menu.Items.Add("Quitter", null, (_, _) => Quit());
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

        servers.SelectedIndexChanged += (_, _) =>
        {
            if (servers.SelectedItem is not ServerItem s || ServerFiles.Unwrap(s.Dir) == settings.ServerDir) return;
            settings.ServerDir = ServerFiles.Unwrap(s.Dir);
            settings.McVersion = ServerFiles.VersionOf(s.Dir);
            settings.Save();
            Log($"Serveur sélectionné : {settings.ServerDir}");
            UpdateUi();
        };
        deleteBtn.Click += async (_, _) => await Run(DeleteServer);
        modsBtn.Click += async (_, _) => await Run(OpenMods);

        if (settings.ServerDir != "") Log($"Serveur sélectionné : {settings.ServerDir}");
        RefreshServers();
        UpdateUi();
    }

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

    // Ferme vraiment l'app (le serveur est arrêté proprement dans OnFormClosing).
    void Quit()
    {
        quitting = true;
        Close();
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
        RefreshServers();
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
        server = Launch(script, javaBin, () => Ui(() => { Log("Serveur arrêté."); UpdateUi(); }), nogui: settings.HideOnStart);
        Log($"Démarrage : {script}");
        if (settings.HideOnStart) HideToTray("Le serveur démarre en arrière-plan.");
        if (settings.AutoRadmin) await EnsureRadmin();
        _ = ShowAddresses(); // après Radmin, pour que son adresse soit listée
    }

    async Task EnsureRadmin()
    {
        var gui = Radmin.GuiPath();
        if (gui == null)
        {
            Log("Radmin VPN n'est pas installé sur ce PC (https://www.radmin-vpn.com) : le serveur démarre sans.");
            tray.ShowBalloonTip(4000, "Radmin VPN introuvable", "Installez Radmin VPN ou désactivez l'option dans Fichier > Paramètres.", ToolTipIcon.Warning);
            return;
        }
        // Lance Radmin s'il est fermé, ou ramène sa fenêtre au premier plan s'il tourne déjà.
        Process.Start(new ProcessStartInfo(gui, "/show") { UseShellExecute = true });
        Log("Radmin VPN ouvert.");
        for (int i = 0; i < 20 && !Radmin.Connected(); i++) await Task.Delay(1000);
        if (Radmin.Connected()) { Log("Radmin VPN est actif."); return; }
        // Son bouton d'alimentation n'a ni commande ni réglage modifiable sans droits administrateur.
        Log("Radmin VPN est éteint : cliquez sur son bouton d'alimentation pour l'activer.");
        tray.ShowBalloonTip(5000, "Radmin VPN est éteint", "Cliquez sur le bouton d'alimentation de Radmin VPN pour l'activer.", ToolTipIcon.Warning);
    }

    // Cache toutes les fenêtres de l'app ; elle reste accessible par l'icône près de l'horloge.
    void HideToTray(string message)
    {
        foreach (var f in Application.OpenForms.Cast<Form>().ToList()) f.Hide();
        tray.ShowBalloonTip(2000, "Serveur Minecraft", message, ToolTipIcon.Info);
    }

    async Task ShowAddresses()
    {
        var props = Path.Combine(settings.ServerDir, "server.properties");
        var port = File.Exists(props) ? File.ReadLines(props).FirstOrDefault(l => l.StartsWith("server-port="))?["server-port=".Length..].Trim() : null;
        if (string.IsNullOrEmpty(port)) port = "25565";

        var lines = new List<string> { "Adresses pour se connecter :", $"Ce PC : localhost:{port}" };
        lines.AddRange(NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254."))
                .Select(a => $"{n.Name} : {a.Address}:{port}")));
        SetAddresses(lines);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            if (IPAddress.TryParse((await http.GetStringAsync("https://api.ipify.org")).Trim(), out var ip))
            {
                lines.Add($"Internet : {ip}:{port} (si le port {port} est ouvert sur la box)");
                SetAddresses(lines);
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException) { } // hors ligne : on garde les adresses locales
    }

    void SetAddresses(List<string> lines)
    {
        addresses.Text = string.Join(Environment.NewLine, lines);
        addresses.Height = (lines.Count + 1) * addresses.Font.Height;
    }

    // Lance un .bat en arrière-plan avec le bon Java en tête du PATH ; sa sortie va dans le journal.
    Process Launch(string script, string? javaBin, Action? onExit = null, bool nogui = false)
    {
        var p = new Process { StartInfo = ServerFiles.StartInfo(script, javaBin, nogui), EnableRaisingEvents = true };
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
        servers.Enabled = deleteBtn.Enabled = !busy && !running;
        modsBtn.Enabled = !busy && hasServer;
        addresses.Visible = running;
        command.Enabled = running && !busy;
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
            TaskDialogButton yes = new("Oui"), minimize = new("Minimiser"), no = new("Non");
            var choice = TaskDialog.ShowDialog(this, new TaskDialogPage
            {
                Caption = "Minecraft Server Maker",
                Heading = "Voulez-vous arrêter le logiciel ?",
                Text = IsRunning ? "Le serveur en cours sera arrêté proprement (monde sauvegardé)."
                                 : "« Minimiser » le laisse tourner près de l'horloge.",
                Icon = TaskDialogIcon.Information,
                Buttons = { yes, minimize, no },
                DefaultButton = yes,
                AllowCancel = true, // la croix du message = Non
            });
            if (choice == yes) BeginInvoke(Quit); // pas de Close() imbriqué dans l'événement de fermeture
            else if (choice == minimize) HideToTray("L'application continue dans la barre des tâches.");
            return;
        }
        StopServer().Wait(); // sauvegarde le monde avant de quitter / éteindre Windows
        tray.Visible = false;
        base.OnFormClosing(e);
    }
}
