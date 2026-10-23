using System.Runtime.InteropServices;

// Thème sombre partagé par toutes les fenêtres : ardoise, vert "lancer", rouge "arrêter".
// Mêmes couleurs que l'icône et l'installeur (tools/AssetGen).
static class Theme
{
    public static readonly Color Background = Hex("#0F172A"), Surface = Hex("#1B2336"), Input = Hex("#1E293B"), Console = Hex("#0B1120"),
        Border = Hex("#334155"), BorderHover = Hex("#475569"), Text = Hex("#F8FAFC"), Muted = Hex("#94A3B8"),
        Accent = Hex("#22C55E"), AccentHover = Hex("#16A34A"), OnAccent = Hex("#0F172A"),
        Danger = Hex("#EF4444"), DangerHover = Hex("#3B1D24"), Warning = Hex("#F59E0B");

    public static readonly Font Body = new("Segoe UI", 9.5f), Strong = new("Segoe UI Semibold", 9.5f),
        Title = new("Segoe UI Semibold", 14f), Mono = new(MonoFamily(), 9f);

    public static Icon AppIcon { get; } = LoadIcon();
    public static Image Logo { get; } = LoadLogo();

    static Color Hex(string hex) => ColorTranslator.FromHtml(hex);
    static string MonoFamily() => FontFamily.Families.Any(f => f.Name == "Cascadia Mono") ? "Cascadia Mono" : "Consolas";

    static Icon LoadIcon()
    {
        using var s = typeof(Theme).Assembly.GetManifestResourceStream("app.ico");
        return s != null ? new Icon(s) : SystemIcons.Application;
    }

    static Image LoadLogo()
    {
        using var s = typeof(Theme).Assembly.GetManifestResourceStream("logo.png");
        return s != null ? new Bitmap(Image.FromStream(s)) : AppIcon.ToBitmap();
    }

    // Applique le thème à une fenêtre et à tout ce qu'elle contient.
    // Un bouton avec Tag = "primary" (vert) ou "danger" (rouge) prend la couleur de son rôle.
    public static void Apply(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        form.Font = Body;
        form.Icon = AppIcon;
        form.HandleCreated += (_, _) => DarkTitleBar(form.Handle);
        if (form.IsHandleCreated) DarkTitleBar(form.Handle);
        foreach (Control c in form.Controls) Style(c);
    }

    public static void Style(Control c)
    {
        switch (c)
        {
            case Button b: StyleButton(b); break;
            case TextBox t:
                t.BackColor = t.Multiline && t.ReadOnly ? Console : Input;
                t.ForeColor = Text;
                t.BorderStyle = t.Multiline ? BorderStyle.None : BorderStyle.FixedSingle;
                break;
            case ComboBox cb: cb.BackColor = Input; cb.ForeColor = Text; cb.FlatStyle = FlatStyle.Flat; break;
            case NumericUpDown n: n.BackColor = Input; n.ForeColor = Text; n.BorderStyle = BorderStyle.FixedSingle; break;
            case ListBox l: l.BackColor = Input; l.ForeColor = Text; l.BorderStyle = BorderStyle.None; break;
            case CheckBox ch: ch.ForeColor = Text; ch.Cursor = Cursors.Hand; break;
            case GroupBox gb: gb.ForeColor = Text; break; // sinon Windows dessine le titre en gris foncé, illisible sur le fond sombre
            case ToolStrip ts: StyleMenu(ts); break;
        }
        foreach (Control child in c.Controls) Style(child);
    }

    public static void StyleMenu(ToolStrip menu)
    {
        menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()) { RoundedEdges = false };
        menu.BackColor = Background;
        menu.ForeColor = Text;
        foreach (ToolStripItem item in menu.Items) StyleItem(item);
    }

    static void StyleItem(ToolStripItem item)
    {
        item.ForeColor = Text;
        if (item is ToolStripMenuItem m) foreach (ToolStripItem sub in m.DropDownItems) StyleItem(sub);
    }

    static void StyleButton(Button b)
    {
        var (back, hover, fore, border) = b.Tag as string switch
        {
            "primary" => (Accent, AccentHover, OnAccent, Accent),
            "danger" => (Surface, DangerHover, Danger, Danger),
            _ => (Border, BorderHover, Text, BorderHover),
        };
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderColor = border;
        b.FlatAppearance.MouseOverBackColor = hover;
        b.FlatAppearance.MouseDownBackColor = hover;
        b.BackColor = back;
        b.ForeColor = fore;
        b.Font = Strong;
        b.Cursor = Cursors.Hand;
        b.Padding = new Padding(6, 1, 6, 1);
    }

    // Barre de titre sombre (Windows 10 1809+ et Windows 11) ; sans effet ailleurs.
    static void DarkTitleBar(IntPtr handle)
    {
        int on = 1;
        if (DwmSetWindowAttribute(handle, 20, ref on, sizeof(int)) != 0) DwmSetWindowAttribute(handle, 19, ref on, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    sealed class DarkMenuColors : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin => Background;
        public override Color MenuStripGradientEnd => Background;
        public override Color ToolStripDropDownBackground => Surface;
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => BorderHover;
        public override Color MenuItemSelected => Border;
        public override Color MenuItemSelectedGradientBegin => Border;
        public override Color MenuItemSelectedGradientEnd => Border;
        public override Color MenuItemPressedGradientBegin => Surface;
        public override Color MenuItemPressedGradientEnd => Surface;
        public override Color CheckBackground => AccentHover;
        public override Color CheckSelectedBackground => Accent;
        public override Color CheckPressedBackground => Accent;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Border;
    }
}
