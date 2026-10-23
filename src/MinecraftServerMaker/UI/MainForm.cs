// Fenêtre principale : mise en page, état des boutons et zone de notification.
partial class MainForm : Form
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

    // Ferme vraiment l'app (le serveur est arrêté proprement dans OnFormClosing).
    void Quit()
    {
        quitting = true;
        Close();
    }

    static Label Lbl(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 3) };

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
