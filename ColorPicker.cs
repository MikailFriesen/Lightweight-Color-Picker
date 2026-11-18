using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

static class Native
{
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern uint GetPixel(IntPtr hdc, int x, int y);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint key, byte alpha, uint flags);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    public static Color ColorAt(int x, int y)
    {
        IntPtr dc = GetDC(IntPtr.Zero);
        uint px = GetPixel(dc, x, y);
        ReleaseDC(IntPtr.Zero, dc);
        return Color.FromArgb((int)(px & 0xFF), (int)((px >> 8) & 0xFF), (int)((px >> 16) & 0xFF));
    }
}

static class Theme
{
    public static readonly Color Bg      = Color.FromArgb(17, 17, 27);
    public static readonly Color Panel   = Color.FromArgb(30, 30, 46);
    public static readonly Color PanelHi = Color.FromArgb(49, 50, 68);
    public static readonly Color Border  = Color.FromArgb(58, 58, 82);
    public static readonly Color Text    = Color.FromArgb(205, 214, 244);
    public static readonly Color Muted   = Color.FromArgb(127, 132, 156);
    public static readonly Color Blue    = Color.FromArgb(137, 180, 250);
    public static readonly Color Mauve   = Color.FromArgb(203, 166, 247);
    public static readonly Color Pink    = Color.FromArgb(243, 139, 168);
    public static readonly Color Peach   = Color.FromArgb(250, 179, 135);
    public static readonly Color Green   = Color.FromArgb(166, 227, 161);

    public static Color Contrast(Color c) { return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 > 150 ? Bg : Color.White; }
    public static Color Lighten(Color c, int a) { return Color.FromArgb(Math.Min(255, c.R + a), Math.Min(255, c.G + a), Math.Min(255, c.B + a)); }
    public static string Hex(Color c) { return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }

    public static void Smooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }
}

abstract class Painted : Control
{
    protected Painted()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
    }
}

