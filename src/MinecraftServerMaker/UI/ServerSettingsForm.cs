// Réglages propres au serveur sélectionné : pour l'instant la RAM.
class ServerSettingsForm : Form
{
    const int StepMb = 512; // la jauge avance par demi-gigaoctet

    readonly CheckBox auto = new() { Text = "Laisser le modpack choisir la RAM", AutoSize = true };
    readonly TrackBar slider = new() { Width = 400, TickStyle = TickStyle.BottomRight, LargeChange = 2, SmallChange = 1 };
    readonly NumericUpDown box = new() { Width = 80, DecimalPlaces = 1, Increment = 0.5m, TextAlign = HorizontalAlignment.Right };
    readonly Button max = new() { Text = "Max", AutoSize = true };
    readonly Label info = new() { AutoSize = true, MaximumSize = new Size(420, 0) };
    bool syncing;

    public ServerConfig Config { get; }

    public ServerSettingsForm(string root)
    {
        Config = ServerConfig.Load(root);
        Text = $"Réglages du serveur — {Path.GetFileName(root)}";
        Icon = SystemIcons.Application;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(470, 280);
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

        var ram = new GroupBox { Text = "Mémoire (RAM) du serveur", Padding = new Padding(10), Dock = DockStyle.Fill };
        var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Dock = DockStyle.Fill, WrapContents = false };
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        row.Controls.AddRange([box, new Label { Text = "Go", AutoSize = true, Margin = new Padding(3, 7, 10, 3) }, max]);
        layout.Controls.AddRange([auto, slider, row, info]);
        ram.Controls.Add(layout);

        var ok = new Button { Text = "Enregistrer", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };
        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Padding = new Padding(0, 8, 0, 0) };
        buttons.Controls.AddRange([cancel, ok]);
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange([ram, buttons]); // ordre inverse du docking

        // Jauge et case restent synchronisées ; la valeur est toujours bornée au maximum conseillé.
        slider.ValueChanged += (_, _) => { if (!syncing) SetRam(slider.Value * StepMb); };
        box.ValueChanged += (_, _) => { if (!syncing) SetRam((int)(box.Value * 1024)); };
        max.Click += (_, _) => SetRam(safeMax);
        auto.CheckedChanged += (_, _) => slider.Enabled = box.Enabled = max.Enabled = !auto.Checked;

        auto.Checked = Config.RamMb == 0;
        SetRam(Config.RamMb > 0 ? Config.RamMb : Math.Min(6144, safeMax));
        ok.Click += (_, _) => Config.RamMb = auto.Checked ? 0 : (int)(box.Value * 1024);
    }

    void SetRam(int mb)
    {
        syncing = true;
        slider.Value = Math.Clamp((int)Math.Round(mb / (double)StepMb), slider.Minimum, slider.Maximum);
        box.Value = Math.Clamp(mb / 1024m, box.Minimum, box.Maximum);
        syncing = false;
    }
}
