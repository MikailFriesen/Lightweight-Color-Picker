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
    public static readonly Color Bg     = Color.FromArgb(23, 25, 30);
    public static readonly Color Panel  = Color.FromArgb(33, 36, 43);
    public static readonly Color Hover  = Color.FromArgb(43, 47, 56);
    public static readonly Color Border = Color.FromArgb(52, 57, 68);
    public static readonly Color Text   = Color.FromArgb(230, 233, 240);
    public static readonly Color Muted  = Color.FromArgb(138, 145, 160);
    public static readonly Color Accent = Color.FromArgb(64, 148, 250);
    public static readonly Color AccentHi = Color.FromArgb(92, 166, 255);
    public static readonly Color Ok     = Color.FromArgb(110, 200, 120);
    public static readonly Color R      = Color.FromArgb(240, 112, 112);
    public static readonly Color G      = Color.FromArgb(108, 200, 122);
    public static readonly Color B      = Color.FromArgb(104, 158, 250);

    public static string Hex(Color c) { return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }
    public static Color Contrast(Color c) { return (c.R * 299 + c.G * 587 + c.B * 114) / 1000 > 150 ? Color.Black : Color.White; }

    public static void Smooth(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
    }

    public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
    public const TextFormatFlags Left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter;
    public const TextFormatFlags Right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter;
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

class FlatButton : Painted
{
    public bool Active;
    bool hover;
    public FlatButton() { Cursor = Cursors.Hand; }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); Theme.Smooth(g);
        using (var p = Logo.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 5))
        {
            if (Active)
            {
                using (var b = new SolidBrush(Theme.Panel)) g.FillPath(b, p);
                using (var pen = new Pen(Theme.Accent, 1.5f)) g.DrawPath(pen, p);
            }
            else
                using (var b = new SolidBrush(hover ? Theme.AccentHi : Theme.Accent)) g.FillPath(b, p);
        }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, Active ? Theme.AccentHi : Color.White, Theme.Center);
    }
}

class Swatch : Painted
{
    public Color? C;
    public Font Mono;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); Theme.Smooth(g);
        using (var p = Logo.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 5))
        {
            if (C == null)
            {
                using (var pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
                TextRenderer.DrawText(g, "No color picked", Font, ClientRectangle, Theme.Muted, Theme.Center);
                return;
            }
            using (var b = new SolidBrush(C.Value)) g.FillPath(b, p);
            using (var pen = new Pen(Color.FromArgb(30, 255, 255, 255))) g.DrawPath(pen, p);
        }
        Color c = C.Value, fg = Theme.Contrast(c);
        TextRenderer.DrawText(g, Theme.Hex(c), Mono, new Rectangle(12, 0, Width - 24, Height), fg, Theme.Left);
        TextRenderer.DrawText(g, string.Format("{0}, {1}, {2}", c.R, c.G, c.B), Font, new Rectangle(12, 0, Width - 24, Height),
            Color.FromArgb(fg.R == 0 ? 90 : 200, fg.R == 0 ? 90 : 200, fg.R == 0 ? 90 : 200), Theme.Right);
    }
}

// label | value | copy, with a small dot on the left to choose the auto-copied format
class FormatRow : Painted
{
    public string Label;
    public Color LabelColor = Theme.Muted;
    public bool Auto;
    public readonly TextBox Box;
    public event EventHandler CopyClick, AutoClick;
    bool hoverCopy, hoverDot, copied;
    readonly Timer reset = new Timer { Interval = 1200 };

    Rectangle DotRect  { get { return new Rectangle(3, (Height - 9) / 2, 9, 9); } }
    Rectangle DotHit   { get { return new Rectangle(0, 0, 18, Height); } }
    Rectangle CopyRect { get { return new Rectangle(Width - 56, 3, 56, Height - 6); } }

