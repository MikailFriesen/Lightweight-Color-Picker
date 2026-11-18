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

    // Dark rounded tile, glowing hue ring, white crosshair in the middle.
    public static void Draw(Graphics g, RectangleF bounds)
    {
        var oldSmooth = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        float s = bounds.Width, x0 = bounds.X, y0 = bounds.Y;

        using (var tile = RoundRect(bounds, s * 0.24f))
        using (var bg = new LinearGradientBrush(bounds, Color.FromArgb(40, 38, 62), Color.FromArgb(12, 12, 20), 45f))
            g.FillPath(bg, tile);

        // hue ring (conic gradient built from thin slices)
        float pad = s * 0.13f, d = s - 2 * pad;
        var ring = new RectangleF(x0 + pad, y0 + pad, d, d);
        for (int a = 0; a < 360; a += 3)
            using (var b = new SolidBrush(Hsv(a + 200, 0.78, 1.0)))
                g.FillPie(b, ring.X, ring.Y, ring.Width, ring.Height, a - 90, 4f);

        // inner disc
        float inner = d * 0.60f;
        var ir = new RectangleF(x0 + (s - inner) / 2, y0 + (s - inner) / 2, inner, inner);
        using (var b = new LinearGradientBrush(ir, Color.FromArgb(34, 33, 52), Color.FromArgb(16, 16, 26), 90f))
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

    public static Bitmap Render(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            Draw(g, new RectangleF(0, 0, size, size));
        }
        return bmp;
    }
}
