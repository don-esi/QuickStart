using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static Bitmap Draw(int size)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.Clear(Color.Transparent);

    var fill = Color.FromArgb(255, 242, 242, 240);
    var ink = Color.FromArgb(255, 28, 28, 28);

    if (size <= 24)
    {
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.None;
        DrawSmall(g, size, fill, ink);
        return bmp;
    }

    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;

    float pad = size * 0.08f;
    float radius = size * 0.18f;
    var bounds = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);

    using (var path = RoundedRect(bounds, radius))
    using (var brush = new SolidBrush(fill))
    using (var edge = new Pen(ink, Math.Max(1f, size / 32f)))
    {
        g.FillPath(brush, path);
        g.DrawPath(edge, path);
    }

    float barLeft = bounds.X + bounds.Width * 0.18f;
    float barWidth = bounds.Width * 0.64f;
    float barH = Math.Max(2f, size * 0.10f);
    float gap = Math.Max(2f, size * 0.10f);
    float total = 3 * barH + 2 * gap;
    float barTop = bounds.Y + (bounds.Height - total) / 2f;
    float[] widths = [barWidth, barWidth, barWidth * 0.72f];

    using var barBrush = new SolidBrush(ink);
    for (var i = 0; i < 3; i++)
        g.FillRectangle(barBrush, barLeft, barTop + i * (barH + gap), widths[i], barH);

    return bmp;
}

static void DrawSmall(Graphics g, int size, Color fill, Color ink)
{
    // Integer grid for 16/20/24 — light plate + 3 dark bars + 1px outline.
    var pad = size <= 16 ? 1 : 2;
    var body = size - 2 * pad;
    using (var brush = new SolidBrush(fill))
        g.FillRectangle(brush, pad, pad, body, body);

    var barX = pad + (size <= 16 ? 2 : 3);
    var barW = body - (size <= 16 ? 4 : 6);
    var barH = 2;
    var gap = size <= 16 ? 2 : 3;
    var total = 3 * barH + 2 * gap;
    var barY = pad + (body - total) / 2;

    using var inkBrush = new SolidBrush(ink);
    g.FillRectangle(inkBrush, barX, barY, barW, barH);
    g.FillRectangle(inkBrush, barX, barY + barH + gap, barW, barH);
    g.FillRectangle(inkBrush, barX, barY + 2 * (barH + gap), (int)(barW * 0.7), barH);

    using var pen = new Pen(ink);
    g.DrawRectangle(pen, pad, pad, body - 1, body - 1);
}

static GraphicsPath RoundedRect(RectangleF r, float radius)
{
    var d = radius * 2f;
    var path = new GraphicsPath();
    path.AddArc(r.X, r.Y, d, d, 180, 90);
    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}

static void SaveIco(IReadOnlyList<Bitmap> images, string path)
{
    using var fs = File.Create(path);
    using var bw = new BinaryWriter(fs);
    bw.Write((ushort)0);
    bw.Write((ushort)1);
    bw.Write((ushort)images.Count);

    var pngs = new List<byte[]>();
    foreach (var img in images)
    {
        using var ms = new MemoryStream();
        img.Save(ms, ImageFormat.Png);
        pngs.Add(ms.ToArray());
    }

    var offset = 6 + 16 * images.Count;
    for (var i = 0; i < images.Count; i++)
    {
        bw.Write((byte)(images[i].Width >= 256 ? 0 : images[i].Width));
        bw.Write((byte)(images[i].Height >= 256 ? 0 : images[i].Height));
        bw.Write((byte)0);
        bw.Write((byte)0);
        bw.Write((ushort)1);
        bw.Write((ushort)32);
        bw.Write(pngs[i].Length);
        bw.Write(offset);
        offset += pngs[i].Length;
    }

    foreach (var png in pngs)
        bw.Write(png);
}

var root = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var assets = Path.Combine(root, "assets");
var resources = Path.Combine(root, "Resources");
Directory.CreateDirectory(assets);
Directory.CreateDirectory(resources);

int[] sizes = [16, 20, 24, 32, 48, 256];
var bitmaps = sizes.Select(Draw).ToList();
try
{
    for (var i = 0; i < sizes.Length; i++)
        bitmaps[i].Save(Path.Combine(assets, $"app-icon-{sizes[i]}.png"), ImageFormat.Png);

    var icoPath = Path.Combine(resources, "app.ico");
    SaveIco(bitmaps, icoPath);
    Console.WriteLine($"Wrote {icoPath}");
}
finally
{
    foreach (var b in bitmaps)
        b.Dispose();
}
