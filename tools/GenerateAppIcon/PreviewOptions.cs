using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GenerateAppIcon;

internal static class PreviewOptions
{
    private static readonly Color Fill = Color.FromArgb(255, 242, 242, 240);
    private static readonly Color Ink = Color.FromArgb(255, 28, 28, 28);

    public static void Run(string root)
    {
        var assets = Path.Combine(root, "assets", "options");
        Directory.CreateDirectory(assets);

        var media = ResolveMediaDir();
        if (media is not null)
            Directory.CreateDirectory(media);

        var options = new (string Id, Func<int, Bitmap> Draw)[]
        {
            ("rocket", DrawRocket),
            ("q", DrawQ),
            ("grid", DrawGrid),
            ("chevron", DrawChevron),
            ("menucard", DrawMenuCard),
        };

        int[] sizes = [16, 32, 256];
        foreach (var (id, draw) in options)
        {
            foreach (var size in sizes)
            {
                using var bmp = draw(size);
                var name = $"qsm-opt-{id}-{size}.png";
                var assetPath = Path.Combine(assets, name);
                bmp.Save(assetPath, ImageFormat.Png);
                Console.WriteLine($"Wrote {assetPath}");
                if (media is not null)
                {
                    var mediaPath = Path.Combine(media, name);
                    File.Copy(assetPath, mediaPath, overwrite: true);
                    Console.WriteLine($"Wrote {mediaPath}");
                }
            }
        }
    }

    private static string? ResolveMediaDir()
    {
        var env = Environment.GetEnvironmentVariable("CURSOR_AGENT_STORE_FILES_DIR");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var peer = Path.GetFullPath(Path.Combine(env, "..", "..", "bc-720fbb48-c750-4a47-8416-e7e45586a512", "files", "media"));
            // Prefer explicit project store path used in this session.
        }

