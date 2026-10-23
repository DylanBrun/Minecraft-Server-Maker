using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Dessine l'identité visuelle : un bloc d'herbe isométrique en pixel-art, posé sur un carré arrondi ardoise,
// avec une pastille verte marche/arrêt. Mêmes couleurs que l'app (UI/Theme.cs).
var root = args.Length > 0 ? args[0] : FindRepoRoot();
var assets = Directory.CreateDirectory(Path.Combine(root, "src", "MinecraftServerMaker", "Assets")).FullName;
var installer = Directory.CreateDirectory(Path.Combine(root, "installer")).FullName;

WriteIco(Path.Combine(assets, "app.ico"), [16, 20, 24, 32, 40, 48, 64, 128, 256]);
Save(Art.Icon(256), Path.Combine(assets, "logo.png"), ImageFormat.Png);
Save(Art.Wizard(164, 314), Path.Combine(installer, "wizard.bmp"), ImageFormat.Bmp);
Save(Art.Wizard(328, 628), Path.Combine(installer, "wizard-2x.bmp"), ImageFormat.Bmp);
Save(Art.Small(55), Path.Combine(installer, "wizard-small.bmp"), ImageFormat.Bmp);
Save(Art.Small(110), Path.Combine(installer, "wizard-small-2x.bmp"), ImageFormat.Bmp);
Console.WriteLine($"Images écrites dans {assets} et {installer}");

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MinecraftServerMaker.sln"))) dir = dir.Parent;
    return dir?.FullName ?? throw new Exception("Racine du dépôt introuvable : passez-la en argument.");
}

static void Save(Bitmap bmp, string path, ImageFormat format)
{
    using (bmp)
    {
        if (format == ImageFormat.Bmp) // Inno Setup 6.2 lit des BMP 24 bits sans transparence
        {
            using var flat = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(flat)) g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
            flat.Save(path, format);
        }
        else bmp.Save(path, format);
    }
}

// Fichier .ico : PNG pour 256 px, bitmap 32 bits classique en dessous. System.Drawing.Icon (barre de titre,
// zone de notification) ne sait pas décoder un PNG de petite taille et l'affiche comme du bruit.
static void WriteIco(string path, int[] sizes)
{
    var pngs = sizes.Select(s => { using var bmp = Art.Icon(s); return s >= 256 ? Png(bmp) : Dib(bmp); }).ToList();
    using var w = new BinaryWriter(File.Create(path));
    w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
    int offset = 6 + 16 * sizes.Length;
    for (int i = 0; i < sizes.Length; i++)
    {
        w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
        w.Write(pngs[i].Length); w.Write(offset);
        offset += pngs[i].Length;
    }
    foreach (var png in pngs) w.Write(png);
}

