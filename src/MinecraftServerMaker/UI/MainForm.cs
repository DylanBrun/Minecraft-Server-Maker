// Fenêtre principale : mise en page, état des boutons et zone de notification.
partial class MainForm : Form
{
    readonly Settings settings = Settings.Load();

    readonly TextBox apiKey = new() { Width = 360, UseSystemPasswordChar = true };
    readonly TextBox search = new() { Width = 300, PlaceholderText = "Nom, lien CurseForge ou Project ID" };
    readonly Button searchBtn = new() { Text = "Rechercher", AutoSize = true };
    readonly ComboBox packs = new() { Width = 520, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox versions = new() { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Button createBtn = new() { Text = "Créer le serveur", AutoSize = true, Tag = "primary" };
    readonly CheckBox buildBox = new() { Text = "Sans server pack (construire depuis la dernière version joueur)", AutoSize = true };
    readonly ComboBox servers = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly Button deleteBtn = new() { Text = "Supprimer", AutoSize = true, Tag = "danger" };
    readonly Button modsBtn = new() { Text = "Ajouter des mods…", AutoSize = true };
    readonly Button settingsBtn = new() { Text = "Réglages…", AutoSize = true };
    readonly PowerButton power = new() { Size = new Size(220, 220), Anchor = AnchorStyles.None };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 30, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI Semibold", 10.5f) };

    // TextBox en lecture seule plutôt qu'un Label : on peut sélectionner et copier une adresse.
    readonly TextBox addresses = new()
    {
        Dock = DockStyle.Bottom, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
        TextAlign = HorizontalAlignment.Center, Visible = false, TabStop = false,
    };
    readonly TextBox log = new() { Dock = DockStyle.Bottom, Height = 170, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = Theme.Mono };
    readonly TextBox command = new() { Dock = DockStyle.Bottom, Font = Theme.Mono, PlaceholderText = "Commande serveur (ex : op Pseudo, list, say Salut) puis Entrée" };
    readonly NotifyIcon tray = new() { Icon = Theme.AppIcon, Visible = true };
    readonly ToolStripMenuItem trayToggle = new();
    Process? server;
    bool quitting, busy, online;

    bool IsRunning => server is { HasExited: false };

    public MainForm()
    {
        Text = "Minecraft Server Maker";
        ClientSize = new Size(730, 760);
        MinimumSize = new Size(730, 700);
        apiKey.Text = settings.ApiKey;

        // Carte des réglages de création et de choix du serveur, posée sur le fond avec une marge.
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10), BackColor = Theme.Surface };
        top.Controls.AddRange([Lbl("Clé API CurseForge :"), apiKey, Lbl("Modpack :"), search, searchBtn, packs, Lbl("Version Minecraft :"), versions, createBtn, buildBox, Lbl("Serveur :"), servers, deleteBtn, modsBtn, settingsBtn]);
        top.SetFlowBreak(buildBox, true);
        top.SetFlowBreak(createBtn, true);
        top.SetFlowBreak(apiKey, true);
        top.SetFlowBreak(searchBtn, true);
        top.SetFlowBreak(packs, true);
        var card = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(14, 12, 14, 0) };
        card.Controls.Add(top);

        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        center.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        center.Controls.Add(power);
        // Le bouton suit la place disponible (la liste d'adresses en prend quand le serveur tourne).
        center.SizeChanged += (_, _) =>
        {
            int d = Math.Clamp(Math.Min(center.ClientSize.Width, center.ClientSize.Height) - 10, 110, 260);
            power.Size = new Size(d, d);
        };