        var projectMedia = @"C:\Users\donha\AppData\Local\Cursor\AgentStores\cursor_agent_stores\bc-720fbb48-c750-4a47-8416-e7e45586a512\files\media";
        return Directory.Exists(Path.GetDirectoryName(projectMedia)!) ? projectMedia : null;
    }

    private static Bitmap Canvas(int size, Action<Graphics, int> paint)
    {
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

        paint(g, size);
        return bmp;
    }

    private static void Plate(Graphics g, int size, out RectangleF bounds)
    {
        float pad = Math.Max(1f, size * 0.08f);
        bounds = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
        float radius = Math.Max(1f, size * 0.18f);
        using var path = RoundedRect(bounds, radius);
        using var brush = new SolidBrush(Fill);
        using var pen = new Pen(Ink, Math.Max(1f, size / 32f));
        g.FillPath(brush, path);
        if (size >= 20)
            g.DrawPath(pen, path);
    }

    // 1) Rocket / launch mark — body + fins + flame
    private static Bitmap DrawRocket(int size) => Canvas(size, (g, s) =>
    {
        Plate(g, s, out var b);
        using var ink = new SolidBrush(Ink);
        float cx = b.X + b.Width / 2f;
        float top = b.Y + b.Height * 0.14f;
        float mid = b.Y + b.Height * 0.55f;
        float bottom = b.Y + b.Height * 0.72f;
        float bodyW = b.Width * 0.28f;

        // Nose + body
        using (var body = new GraphicsPath())
        {
            body.AddPolygon(new[]
            {
                new PointF(cx, top),
                new PointF(cx + bodyW / 2f, mid * 0.55f + top * 0.45f),
                new PointF(cx + bodyW / 2f, bottom),
                new PointF(cx - bodyW / 2f, bottom),
                new PointF(cx - bodyW / 2f, mid * 0.55f + top * 0.45f),
            });
            g.FillPath(ink, body);
        }

        // Fins
        float fin = b.Width * 0.16f;
        g.FillPolygon(ink, new[]
        {
            new PointF(cx - bodyW / 2f, bottom - b.Height * 0.08f),
            new PointF(cx - bodyW / 2f - fin, bottom + b.Height * 0.06f),
            new PointF(cx - bodyW / 2f, bottom),
        });
        g.FillPolygon(ink, new[]
        {
            new PointF(cx + bodyW / 2f, bottom - b.Height * 0.08f),
            new PointF(cx + bodyW / 2f + fin, bottom + b.Height * 0.06f),
            new PointF(cx + bodyW / 2f, bottom),
        });

        // Flame (cutout-style by drawing fill then smaller flame in fill color on plate — use ink chevron)
        float flameTop = bottom + b.Height * 0.02f;
        float flameBot = b.Bottom - b.Height * 0.08f;
        g.FillPolygon(ink, new[]
        {
            new PointF(cx, flameBot),
            new PointF(cx - bodyW * 0.35f, flameTop),
            new PointF(cx + bodyW * 0.35f, flameTop),
        });

        // Window (hole) punched with plate color
        float winR = Math.Max(1f, b.Width * 0.07f);
        using var hole = new SolidBrush(Fill);
        g.FillEllipse(hole, cx - winR, top + b.Height * 0.22f - winR, winR * 2f, winR * 2f);
    });

    // 2) Stylized Q
    private static Bitmap DrawQ(int size) => Canvas(size, (g, s) =>
    {
        Plate(g, s, out var b);
        float thick = Math.Max(2f, s * 0.12f);
        using var pen = new Pen(Ink, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float inset = b.Width * 0.18f;
        var ring = new RectangleF(b.X + inset, b.Y + inset * 0.9f, b.Width - 2 * inset, b.Height - 2.2f * inset);
        g.DrawEllipse(pen, ring);
        // Tail of Q
        float tx0 = b.X + b.Width * 0.58f;
        float ty0 = b.Y + b.Height * 0.58f;
        float tx1 = b.X + b.Width * 0.78f;
        float ty1 = b.Y + b.Height * 0.78f;
        g.DrawLine(pen, tx0, ty0, tx1, ty1);
    });

    // 3) 2×2 grid / tiles (distinct from 3-bar menu)
    private static Bitmap DrawGrid(int size) => Canvas(size, (g, s) =>
    {
        Plate(g, s, out var b);
        using var ink = new SolidBrush(Ink);
        float margin = b.Width * 0.18f;
        float gap = Math.Max(1f, b.Width * 0.10f);
        float tile = (b.Width - 2 * margin - gap) / 2f;
        float x0 = b.X + margin;
        float y0 = b.Y + margin;
        float r = Math.Max(0.5f, tile * 0.18f);
        for (var row = 0; row < 2; row++)
        for (var col = 0; col < 2; col++)
        {
            var rect = new RectangleF(x0 + col * (tile + gap), y0 + row * (tile + gap), tile, tile);
            using var path = RoundedRect(rect, r);
            g.FillPath(ink, path);
        }
    });

    // 4) Play / chevron-in-square (launch)
    private static Bitmap DrawChevron(int size) => Canvas(size, (g, s) =>
    {
        Plate(g, s, out var b);
        using var ink = new SolidBrush(Ink);
        float left = b.X + b.Width * 0.32f;
        float right = b.X + b.Width * 0.72f;
        float top = b.Y + b.Height * 0.22f;
        float bot = b.Y + b.Height * 0.78f;
        float mid = b.Y + b.Height * 0.50f;
        g.FillPolygon(ink, new[]
        {
            new PointF(left, top),
            new PointF(right, mid),
            new PointF(left, bot),
        });
    });

    // 5) Refined current menu-card (contrast reference)
    private static Bitmap DrawMenuCard(int size) => Canvas(size, (g, s) =>
    {
        Plate(g, s, out var b);
        using var ink = new SolidBrush(Ink);
        float barLeft = b.X + b.Width * 0.18f;
        float barW = b.Width * 0.64f;
        float barH = Math.Max(1f, size <= 16 ? 2f : s * 0.10f);
        float gap = Math.Max(1f, size <= 16 ? 2f : s * 0.10f);
        float total = 3 * barH + 2 * gap;
        float barTop = b.Y + (b.Height - total) / 2f;
        float[] widths = [barW, barW, barW * 0.72f];
        for (var i = 0; i < 3; i++)
            g.FillRectangle(ink, barLeft, barTop + i * (barH + gap), widths[i], barH);
    });

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
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
}
