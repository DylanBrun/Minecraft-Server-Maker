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
            case CheckBox ch: StyleCheckBox(ch); break;
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

        // On redessine tout le bouton : Windows ajoute ses propres marges au texte et en rogne le bas
        // quand le bouton a la hauteur d'un champ. Ici le texte est centré sur toute la hauteur.
        bool over = false;
        b.MouseEnter += (_, _) => { over = true; b.Invalidate(); };
        b.MouseLeave += (_, _) => { over = false; b.Invalidate(); };
        b.Paint += (_, e) =>
        {
            var rect = b.ClientRectangle;
            using (var fill = new SolidBrush(!b.Enabled ? Surface : over ? hover : back)) e.Graphics.FillRectangle(fill, rect);
            using (var pen = new Pen(b.Enabled ? border : Border)) e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
            TextRenderer.DrawText(e.Graphics, b.Text, b.Font, rect, b.Enabled ? fore : Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (b.Focused) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(rect, -3, -3), fore, back);
        };
    }

    // Case à cocher redessinée : carré et libellé centrés tous les deux sur la hauteur du contrôle.
    static void StyleCheckBox(CheckBox ch)
    {
        ch.ForeColor = Text;
        ch.Cursor = Cursors.Hand;
        ch.MinimumSize = new Size(0, 24);
        ch.Padding = new Padding(0, 0, 10, 0); // le libellé démarre plus à droite que sur la case Windows
        bool over = false;
        ch.MouseEnter += (_, _) => { over = true; ch.Invalidate(); };
        ch.MouseLeave += (_, _) => { over = false; ch.Invalidate(); };
        ch.CheckedChanged += (_, _) => ch.Invalidate();
        ch.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.Clear(ch.Parent?.BackColor ?? Background);
            int box = Math.Min(16, ch.Height - 4), y = (ch.Height - box) / 2;
            var square = new Rectangle(1, y, box, box);
            using (var fill = new SolidBrush(ch.Checked ? Accent : over ? Border : Input)) g.FillRectangle(fill, square);
            using (var pen = new Pen(ch.Checked ? Accent : BorderHover)) g.DrawRectangle(pen, square);
            if (ch.Checked)
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using var tick = new Pen(OnAccent, 2f);
                g.DrawLines(tick, [new PointF(square.X + box * 0.22f, y + box * 0.52f), new PointF(square.X + box * 0.42f, y + box * 0.72f), new PointF(square.X + box * 0.80f, y + box * 0.28f)]);
            }
            var textRect = new Rectangle(square.Right + 7, 0, ch.Width - square.Right - 7, ch.Height);
            TextRenderer.DrawText(g, ch.Text, ch.Font, textRect, ch.Enabled ? Text : Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (ch.Focused) ControlPaint.DrawFocusRectangle(g, textRect);
        };
    }

    // Donne aux boutons la hauteur du champ de saisie de leur ligne, et les aligne sur lui.
    // Suit les changements de taille du champ (mise à l'échelle de l'écran).
    public static void MatchHeight(Control field, params Button[] buttons)
    {
        void Apply()
        {
            foreach (var b in buttons)
            {
                b.AutoSize = false;
                b.Padding = Padding.Empty; // à la hauteur d'un champ, la marge intérieure rognerait le texte
                b.Size = new Size(TextRenderer.MeasureText(b.Text, b.Font).Width + 26, field.Height);
                b.Margin = new Padding(b.Margin.Left, field.Margin.Top, b.Margin.Right, field.Margin.Bottom);
            }
        }
        field.SizeChanged += (_, _) => Apply();
        Apply();
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
