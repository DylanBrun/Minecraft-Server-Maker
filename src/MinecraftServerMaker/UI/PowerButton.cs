using System.Drawing.Drawing2D;

// Gros bouton rond Lancer/Arrêter : disque vert ou rouge avec un halo, symbole marche/arrêt et libellé.
// Il réagit au survol et au clic, et s'utilise au clavier (Tab puis Entrée ou Espace).
sealed class PowerButton : Control
{
    bool running, hover, pressed;

    public PowerButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleRole = AccessibleRole.PushButton;
    }

    // true : le serveur tourne, le bouton sert à l'arrêter (rouge).
    public bool Running
    {
        get => running;
        set { running = value; Invalidate(); }
    }

    protected override void OnTextChanged(EventArgs e) { AccessibleName = Text; base.OnTextChanged(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Enter or Keys.Space && Enabled) { OnClick(EventArgs.Empty); e.Handled = true; }
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        float size = Math.Min(Width, Height), cx = Width / 2f, cy = Height / 2f;
        float r = size / 2f * 0.80f * (pressed ? 0.96f : 1f);
        var main = !Enabled ? Theme.Border : running ? Theme.Danger : Theme.Accent;

        // Halo : quelques cercles transparents, plus marqués au survol.
        if (Enabled)
            for (int i = 6; i >= 1; i--)
            {
                using var halo = new SolidBrush(Color.FromArgb(hover ? 22 : 12, main));
                float hr = r + i * size * 0.016f;
                g.FillEllipse(halo, cx - hr, cy - hr, hr * 2, hr * 2);
            }

        var disc = new RectangleF(cx - r, cy - r, r * 2, r * 2);
        using (var fill = new LinearGradientBrush(disc, Shade(main, hover ? 0.25f : 0.12f), Shade(main, -0.18f), LinearGradientMode.Vertical))
            g.FillEllipse(fill, disc);
        using (var ring = new Pen(Color.FromArgb(55, 255, 255, 255), Math.Max(1f, size * 0.010f)))
            g.DrawEllipse(ring, cx - r * 0.90f, cy - r * 0.90f, r * 1.80f, r * 1.80f);

        // Texte foncé sur le vert, blanc sur le rouge : contraste suffisant dans les deux cas.
        var fore = !Enabled ? Theme.Muted : running ? Color.White : Theme.OnAccent;
        float icon = r * 0.27f, iconY = cy - r * 0.20f;
        using (var pen = new Pen(fore, Math.Max(2f, r * 0.075f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawArc(pen, cx - icon, iconY - icon, icon * 2, icon * 2, -60, 300);
            g.DrawLine(pen, cx, iconY - icon * 1.30f, cx, iconY - icon * 0.15f);
        }
        using (var font = new Font("Segoe UI Semibold", Math.Max(8f, r * 0.20f), GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(fore))
            g.DrawString(Text, font, brush, new RectangleF(cx - r, cy + r * 0.22f, r * 2, r * 0.4f),
                new StringFormat { Alignment = StringAlignment.Center });

        if (Focused && ShowFocusCues)
            using (var focus = new Pen(Theme.Text, 2) { DashStyle = DashStyle.Dot })
                g.DrawEllipse(focus, cx - r - 6, cy - r - 6, (r + 6) * 2, (r + 6) * 2);
    }

    // Éclaircit (amount > 0) ou assombrit (amount < 0) une couleur.
    static Color Shade(Color c, float amount)
    {
        int Mix(int v) => (int)Math.Clamp(amount >= 0 ? v + (255 - v) * amount : v * (1 + amount), 0, 255);
        return Color.FromArgb(c.A, Mix(c.R), Mix(c.G), Mix(c.B));
    }
}