        var file = new ToolStripMenuItem("&Fichier");
        file.DropDownItems.Add("Ouvrir le dossier du serveur", null, (_, _) =>
        {
            if (Directory.Exists(settings.ServerDir)) Process.Start(new ProcessStartInfo(settings.ServerDir) { UseShellExecute = true });
        });
        file.DropDownItems.Add("Ouvrir les journaux de l'app", null, (_, _) =>
        {
            Directory.CreateDirectory(AppLog.Dir);
            Process.Start(new ProcessStartInfo(AppLog.Dir) { UseShellExecute = true });
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

        Controls.AddRange([center, addresses, status, log, command, card, Header(), menuBar]); // ordre inverse du docking
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
        settingsBtn.Click += async (_, _) => await Run(OpenSettings);
        backupTimer.Tick += (_, _) => _ = BackupNow();

        Theme.Apply(this);
        Theme.StyleMenu(menu);
        Theme.MatchHeight(search, searchBtn);
        Theme.MatchHeight(versions, createBtn);
        Theme.MatchHeight(servers, deleteBtn, modsBtn, settingsBtn);
        addresses.BackColor = Theme.Background;
        addresses.ForeColor = Theme.Muted;

        if (settings.ServerDir != "") Log($"Serveur sélectionné : {settings.ServerDir}");
        RefreshServers();
        UpdateUi();
    }

    // Ferme vraiment l'app (le serveur est arrêté proprement dans OnFormClosing).
    void Quit()
    {
        quitting = true;
        Close();
    }

    static Label Lbl(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 3), ForeColor = Theme.Muted };

    // Bandeau du haut : logo, nom de l'app et un léger dégradé souligné de vert.
    static Panel Header()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 66 };
        header.Paint += (_, e) =>
        {
            using var fill = new System.Drawing.Drawing2D.LinearGradientBrush(header.ClientRectangle, Theme.Surface, Theme.Background, 0f);
            e.Graphics.FillRectangle(fill, header.ClientRectangle);
            using var line = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(line, 0, header.Height - 2, header.Width, 2);
        };
        header.Resize += (_, _) => header.Invalidate();
        var logo = new PictureBox { Image = Theme.Logo, SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(44, 44), Location = new Point(14, 10), BackColor = Color.Transparent };
        var title = new Label { Text = "Minecraft Server Maker", Font = Theme.Title, AutoSize = true, Location = new Point(64, 10), BackColor = Color.Transparent, ForeColor = Theme.Text };
        var subtitle = new Label { Text = "Votre serveur moddé en un clic", AutoSize = true, Location = new Point(66, 37), BackColor = Color.Transparent, ForeColor = Theme.Muted };
        header.Controls.AddRange([logo, title, subtitle]);
        return header;
    }

    // Cache toutes les fenêtres de l'app ; elle reste accessible par l'icône près de l'horloge.
    void HideToTray(string message)
    {
        foreach (var f in Application.OpenForms.Cast<Form>().ToList()) f.Hide();
        tray.ShowBalloonTip(2000, "Serveur Minecraft", message, ToolTipIcon.Info);
    }

    void UpdateUi()
    {
        bool running = IsRunning, hasServer = ServerFiles.FindStartScript(settings.ServerDir) != null;
        power.Enabled = !busy && hasServer;
        power.Text = running ? "ARRÊTER" : "LANCER";
        power.Running = running;
        searchBtn.Enabled = createBtn.Enabled = buildBox.Enabled = !busy;
        servers.Enabled = deleteBtn.Enabled = !busy && !running;
        modsBtn.Enabled = settingsBtn.Enabled = !busy && hasServer;
        addresses.Visible = running;
        command.Enabled = running && !busy;
        trayToggle.Text = running ? "Arrêter le serveur" : "Lancer le serveur";
        trayToggle.Enabled = power.Enabled;
        status.Text = busy && running ? "Arrêt en cours…"
            : busy ? "Patientez…"
            : running ? (online ? "En ligne ✔" : "Démarrage…")
            : hasServer ? $"Arrêté - {Path.GetFileName(settings.ServerDir)}"
            : "Aucun serveur : choisissez un modpack et créez-le";
        status.ForeColor = busy || (running && !online) ? Theme.Warning : running ? Theme.Accent : Theme.Muted;
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

    // fromServer : ligne de la console du serveur, déjà dans ses propres logs, donc pas recopiée dans le journal de l'app.
    void Log(string line, bool fromServer = false)
    {
        if (!fromServer) AppLog.Write(line);
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