static byte[] Png(Bitmap bmp)
{
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

// Image d'icône "DIB" : en-tête BITMAPINFOHEADER, pixels BGRA de bas en haut, puis masque AND (vide : l'alpha suffit).
static byte[] Dib(Bitmap bmp)
{
    int n = bmp.Width, maskRow = (n + 31) / 32 * 4;
    using var ms = new MemoryStream();
    using var w = new BinaryWriter(ms);
    w.Write(40); w.Write(n); w.Write(n * 2); w.Write((short)1); w.Write((short)32);
    w.Write(0); w.Write(n * n * 4 + maskRow * n); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
    for (int y = n - 1; y >= 0; y--)
        for (int x = 0; x < n; x++)
        {
            var c = bmp.GetPixel(x, y);
            w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
        }
    w.Write(new byte[maskRow * n]);
    return ms.ToArray();
}

static class Art
{
    static readonly Color Bg = ColorTranslator.FromHtml("#0F172A"), Card = ColorTranslator.FromHtml("#1E293B"), Accent = ColorTranslator.FromHtml("#22C55E");
    static readonly Color[] Grass = new[] { "#5DBB3F", "#4FA834", "#6CCB4B", "#57B23A" }.Select(ColorTranslator.FromHtml).ToArray();
    static readonly Color[] Dirt = new[] { "#8B5A2B", "#7A4E25", "#9A6634", "#6F4521" }.Select(ColorTranslator.FromHtml).ToArray();

    public static Bitmap Icon(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float r = size * 0.22f;
        using (var path = Rounded(new RectangleF(0, 0, size - 1, size - 1), r))
        using (var brush = new LinearGradientBrush(new PointF(0, 0), new PointF(0, size), Card, Bg))
            g.FillPath(brush, path);
        // Petites tailles : moins de cubes par face, sinon la texture devient de la bouillie.
        // Cube centré verticalement : il mesure 0,25 + 0,25 + 0,56 de sa largeur en hauteur.
        float width = size * (size <= 24 ? 0.80f : 0.60f);
        Block(g, size / 2f, size / 2f - 0.28f * width, width, size <= 24 ? 4 : 8);
        if (size >= 32) PowerBadge(g, size * 0.75f, size * 0.75f, size * 0.17f);
        return bmp;
    }

    public static Bitmap Wizard(int w, int h)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using (var bg = new LinearGradientBrush(new Point(0, 0), new Point(0, h), Card, Bg)) g.FillRectangle(bg, 0, 0, w, h);
        // Grille de pixels très discrète en fond.
        float cell = w / 20f;
        using (var dot = new SolidBrush(Color.FromArgb(7, 255, 255, 255)))
            for (float y = 0; y < h; y += cell) for (float x = (y / cell % 2) * cell; x < w; x += 2 * cell) g.FillRectangle(dot, x, y, cell, cell);
        Block(g, w / 2f, h * 0.25f, w * 0.60f, 8);
        PowerBadge(g, w * 0.70f, h * 0.36f, w * 0.12f);
        using var title = new Font("Segoe UI Semibold", w * 0.105f, GraphicsUnit.Pixel);
        using var sub = new Font("Segoe UI", w * 0.062f, GraphicsUnit.Pixel);
        using var white = new SolidBrush(Color.White);
        using var green = new SolidBrush(Accent);
        using var muted = new SolidBrush(ColorTranslator.FromHtml("#94A3B8"));
        var center = new StringFormat { Alignment = StringAlignment.Center };
        g.DrawString("Minecraft", title, white, new RectangleF(0, h * 0.56f, w, h * 0.1f), center);
        g.DrawString("Server Maker", title, green, new RectangleF(0, h * 0.56f + title.Height, w, h * 0.1f), center);
        g.DrawString("Ton serveur moddé\nen un clic", sub, muted, new RectangleF(0, h * 0.75f, w, h * 0.12f), center);
        // Bande d'herbe en pixels tout en bas.
        float px = w / 16f;
        var rnd = new Random(7);
        for (int i = 0; i < 16; i++)
        {
            float top = h - px * 2 - (rnd.Next(3) == 0 ? px / 2 : 0);
            using var grass = new SolidBrush(Grass[rnd.Next(Grass.Length)]);
            using var dirt = new SolidBrush(Dirt[rnd.Next(Dirt.Length)]);
            g.FillRectangle(grass, i * px, top, px, px);
            g.FillRectangle(dirt, i * px, top + px, px, h - top - px);
        }
        return bmp;
    }

    public static Bitmap Small(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.White); // le bandeau de l'installeur est blanc
        using var icon = Icon(size);
        g.DrawImage(icon, 0, 0, size, size);
        return bmp;
    }

    // Cube isométrique : dessus herbe, côtés terre avec une frange d'herbe, chaque face découpée en n×n pixels.
    static void Block(Graphics g, float cx, float cy, float width, int n)
    {
        float hw = width / 2, q = width / 4, side = width * 0.56f;
        var top = new PointF(cx, cy - q); var left = new PointF(cx - hw, cy); var right = new PointF(cx + hw, cy);
        var front = new PointF(cx, cy + q);
        var rnd = new Random(42);
        // Dessus : losange top-left-front-right.
        Face(g, top, Sub(right, top), Sub(left, top), n, (i, j) => Grass[rnd.Next(Grass.Length)], 1f);
        var down = new PointF(0, side);
        // Côté gauche (éclairé) et côté droit (dans l'ombre).
        Face(g, left, Sub(front, left), down, n, (i, j) => j < n / 4 + (rnd.Next(3) == 0 ? 1 : 0) ? Grass[rnd.Next(Grass.Length)] : Dirt[rnd.Next(Dirt.Length)], 0.82f);
        Face(g, front, Sub(right, front), down, n, (i, j) => j < n / 4 + (rnd.Next(3) == 0 ? 1 : 0) ? Grass[rnd.Next(Grass.Length)] : Dirt[rnd.Next(Dirt.Length)], 0.62f);
    }

    static void Face(Graphics g, PointF origin, PointF u, PointF v, int n, Func<int, int, Color> color, float light)
    {
        var old = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.None; // bords nets entre les pixels du bloc
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                PointF P(float a, float b) => new(origin.X + u.X * a / n + v.X * b / n, origin.Y + u.Y * a / n + v.Y * b / n);
                var c = color(i, j);
                using var brush = new SolidBrush(Color.FromArgb((int)(c.R * light), (int)(c.G * light), (int)(c.B * light)));
                g.FillPolygon(brush, [P(i, j), P(i + 1.04f, j), P(i + 1.04f, j + 1.04f), P(i, j + 1.04f)]);
            }
        g.SmoothingMode = old;
    }

    // Pastille verte avec le symbole marche/arrêt.
    static void PowerBadge(Graphics g, float cx, float cy, float r)
    {
        using (var halo = new SolidBrush(Color.FromArgb(70, Accent))) g.FillEllipse(halo, cx - r * 1.3f, cy - r * 1.3f, r * 2.6f, r * 2.6f);
        using (var ring = new SolidBrush(Bg)) g.FillEllipse(ring, cx - r * 1.12f, cy - r * 1.12f, r * 2.24f, r * 2.24f);
        using (var fill = new SolidBrush(Accent)) g.FillEllipse(fill, cx - r, cy - r, r * 2, r * 2);
        using var pen = new Pen(Bg, Math.Max(1.5f, r * 0.22f)) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float a = r * 0.52f;
        g.DrawArc(pen, cx - a, cy - a, a * 2, a * 2, -60, 300);
        g.DrawLine(pen, cx, cy - a * 1.25f, cx, cy - a * 0.15f);
    }

    static PointF Sub(PointF a, PointF b) => new(a.X - b.X, a.Y - b.Y);

    static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}