    public FormatRow()
    {
        Height = 28;
        Box = new TextBox { BorderStyle = BorderStyle.None, ReadOnly = true, BackColor = Theme.Bg,
                            ForeColor = Theme.Text, Font = new Font("Consolas", 10f), TabStop = false };
        Controls.Add(Box);
        reset.Tick += delegate { reset.Stop(); copied = false; Invalidate(); };
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (Box == null) return;
        Box.Location = new Point(96, (Height - Box.Height) / 2 + 1);
        Box.Width = Width - 96 - 60;
    }

    public void Flash() { copied = true; Invalidate(); reset.Stop(); reset.Start(); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool c = CopyRect.Contains(e.Location), d = DotHit.Contains(e.Location);
        if (c != hoverCopy || d != hoverDot) { hoverCopy = c; hoverDot = d; Cursor = (c || d) ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }
    protected override void OnMouseLeave(EventArgs e) { hoverCopy = hoverDot = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (CopyRect.Contains(e.Location) && CopyClick != null) CopyClick(this, e);
        else if (DotHit.Contains(e.Location) && AutoClick != null) AutoClick(this, e);
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); Theme.Smooth(g);
        var dr = DotRect;
        if (Auto) using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, dr);
        else using (var pen = new Pen(hoverDot ? Theme.Muted : Theme.Border)) g.DrawEllipse(pen, dr);

        TextRenderer.DrawText(g, Label, Font, new Rectangle(20, 0, 76, Height), LabelColor, Theme.Left);

        var cr = CopyRect;
        using (var p = Logo.RoundRect(cr, 4))
        using (var b = new SolidBrush(hoverCopy && !copied ? Theme.Hover : Theme.Panel))
            g.FillPath(b, p);
        TextRenderer.DrawText(g, copied ? "copied" : "copy", Font, cr,
            copied ? Theme.Ok : hoverCopy ? Theme.AccentHi : Theme.Muted, Theme.Center);
    }
}

class Chips : Painted
{
    public readonly List<Color> Items = new List<Color>();
    public event Action<Color> ChipClick;
    int hover = -1;
    const int S = 24, G = 4, Max = 10;

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
            TextRenderer.DrawText(g, "History is empty", Font, ClientRectangle, Theme.Muted, Theme.Left);
            return;
        }
        for (int i = 0; i < Items.Count; i++)
            using (var p = Logo.RoundRect(ChipRect(i), 4))
            {
                using (var b = new SolidBrush(Items[i])) g.FillPath(b, p);
                using (var pen = new Pen(i == hover ? Theme.Text : Theme.Border)) g.DrawPath(pen, p);
            }
    }
}

// Magnifier that follows the cursor while picking
class Loupe : Form
{
    const int N = 11, Z = 10, Pad = 5, Info = 22;
    readonly Bitmap cap = new Bitmap(N, N, PixelFormat.Format32bppRgb);
    Color center;
    readonly Font mono = new Font("Consolas", 8.5f);

    public Loupe()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Theme.Panel;
        DoubleBuffered = true;
        ClientSize = new Size(N * Z + Pad * 2, N * Z + Pad * 2 + Info);
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80 | 0x08000000 | 0x20 | 0x80000 | 0x8; // toolwindow, noactivate, click-through, topmost
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.SetLayeredWindowAttributes(Handle, 0, 255, 2);
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
        int x = p.X + 18, y = p.Y + 18;
        if (x + Width > wa.Right) x = p.X - 18 - Width;
        if (y + Height > wa.Bottom) y = p.Y - 18 - Height;
        if (Location.X != x || Location.Y != y) Location = new Point(x, y);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Panel);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(cap, new Rectangle(Pad, Pad, N * Z, N * Z));
        g.PixelOffsetMode = PixelOffsetMode.Default;

        var mid = new Rectangle(Pad + (N / 2) * Z, Pad + (N / 2) * Z, Z - 1, Z - 1);
        using (var outer = new Pen(Color.Black)) g.DrawRectangle(outer, Rectangle.Inflate(mid, 1, 1));
        using (var inner = new Pen(Color.White)) g.DrawRectangle(inner, mid);

        int iy = Pad * 2 + N * Z;
        var chip = new Rectangle(Pad, iy + 2, 12, 12);
        using (var b = new SolidBrush(center)) g.FillRectangle(b, chip);
        using (var pen = new Pen(Theme.Border)) g.DrawRectangle(pen, chip);
        TextRenderer.DrawText(g, Theme.Hex(center), mono, new Rectangle(Pad + 16, iy, Width, 16), Theme.Text, Theme.Left);

        using (var pen = new Pen(Theme.Accent)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }
}