// Gradient "Pick color" button
class GradButton : Painted
{
    public bool Active;
    bool hover, down;
    public GradButton() { Cursor = Cursors.Hand; }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); Theme.Smooth(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        Color a = Active ? Theme.Pink : Theme.Blue, b = Active ? Theme.Peach : Theme.Mauve;
        int lift = down ? -15 : hover ? 18 : 0;
        if (lift > 0) { a = Theme.Lighten(a, lift); b = Theme.Lighten(b, lift); }
        using (var p = Logo.RoundRect(r, 11))
        using (var br = new LinearGradientBrush(r, a, b, 0f))
            g.FillPath(br, p);
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Theme.Bg,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// Big preview of the picked color
class Swatch : Painted
{
    public Color? C;
    public string Hint = "";
    public Font Big;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); Theme.Smooth(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
        using (var p = Logo.RoundRect(r, 12))
        {
            if (C == null)
            {
                using (var b = new SolidBrush(Theme.Panel)) g.FillPath(b, p);
                using (var pen = new Pen(Theme.Border, 1.5f) { DashStyle = DashStyle.Dash }) g.DrawPath(pen, p);
                TextRenderer.DrawText(g, Hint, Font, ClientRectangle, Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            Color c = C.Value, fg = Theme.Contrast(c);
            using (var b = new SolidBrush(c)) g.FillPath(b, p);
            using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawPath(pen, p);
            TextRenderer.DrawText(g, Theme.Hex(c), Big, new Point(16, 12), fg);
            TextRenderer.DrawText(g, string.Format("rgb({0}, {1}, {2})", c.R, c.G, c.B), Font, new Point(18, Height - 30),
                Color.FromArgb(200, fg));
        }
    }
}

// One output format: auto-copy dot, label, selectable value, copy button
class FormatRow : Painted
{
    public string Label;
    public bool Auto;
    public readonly TextBox Box;
    public event EventHandler CopyClick, AutoClick;
    bool hoverCopy, hoverDot, copied;
    readonly Timer reset = new Timer { Interval = 1300 };

    Rectangle DotRect  { get { return new Rectangle(12, (Height - 14) / 2, 14, 14); } }
    Rectangle CopyRect { get { return new Rectangle(Width - 76, 5, 70, Height - 10); } }

    public FormatRow()
    {
        Height = 36;
        Box = new TextBox { BorderStyle = BorderStyle.None, ReadOnly = true, BackColor = Theme.Panel,
                            ForeColor = Theme.Text, Font = new Font("Consolas", 10.5f), TabStop = false };
        Controls.Add(Box);
        reset.Tick += delegate { reset.Stop(); copied = false; Invalidate(); };
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (Box == null) return;
        Box.Location = new Point(112, (Height - Box.Height) / 2 + 1);
        Box.Width = Width - 112 - 84;
    }

    public void Flash() { copied = true; Invalidate(); reset.Stop(); reset.Start(); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool c = CopyRect.Contains(e.Location), d = Rectangle.Inflate(DotRect, 6, 6).Contains(e.Location);
        if (c != hoverCopy || d != hoverDot) { hoverCopy = c; hoverDot = d; Cursor = (c || d) ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hoverCopy = hoverDot = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (CopyRect.Contains(e.Location) && CopyClick != null) CopyClick(this, e);
        else if (Rectangle.Inflate(DotRect, 6, 6).Contains(e.Location) && AutoClick != null) AutoClick(this, e);
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); Theme.Smooth(g);
        using (var p = Logo.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 9))
        using (var b = new SolidBrush(Theme.Panel))
            g.FillPath(b, p);

        var dr = DotRect;
        if (Auto)
            using (var b = new LinearGradientBrush(dr, Theme.Blue, Theme.Mauve, 45f)) g.FillEllipse(b, dr);
        else
            using (var pen = new Pen(hoverDot ? Theme.Text : Theme.Border, 1.5f)) g.DrawEllipse(pen, dr);

        TextRenderer.DrawText(g, Label, Font, new Rectangle(34, 0, 78, Height), Auto ? Theme.Text : Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left);

        var cr = CopyRect;
        using (var p = Logo.RoundRect(cr, 7))
        using (var b = new SolidBrush(hoverCopy ? Theme.Lighten(Theme.PanelHi, 14) : Theme.PanelHi))
            g.FillPath(b, p);
        TextRenderer.DrawText(g, copied ? "Copied ✓" : "Copy", Font, cr, copied ? Theme.Green : Theme.Text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// Row of recently picked colors
class Chips : Painted
{
    public readonly List<Color> Items = new List<Color>();
    public event Action<Color> ChipClick;
    int hover = -1;
    const int S = 28, G = 6, Max = 10;

    Rectangle ChipRect(int i) { return new Rectangle(1 + i * (S + G), (Height - S) / 2, S, S); }

    public void Add(Color c) { Items.Insert(0, c); if (Items.Count > Max) Items.RemoveAt(Max); Invalidate(); }

    int Hit(Point p) { for (int i = 0; i < Items.Count; i++) if (ChipRect(i).Contains(p)) return i; return -1; }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        int h = Hit(e.Location);
        if (h >= 0 && ChipClick != null) ChipClick(Items[h]);
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); Theme.Smooth(g);
        if (Items.Count == 0)
        {
            TextRenderer.DrawText(g, "Your picks will show up here", Font, ClientRectangle, Theme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            return;
        }
        for (int i = 0; i < Items.Count; i++)
        {
            var r = ChipRect(i);
            using (var p = Logo.RoundRect(r, 8))
            {
                using (var b = new SolidBrush(Items[i])) g.FillPath(b, p);
                using (var pen = new Pen(i == hover ? Theme.Text : Theme.Border, i == hover ? 2f : 1f)) g.DrawPath(pen, p);
            }
        }
    }
}

// Magnifier that follows the cursor while picking
class Loupe : Form
{
    const int N = 11, Z = 12, Pad = 8, Info = 34;
    readonly Bitmap cap = new Bitmap(N, N, PixelFormat.Format32bppRgb);
    Color center;
    readonly Font mono = new Font("Consolas", 9.5f);

    public Loupe()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Panel;
        DoubleBuffered = true;
        ClientSize = new Size(N * Z + Pad * 2, N * Z + Pad * 2 + Info);
        using (var p = Logo.RoundRect(new RectangleF(0, 0, Width, Height), 12)) Region = new Region(p);
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            // toolwindow | noactivate | click-through (transparent + layered) | topmost
            cp.ExStyle |= 0x80 | 0x08000000 | 0x20 | 0x80000 | 0x8;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetLayeredWindowAttributes(Handle, 0, 250, 2);
        try { Native.SetWindowDisplayAffinity(Handle, 0x11); } catch { } // keep the loupe out of its own screen capture
    }

    public void Track(Point p)
    {
        using (var g = Graphics.FromImage(cap))
        {
            g.Clear(Color.Black);
            try { g.CopyFromScreen(p.X - N / 2, p.Y - N / 2, 0, 0, new Size(N, N)); } catch { }
        }
        center = Native.ColorAt(p.X, p.Y);

        var wa = Screen.FromPoint(p).WorkingArea;
        int x = p.X + 24, y = p.Y + 24;
        if (x + Width > wa.Right) x = p.X - 24 - Width;
        if (y + Height > wa.Bottom) y = p.Y - 24 - Height;
        if (Location.X != x || Location.Y != y) Location = new Point(x, y);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Panel);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        var area = new Rectangle(Pad, Pad, N * Z, N * Z);
        g.DrawImage(cap, area);
        g.PixelOffsetMode = PixelOffsetMode.Default;

        using (var grid = new Pen(Color.FromArgb(35, 0, 0, 0)))
            for (int i = 1; i < N; i++)
            {
                g.DrawLine(grid, Pad + i * Z, Pad, Pad + i * Z, Pad + N * Z);
                g.DrawLine(grid, Pad, Pad + i * Z, Pad + N * Z, Pad + i * Z);
            }

        var mid = new Rectangle(Pad + (N / 2) * Z, Pad + (N / 2) * Z, Z, Z);
        using (var outer = new Pen(Color.Black, 1)) g.DrawRectangle(outer, Rectangle.Inflate(mid, 1, 1));
        using (var inner = new Pen(Color.White, 1)) g.DrawRectangle(inner, mid);

        Theme.Smooth(g);
        int iy = Pad * 2 + N * Z;
        var chip = new RectangleF(Pad, iy + 4, 22, 22);
        using (var p = Logo.RoundRect(chip, 6))
        {
            using (var b = new SolidBrush(center)) g.FillPath(b, p);
            using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
        }
        TextRenderer.DrawText(g, Theme.Hex(center), mono, new Point(Pad + 28, iy + 1), Theme.Text);
        TextRenderer.DrawText(g, string.Format("{0}, {1}, {2}", center.R, center.G, center.B), mono,
            new Point(Pad + 28, iy + 16), Theme.Muted);

        using (var pen = new Pen(Theme.Border, 1.5f))
        using (var p = Logo.RoundRect(new RectangleF(0.75f, 0.75f, Width - 1.5f, Height - 1.5f), 12))
            g.DrawPath(pen, p);
    }
}

class ColorPicker : Form
{
    const int WH_MOUSE_LL = 14, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204;
    const int WM_HOTKEY = 0x0312, HOTKEY_ID = 1;
    const uint MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

    static readonly string[] FormatNames = { "Red", "Green", "Blue", "R, G, B", "rgb()", "Hex", "Hex (no #)" };
    static string Format(int i, Color c)
    {
        switch (i)
        {
            case 0: return c.R.ToString();
            case 1: return c.G.ToString();
            case 2: return c.B.ToString();
            case 3: return string.Format("{0}, {1}, {2}", c.R, c.G, c.B);
            case 4: return string.Format("rgb({0}, {1}, {2})", c.R, c.G, c.B);
            case 5: return Theme.Hex(c);
            default: return Theme.Hex(c).Substring(1);
        }
    }

    readonly Native.HookProc hookProc;
    IntPtr hook = IntPtr.Zero;
    bool swallowUp, hotkeyOk;
    readonly Timer timer = new Timer { Interval = 15 };
    readonly GradButton pickBtn;
    readonly Swatch swatch;
    readonly FormatRow[] rows = new FormatRow[FormatNames.Length];
    readonly Chips chips;
    readonly Loupe loupe = new Loupe();
    readonly Font titleFont = new Font("Segoe UI Semibold", 14f);
    readonly Font smallFont = new Font("Segoe UI", 8.25f, FontStyle.Bold);
    int autoIdx = 4;
    string subtitle = "Ctrl+Shift+P to pick  ·  Esc to cancel";
    int formatsY, historyY;

    ColorPicker()
    {
        Text = "Color Picker";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        TopMost = true;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = new Font("Segoe UI", 9.75f);
        DoubleBuffered = true;
        KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

        const int X = 20, W = 370;
        int y = 80;

        pickBtn = new GradButton { Location = new Point(X, y), Size = new Size(W, 46), Font = new Font("Segoe UI Semibold", 10.5f) };
        pickBtn.Click += delegate { if (hook == IntPtr.Zero) StartPicking(); else StopPicking(); };
        y += 58;

        swatch = new Swatch { Location = new Point(X, y), Size = new Size(W, 88), Big = new Font("Segoe UI Semibold", 20f),
                              Hint = "Nothing picked yet" };
        y += 104;

        formatsY = y; y += 24;
        var tip = new ToolTip();
        for (int i = 0; i < rows.Length; i++)
        {
            int idx = i;
            var row = new FormatRow { Label = FormatNames[i], Location = new Point(X, y), Width = W, Auto = i == autoIdx };
            row.CopyClick += delegate { if (swatch.C != null) { CopyText(Format(idx, swatch.C.Value)); row.Flash(); } };
            row.AutoClick += delegate { SetAuto(idx); };
            tip.SetToolTip(row, "Click the dot to auto-copy this format after each pick");
            rows[i] = row;
            Controls.Add(row);
            y += 40;
        }
        y += 10;

        historyY = y; y += 24;
        chips = new Chips { Location = new Point(X, y), Size = new Size(W, 34) };
        chips.ChipClick += Display;
        y += 34 + 20;

        Controls.Add(pickBtn);
        Controls.Add(swatch);
        Controls.Add(chips);
        ClientSize = new Size(X * 2 + W, y);

        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) StopPicking(); };

        timer.Tick += delegate {
            if ((Native.GetAsyncKeyState(0x1B) & 0x8000) != 0) { StopPicking(); return; }
            Native.POINT p; Native.GetCursorPos(out p);
            loupe.Track(new Point(p.X, p.Y));
        };

        hookProc = HookCallback;
        FormClosing += delegate { StopPicking(); if (hotkeyOk) Native.UnregisterHotKey(Handle, HOTKEY_ID); };
        ResetButton();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int on = 1, caption = Theme.Bg.B << 16 | Theme.Bg.G << 8 | Theme.Bg.R;
        try
        {
            Native.DwmSetWindowAttribute(Handle, 20, ref on, 4);      // dark title bar
            Native.DwmSetWindowAttribute(Handle, 35, ref caption, 4); // title bar color (Win 11)
        }
        catch { }
        hotkeyOk = Native.RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)Keys.P);
        if (!hotkeyOk) subtitle = "Ctrl+Shift+P is used by another app";
        ResetButton();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics; Theme.Smooth(g);
        Logo.Draw(g, new RectangleF(20, 18, 44, 44));
        TextRenderer.DrawText(g, "Color Picker", titleFont, new Point(74, 14), Theme.Text);
        TextRenderer.DrawText(g, subtitle, Font, new Point(76, 42), hotkeyOk ? Theme.Muted : Theme.Pink);

        TextRenderer.DrawText(g, "FORMATS", smallFont, new Point(20, formatsY), Theme.Muted);
        TextRenderer.DrawText(g, "● = copied automatically", Font, new Rectangle(190, formatsY - 2, 200, 20), Theme.Muted,
            TextFormatFlags.Right);
        TextRenderer.DrawText(g, "HISTORY", smallFont, new Point(20, historyY), Theme.Muted);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            if (hook == IntPtr.Zero) StartPicking(); else StopPicking();
        }
        base.WndProc(ref m);
    }

    void ResetButton()
    {
        if (pickBtn == null) return;
        pickBtn.Active = false;
        pickBtn.Text = hotkeyOk ? "Pick a color    Ctrl + Shift + P" : "Pick a color";
        pickBtn.Invalidate();
    }

    void SetAuto(int idx)
    {
        autoIdx = autoIdx == idx ? -1 : idx;
        for (int i = 0; i < rows.Length; i++) { rows[i].Auto = i == autoIdx; rows[i].Invalidate(); }
    }

    void StartPicking()
    {
        if (hook != IntPtr.Zero) return;
        hook = Native.SetWindowsHookEx(WH_MOUSE_LL, hookProc,
            Native.GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
        pickBtn.Active = true;
        pickBtn.Text = "Picking…  click anywhere  ·  Esc cancels";
        pickBtn.Invalidate();
        Native.POINT p; Native.GetCursorPos(out p);
        loupe.Track(new Point(p.X, p.Y));
        loupe.Show();
        timer.Start();
    }

    void StopPicking()
    {
        timer.Stop();
        if (hook != IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        loupe.Hide();
        ResetButton();
    }

    IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_LBUTTONDOWN)
            {
                Native.POINT p; Native.GetCursorPos(out p);
                Color c = Native.ColorAt(p.X, p.Y);
                swallowUp = true;
                BeginInvoke((MethodInvoker)delegate { StopPicking(); Picked(c); });
                return (IntPtr)1; // swallow the click so nothing underneath gets clicked
            }
            if (msg == WM_LBUTTONUP && swallowUp) { swallowUp = false; return (IntPtr)1; }
            if (msg == WM_RBUTTONDOWN)
            {
                BeginInvoke((MethodInvoker)StopPicking);
                return (IntPtr)1;
            }
        }
        return Native.CallNextHookEx(hook, code, wParam, lParam);
    }

    void Picked(Color c)
    {
        chips.Add(c);
        Display(c);
        if (autoIdx >= 0) { CopyText(Format(autoIdx, c)); rows[autoIdx].Flash(); }
    }

    void Display(Color c)
    {
        swatch.C = c;
        swatch.Invalidate();
        for (int i = 0; i < rows.Length; i++) rows[i].Box.Text = Format(i, c);
    }

    static void CopyText(string s)
    {
        try { Clipboard.SetText(s); } catch { }
    }

    // Renders the window (and the loupe) to a PNG without showing it on screen; used for README screenshots.
    static void Snapshot(string path)
    {
        var f = new ColorPicker { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000), ShowInTaskbar = false };
        f.Show();
        f.chips.Add(Color.FromArgb(166, 227, 161)); f.chips.Add(Color.FromArgb(250, 179, 135));
        f.chips.Add(Color.FromArgb(137, 180, 250)); f.chips.Add(Color.FromArgb(203, 166, 247));
        f.Picked(Color.FromArgb(243, 139, 168));
        Application.DoEvents();
        Point off = f.PointToScreen(Point.Empty);
        off.Offset(-f.Left, -f.Top);
        using (var full = new Bitmap(f.Width, f.Height))
        {
            f.DrawToBitmap(full, new Rectangle(0, 0, full.Width, full.Height));
            using (var client = full.Clone(new Rectangle(off, f.ClientSize), full.PixelFormat))
                client.Save(path, ImageFormat.Png);
        }
        // loupe preview, showing whatever is under the real cursor
        Native.POINT p; Native.GetCursorPos(out p);
        f.loupe.Track(new Point(p.X, p.Y));
        f.loupe.Location = new Point(-5000, -5000);
        f.loupe.Show();
        Application.DoEvents();
        using (var lb = new Bitmap(f.loupe.Width, f.loupe.Height))
        {
            f.loupe.DrawToBitmap(lb, new Rectangle(0, 0, lb.Width, lb.Height));
            lb.Save(System.IO.Path.ChangeExtension(path, ".loupe.png"), ImageFormat.Png);
        }
        f.Close();
    }

    [STAThread]
    static void Main(string[] args)
    {
        Native.SetProcessDPIAware();
        Application.EnableVisualStyles();
        if (args.Length == 2 && args[0] == "--snapshot")
        {
            try { Snapshot(args[1]); } catch (Exception ex) { System.IO.File.WriteAllText(args[1] + ".err.txt", ex.ToString()); }
            return;
        }
        Application.Run(new ColorPicker());
    }
}
