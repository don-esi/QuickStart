using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static Bitmap DrawRocketYellow(int size)
{
    // Windows accent gold (#FFB900) on near-black plate — readable on dark trays.
    var winGold = Color.FromArgb(255, 255, 185, 0);
    var winGoldDeep = Color.FromArgb(255, 230, 160, 0);
    var plate = Color.FromArgb(255, 32, 32, 32);
    var plateEdge = Color.FromArgb(255, 64, 64, 64);

    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.Clear(Color.Transparent);
    if (size >= 32)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }
    else
    {
        g.SmoothingMode = SmoothingMode.None;
        g.PixelOffsetMode = PixelOffsetMode.None;
    }

    float pad = Math.Max(1f, size * 0.06f);
    var bounds = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
    float radius = Math.Max(1f, size * 0.18f);

    using (var path = RoundedRect(bounds, radius))
    using (var fill = new SolidBrush(plate))
    using (var edge = new Pen(plateEdge, Math.Max(1f, size / 32f)))
    {
        g.FillPath(fill, path);
        if (size >= 20)
            g.DrawPath(edge, path);
    }

    using var gold = new SolidBrush(winGold);
    using var goldDeep = new SolidBrush(winGoldDeep);

    float cx = bounds.X + bounds.Width / 2f;
    float top = bounds.Y + bounds.Height * 0.12f;
    float midY = bounds.Y + bounds.Height * 0.42f;
    float bottom = bounds.Y + bounds.Height * 0.68f;
    float bodyW = Math.Max(2f, bounds.Width * 0.30f);

    // Body + nose
    using (var body = new GraphicsPath())
    {
        body.AddPolygon(new[]
        {
            new PointF(cx, top),
            new PointF(cx + bodyW / 2f, midY),
            new PointF(cx + bodyW / 2f, bottom),
            new PointF(cx - bodyW / 2f, bottom),
            new PointF(cx - bodyW / 2f, midY),
        });
        g.FillPath(gold, body);
    }

    // Fins
    float fin = bounds.Width * 0.16f;
    g.FillPolygon(gold, new[]
    {
        new PointF(cx - bodyW / 2f, bottom - bounds.Height * 0.08f),
        new PointF(cx - bodyW / 2f - fin, bottom + bounds.Height * 0.05f),
        new PointF(cx - bodyW / 2f, bottom),
    });
    g.FillPolygon(gold, new[]
    {
        new PointF(cx + bodyW / 2f, bottom - bounds.Height * 0.08f),
        new PointF(cx + bodyW / 2f + fin, bottom + bounds.Height * 0.05f),
        new PointF(cx + bodyW / 2f, bottom),
    });

    // Flame (deeper gold)
    float flameTop = bottom + bounds.Height * 0.02f;
    float flameBot = bounds.Bottom - bounds.Height * 0.08f;
    g.FillPolygon(goldDeep, new[]
    {
        new PointF(cx, flameBot),
        new PointF(cx - bodyW * 0.38f, flameTop),
        new PointF(cx + bodyW * 0.38f, flameTop),
    });

    // Porthole (plate-colored cutout)
    float winR = Math.Max(1f, bounds.Width * 0.07f);
    using var hole = new SolidBrush(plate);
    g.FillEllipse(hole, cx - winR, top + bounds.Height * 0.20f - winR, winR * 2f, winR * 2f);

    return bmp;
}

static GraphicsPath RoundedRect(RectangleF r, float radius)
{
    var d = Math.Max(0.1f, radius * 2f);
    var path = new GraphicsPath();
    if (d >= r.Width || d >= r.Height)
    {
        path.AddRectangle(r);
        path.CloseFigure();
        return path;
    }

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

var rootArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal))
    ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

if (args.Any(a => string.Equals(a, "--options", StringComparison.OrdinalIgnoreCase)))
{
    GenerateAppIcon.PreviewOptions.Run(rootArg);
    return;
}

var assets = Path.Combine(rootArg, "assets");
var resources = Path.Combine(rootArg, "Resources");
var media = @"C:\Users\donha\AppData\Local\Cursor\AgentStores\cursor_agent_stores\bc-720fbb48-c750-4a47-8416-e7e45586a512\files\media";
Directory.CreateDirectory(assets);
Directory.CreateDirectory(resources);
Directory.CreateDirectory(media);

int[] sizes = [16, 20, 24, 32, 48, 256];
var bitmaps = sizes.Select(DrawRocketYellow).ToList();
try
{
    for (var i = 0; i < sizes.Length; i++)
    {
        var png = Path.Combine(assets, $"app-icon-{sizes[i]}.png");
        bitmaps[i].Save(png, ImageFormat.Png);
        Console.WriteLine($"Wrote {png}");
    }

    // Don-facing yellow rocket previews
    foreach (var size in new[] { 32, 256 })
    {
        var idx = Array.IndexOf(sizes, size);
        var name = $"qsm-opt-rocket-yellow-{size}.png";
        var assetOpt = Path.Combine(assets, "options", name);
        Directory.CreateDirectory(Path.GetDirectoryName(assetOpt)!);
        bitmaps[idx].Save(assetOpt, ImageFormat.Png);
        var mediaPath = Path.Combine(media, name);
        File.Copy(assetOpt, mediaPath, overwrite: true);
        Console.WriteLine($"Wrote {mediaPath}");
    }

    var icoPath = Path.Combine(resources, "app.ico");
    SaveIco(bitmaps, icoPath);
    Console.WriteLine($"Wrote {icoPath}");
}
finally
{
    foreach (var b in bitmaps)
        b.Dispose();
}
