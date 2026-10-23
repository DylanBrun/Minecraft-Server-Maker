// Réglages propres au serveur sélectionné : RAM et sauvegardes automatiques du monde.
class ServerSettingsForm : Form
{
    const int StepMb = 512; // la jauge avance par demi-gigaoctet

    readonly CheckBox auto = new() { Text = "Laisser le modpack choisir la RAM", AutoSize = true };
    readonly TrackBar slider = new() { Width = 400, TickStyle = TickStyle.BottomRight, LargeChange = 2, SmallChange = 1 };
    readonly NumericUpDown box = new() { Width = 80, DecimalPlaces = 1, Increment = 0.5m, TextAlign = HorizontalAlignment.Right };
    readonly Button max = new() { Text = "Max", AutoSize = true };
    readonly Label info = new() { AutoSize = true, MaximumSize = new Size(420, 0) };
    readonly CheckBox backup = new() { Text = "Sauvegarder le monde automatiquement", AutoSize = true };
    readonly NumericUpDown minutes = new() { Width = 70, Minimum = 1, Maximum = 1440, TextAlign = HorizontalAlignment.Right };
    readonly NumericUpDown keep = new() { Width = 70, Minimum = 1, Maximum = 100, TextAlign = HorizontalAlignment.Right };
    bool syncing;

    public ServerConfig Config { get; }

    public ServerSettingsForm(string root)
    {
        Config = ServerConfig.Load(root);
        Text = $"Réglages du serveur — {Path.GetFileName(root)}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(470, 440);
        Padding = new Padding(10);

        long totalMb = Memory.TotalMb();
        int safeMax = Memory.SafeMaxMb(totalMb);
        slider.Minimum = 1024 / StepMb;
        slider.Maximum = Math.Max(slider.Minimum, safeMax / StepMb);
        slider.TickFrequency = 1024 / StepMb;
        box.Minimum = 1;
        box.Maximum = slider.Maximum * StepMb / 1024m;
        info.Text = $"RAM du PC : {totalMb / 1024.0:0.#} Go. Maximum conseillé : {box.Maximum:0.#} Go " +
                    "(8 Go restent pour Windows et pour le jeu si tu joues sur ce PC). Pris en compte au prochain lancement.";

        var ram = new GroupBox { Text = "Mémoire (RAM) du serveur", Padding = new Padding(10), Dock = DockStyle.Top, Height = 200 };
        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill, WrapContents = false };
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        row.Controls.AddRange([box, new Label { Text = "Go", AutoSize = true, Margin = new Padding(3, 7, 10, 3) }, max]);
        layout.Controls.AddRange([auto, slider, row, info]);
        ram.Controls.Add(layout);

        var saves = new GroupBox { Text = "Sauvegardes automatiques du monde", Padding = new Padding(10), Dock = DockStyle.Fill };
        var savesLayout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill, WrapContents = false };
        static FlowLayoutPanel Line(params Control[] controls)
        {
            var line = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            line.Controls.AddRange(controls);
            return line;
        }
        static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 3) };
        savesLayout.Controls.AddRange([
            backup,
            Line(Caption("Une copie toutes les"), minutes, Caption("minutes")),
            Line(Caption("Garder les"), keep, Caption("dernières copies")),
            new Label { AutoSize = true, MaximumSize = new Size(420, 0), Text =
                "Les copies (.zip) vont dans le dossier backups du serveur, pendant qu'il tourne. " +
                "En jeu, un opérateur peut taper dans le chat : !ASWorld enable, !ASWorld disable ou !ASWorld set <minutes>." },
        ]);
        saves.Controls.Add(savesLayout);

        var ok = new Button { Text = "Enregistrer", AutoSize = true, DialogResult = DialogResult.OK, Tag = "primary" };
        var cancel = new Button { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Padding = new Padding(0, 8, 0, 0) };
        buttons.Controls.AddRange([cancel, ok]);
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange([saves, ram, buttons]); // ordre inverse du docking

        // Jauge et case restent synchronisées ; la valeur est toujours bornée au maximum conseillé.
        slider.ValueChanged += (_, _) => { if (!syncing) SetRam(slider.Value * StepMb); };
        box.ValueChanged += (_, _) => { if (!syncing) SetRam((int)(box.Value * 1024)); };
        max.Click += (_, _) => SetRam(safeMax);
        auto.CheckedChanged += (_, _) => slider.Enabled = box.Enabled = max.Enabled = !auto.Checked;
        backup.CheckedChanged += (_, _) => minutes.Enabled = keep.Enabled = backup.Checked;

        auto.Checked = Config.RamMb == 0;
        SetRam(Config.RamMb > 0 ? Config.RamMb : Math.Min(6144, safeMax));
        backup.Checked = Config.AutoBackup;
        minutes.Enabled = keep.Enabled = backup.Checked;
        minutes.Value = Math.Clamp(Config.BackupMinutes, 1, 1440);
        keep.Value = Math.Clamp(Config.BackupsToKeep, 1, 100);
        Theme.Apply(this);
        ok.Click += (_, _) =>
        {
            Config.RamMb = auto.Checked ? 0 : (int)(box.Value * 1024);
            Config.AutoBackup = backup.Checked;
            Config.BackupMinutes = (int)minutes.Value;
            Config.BackupsToKeep = (int)keep.Value;
        };
    }

    void SetRam(int mb)
    {
        syncing = true;
        slider.Value = Math.Clamp((int)Math.Round(mb / (double)StepMb), slider.Minimum, slider.Maximum);
        box.Value = Math.Clamp(mb / 1024m, box.Minimum, box.Maximum);
        syncing = false;
    }
}