class ColorPicker : Form
{
    const int WH_MOUSE_LL = 14, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204;
    const int WM_HOTKEY = 0x0312, HOTKEY_ID = 1;
    const uint MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

    static readonly string[] FormatNames = { "Red", "Green", "Blue", "R, G, B", "rgb()", "Hex", "Hex, no #" };
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
    readonly FlatButton pickBtn;
    readonly Swatch swatch;
    readonly FormatRow[] rows = new FormatRow[FormatNames.Length];
    readonly Chips chips;
    readonly Loupe loupe = new Loupe();
    int autoIdx = 4;
    int sepY;

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

        const int X = 14, W = 300;
        int y = 14;

        pickBtn = new FlatButton { Location = new Point(X, y), Size = new Size(W, 36), Font = new Font("Segoe UI Semibold", 9.75f) };
        pickBtn.Click += delegate { if (hook == IntPtr.Zero) StartPicking(); else StopPicking(); };
        y += 46;

        swatch = new Swatch { Location = new Point(X, y), Size = new Size(W, 62), Mono = new Font("Consolas", 15f) };
        y += 72;

        var labelColors = new[] { Theme.R, Theme.G, Theme.B };

        var tip = new ToolTip();
        for (int i = 0; i < rows.Length; i++)
        {
            int idx = i;
            var row = new FormatRow { Label = FormatNames[i], Location = new Point(X, y), Width = W, Auto = i == autoIdx };
            if (i < 3) row.LabelColor = labelColors[i];
            row.CopyClick += delegate { if (swatch.C != null) { CopyText(Format(idx, swatch.C.Value)); row.Flash(); } };
            row.AutoClick += delegate { SetAuto(idx); };
            tip.SetToolTip(row, "Dot: copy this format automatically after each pick");
            rows[i] = row;
            Controls.Add(row);
            y += 28;
        }

        sepY = y + 8; y += 17;
        chips = new Chips { Location = new Point(X, y), Size = new Size(W, 28) };
        chips.ChipClick += Display;
        y += 28 + 12;

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
        ResetButton();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (var pen = new Pen(Theme.Border)) e.Graphics.DrawLine(pen, 14, sepY, ClientSize.Width - 14, sepY);
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
        pickBtn.Text = hotkeyOk ? "Pick   (Ctrl+Shift+P)" : "Pick   (hotkey in use)";
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
        pickBtn.Text = "Click to pick   (Esc to cancel)";
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

    // Renders the window (and the loupe) to PNGs without showing them on screen; used for README screenshots.
    static void Snapshot(string path)
    {
        var f = new ColorPicker { StartPosition = FormStartPosition.Manual, Location = new Point(-5000, -5000), ShowInTaskbar = false };
        f.Show();
        f.chips.Add(Color.FromArgb(46, 139, 87)); f.chips.Add(Color.FromArgb(240, 180, 60));
        f.chips.Add(Color.FromArgb(70, 110, 200)); f.chips.Add(Color.FromArgb(200, 200, 200));
        f.Picked(Color.FromArgb(214, 84, 66));
        Application.DoEvents();
        Point off = f.PointToScreen(Point.Empty);
        off.Offset(-f.Left, -f.Top);
        using (var full = new Bitmap(f.Width, f.Height))
        {
            f.DrawToBitmap(full, new Rectangle(0, 0, full.Width, full.Height));
            using (var client = full.Clone(new Rectangle(off, f.ClientSize), full.PixelFormat))
                client.Save(path, ImageFormat.Png);
        }
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
