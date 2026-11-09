using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

/// <summary>
/// Windows 11 Start logo: 2×2 Fluent blue tiles, diagonal light→dark gradient,
/// thin gutters, subtle rounded corners. Transparent backdrop (no plate).
/// </summary>
static Bitmap DrawStartGrid(int size)
{
    // Sampled / Fluent Win11 Start blues (light cyan-blue → Microsoft blue).
    var light = Color.FromArgb(255, 0x3C, 0xBE, 0xF5); // ~#3CBEF5
    var dark = Color.FromArgb(255, 0x00, 0x78, 0xD4);  // ~#0078D4

    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.Clear(Color.Transparent);
    g.SmoothingMode = size >= 32 ? SmoothingMode.AntiAlias : SmoothingMode.None;
    g.PixelOffsetMode = size >= 32 ? PixelOffsetMode.HighQuality : PixelOffsetMode.None;

    // Fill most of the canvas; thin gutters (~4% like the reference, min 1–2px).
    int pad = size <= 16 ? 1 : Math.Max(1, (int)Math.Round(size * 0.04f));
    int gap = size <= 16 ? 1 : (size <= 32 ? 2 : Math.Max(2, (int)Math.Round(size * 0.045f)));
    int inner = size - 2 * pad;
    int tile = (inner - gap) / 2;
    if (tile < 1) tile = 1;
    int used = 2 * tile + gap;
    int x0 = pad + Math.Max(0, (inner - used) / 2);
    int y0 = pad + Math.Max(0, (inner - used) / 2);
    // Subtle Fluent rounding — nearly square at 16px, soft at larger sizes.
    float tileRadius = size <= 16 ? 1f : Math.Max(1f, tile * 0.14f);

    // One diagonal gradient across the whole 2×2 so tiles read as one Fluent window.
    var gradBounds = new RectangleF(x0, y0, used, used);
    using var gradient = new LinearGradientBrush(
        gradBounds,
        light,
        dark,
        LinearGradientMode.ForwardDiagonal)
    {
        WrapMode = WrapMode.TileFlipXY,
    };

    for (var row = 0; row < 2; row++)
    for (var col = 0; col < 2; col++)
    {
        var rect = new RectangleF(
            x0 + col * (tile + gap),
            y0 + row * (tile + gap),
            tile,
            tile);
        using var tilePath = RoundedRect(rect, tileRadius);
        g.FillPath(gradient, tilePath);
    }

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

// Preview-only: Start grid for Don's approval — does NOT touch Resources\app.ico.
if (args.Any(a => string.Equals(a, "--preview-startgrid", StringComparison.OrdinalIgnoreCase)))
{
    var optDir = Path.Combine(assets, "options");
    Directory.CreateDirectory(optDir);
    foreach (var size in new[] { 16, 32, 256 })
    {
        using var bmp = DrawStartGrid(size);
        var name = $"qsm-opt-startgrid-{size}.png";
        var assetPath = Path.Combine(optDir, name);
        bmp.Save(assetPath, ImageFormat.Png);
        var mediaPath = Path.Combine(media, name);
        File.Copy(assetPath, mediaPath, overwrite: true);
        Console.WriteLine($"Wrote {mediaPath}");
    }

    Console.WriteLine("Preview only — Resources\\app.ico unchanged.");
    return;
}

// Shipped icon (Don-approved): Win11 Start 4-tile Fluent grid.
int[] sizes = [16, 20, 24, 32, 48, 256];
var bitmaps = sizes.Select(DrawStartGrid).ToList();
try
{
    for (var i = 0; i < sizes.Length; i++)
    {
        var png = Path.Combine(assets, $"app-icon-{sizes[i]}.png");
        bitmaps[i].Save(png, ImageFormat.Png);
        Console.WriteLine($"Wrote {png}");
    }

    var optDir = Path.Combine(assets, "options");
    Directory.CreateDirectory(optDir);
    foreach (var size in new[] { 16, 32, 256 })
    {
        var idx = Array.IndexOf(sizes, size);
        var name = $"qsm-opt-startgrid-{size}.png";
        var assetOpt = Path.Combine(optDir, name);
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
