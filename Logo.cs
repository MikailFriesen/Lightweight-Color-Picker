using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// Shared drawing code: the app logo + rounded-rectangle helper.
static class Logo
{
    public static GraphicsPath RoundRect(RectangleF r, float rad)
    {
        var p = new GraphicsPath();
        float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static Color Hsv(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        double c = v * s, x = c * (1 - Math.Abs((h / 60) % 2 - 1)), m = v - c, r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; }
        else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
        return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }

    public static Color Hsl(double h, double s, double l)
    {
        double v = l + s * Math.Min(l, 1 - l);
        return Hsv(h, v == 0 ? 0 : 2 * (1 - l / v), v);
    }

    // Shared neutral used by the app background, so the icon matches the window.
    public static readonly Color Tile = Hsl(214, 0.14, 0.14);

    public static void Draw(Graphics g, RectangleF bounds) { Draw(g, bounds, Tile); }

    // Dark rounded tile, hue ring, white crosshair in the middle.
    public static void Draw(Graphics g, RectangleF bounds, Color Tile)
    {
        var oldSmooth = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float s = bounds.Width, x0 = bounds.X, y0 = bounds.Y;

        using (var tile = RoundRect(bounds, s * 0.18f))
        using (var bg = new SolidBrush(Tile))
            g.FillPath(bg, tile);

        // hue ring (conic gradient built from thin slices)
        float pad = s * 0.13f, d = s - 2 * pad;
        var ring = new RectangleF(x0 + pad, y0 + pad, d, d);
        for (int a = 0; a < 360; a += 3)
            using (var b = new SolidBrush(Hsl(a, 0.70, 0.62)))
                g.FillPie(b, ring.X, ring.Y, ring.Width, ring.Height, a - 90, 4f);

        // inner disc
        float inner = d * 0.60f;
        var ir = new RectangleF(x0 + (s - inner) / 2, y0 + (s - inner) / 2, inner, inner);
        using (var b = new SolidBrush(Tile))
            g.FillEllipse(b, ir);

        // crosshair
        float cx = x0 + s / 2, cy = y0 + s / 2;
        float w = Math.Max(1.4f, s * 0.05f), gap = inner * 0.16f, len = inner * 0.24f;
        using (var p = new Pen(Color.White, w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawLine(p, cx, cy - gap - len, cx, cy - gap);
            g.DrawLine(p, cx, cy + gap, cx, cy + gap + len);
            g.DrawLine(p, cx - gap - len, cy, cx - gap, cy);
            g.DrawLine(p, cx + gap, cy, cx + gap + len, cy);
        }
        float dot = Math.Max(2f, inner * 0.13f);
        using (var b = new SolidBrush(Color.White))
            g.FillEllipse(b, cx - dot / 2, cy - dot / 2, dot, dot);

        g.SmoothingMode = oldSmooth;
    }

    public static Bitmap Render(int size) { return Render(size, Tile); }

    public static Bitmap Render(int size, Color tile)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            Draw(g, new RectangleF(0, 0, size, size), tile);
        }
        return bmp;
    }
}

