using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

class ColorPicker : Form
{
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
    [DllImport("gdi32.dll")] static extern uint GetPixel(IntPtr hdc, int x, int y);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr hMod, uint threadId);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    const int WH_MOUSE_LL = 14, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONDOWN = 0x0204;
    const int WM_HOTKEY = 0x0312, HOTKEY_ID = 1;
    const uint MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000;

    class HistItem
    {
        public Color C;
        public HistItem(Color c) { C = c; }
        public override string ToString() { return string.Format("{0}, {1}, {2}   {3}", C.R, C.G, C.B, Hex(C)); }
    }

    // Output formats: label + how to build the text from a color
    static readonly string[] FormatNames = { "R", "G", "B", "R, G, B", "rgb(R, G, B)", "Hex #RRGGBB", "Hex RRGGBB" };
    static string Format(int i, Color c)
    {
        switch (i)
        {
            case 0: return c.R.ToString();
            case 1: return c.G.ToString();
            case 2: return c.B.ToString();
            case 3: return string.Format("{0}, {1}, {2}", c.R, c.G, c.B);
            case 4: return string.Format("rgb({0}, {1}, {2})", c.R, c.G, c.B);
            case 5: return Hex(c);
            default: return Hex(c).Substring(1);
        }
    }

    HookProc hookProc;
    IntPtr hook = IntPtr.Zero;
    bool swallowUp;
    bool hotkeyOk;
    Timer timer;
    Panel liveSwatch, pickedSwatch;
    Label liveLabel, status;
    Button pickBtn;
    TextBox[] fields = new TextBox[FormatNames.Length];
    ComboBox autoCopy;
    ListBox history;
    Color current = Color.Empty;

    ColorPicker()
    {
        Text = "Color Picker";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        TopMost = true;
        Font = new Font("Segoe UI", 9.5f);
        KeyPreview = true;

        int y = 12;
        pickBtn = new Button { Location = new Point(12, y), Size = new Size(316, 34) };
        pickBtn.Click += delegate { StartPicking(); };
        y += 44;

        liveSwatch = new Panel { Location = new Point(12, y), Size = new Size(36, 36), BorderStyle = BorderStyle.FixedSingle };
        liveLabel = new Label { Location = new Point(56, y), Size = new Size(272, 36), ForeColor = Color.DimGray };
        y += 46;

        pickedSwatch = new Panel { Location = new Point(12, y), Size = new Size(316, 30), BorderStyle = BorderStyle.FixedSingle };
        y += 38;

        Controls.AddRange(new Control[] { pickBtn, liveSwatch, liveLabel, pickedSwatch });

        // One row per format: label, selectable text box, copy button
        for (int i = 0; i < FormatNames.Length; i++)
        {
            int idx = i;
            var lbl = new Label { Text = FormatNames[i], Location = new Point(12, y + 4), Size = new Size(100, 20) };
            var tb = new TextBox { Location = new Point(112, y), Size = new Size(150, 24), ReadOnly = true, BackColor = SystemColors.Window };
            var btn = new Button { Text = "Copy", Location = new Point(268, y - 1), Size = new Size(60, 26) };
            btn.Click += delegate { if (current != Color.Empty) CopyText(Format(idx, current), FormatNames[idx]); };
            fields[i] = tb;
            Controls.AddRange(new Control[] { lbl, tb, btn });
            y += 30;
        }

        y += 4;
        var acLbl = new Label { Text = "Auto-copy on pick:", Location = new Point(12, y + 4), Size = new Size(120, 20) };
        autoCopy = new ComboBox { Location = new Point(132, y), Size = new Size(196, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        autoCopy.Items.Add("(nothing)");
        autoCopy.Items.AddRange(FormatNames);
        autoCopy.SelectedIndex = 5; // rgb(R, G, B)
        Controls.AddRange(new Control[] { acLbl, autoCopy });
        y += 34;

        var hLbl = new Label { Text = "History (click to load):", Location = new Point(12, y), Size = new Size(316, 20), ForeColor = Color.DimGray };
        y += 22;
        history = new ListBox { Location = new Point(12, y), Size = new Size(316, 100) };
        history.SelectedIndexChanged += delegate { var h = history.SelectedItem as HistItem; if (h != null) Display(h.C); };
        y += 104;

        status = new Label { Location = new Point(12, y), Size = new Size(316, 20), ForeColor = Color.SeaGreen };
        y += 24;
        Controls.AddRange(new Control[] { hLbl, history, status });

        ClientSize = new Size(340, y);

        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) StopPicking(); };

        timer = new Timer { Interval = 40 };
        timer.Tick += delegate {
            POINT p; GetCursorPos(out p);
            Color c = ColorAt(p.X, p.Y);
            liveSwatch.BackColor = c;
            liveLabel.Text = string.Format("Under cursor ({0}, {1}):\r\n{2}, {3}, {4}   {5}", p.X, p.Y, c.R, c.G, c.B, Hex(c));
        };
        timer.Start();

        hookProc = HookCallback;
        FormClosing += delegate { StopPicking(); if (hotkeyOk) UnregisterHotKey(Handle, HOTKEY_ID); };
        ResetButtonText();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        hotkeyOk = RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, (uint)Keys.P);
        ResetButtonText();
        if (!hotkeyOk) SetStatus("Ctrl+Shift+P is used by another app - use the button.", Color.Firebrick);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            if (hook == IntPtr.Zero) StartPicking(); else StopPicking();
        }
        base.WndProc(ref m);
    }

    void ResetButtonText()
    {
        if (pickBtn != null) pickBtn.Text = hotkeyOk ? "Pick color  (Ctrl+Shift+P)" : "Pick color";
    }

    static string Hex(Color c) { return string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B); }

    static Color ColorAt(int x, int y)
    {
        IntPtr dc = GetDC(IntPtr.Zero);
        uint px = GetPixel(dc, x, y);
        ReleaseDC(IntPtr.Zero, dc);
        return Color.FromArgb((int)(px & 0xFF), (int)((px >> 8) & 0xFF), (int)((px >> 16) & 0xFF));
    }

    void StartPicking()
    {
        if (hook != IntPtr.Zero) return;
        hook = SetWindowsHookEx(WH_MOUSE_LL, hookProc, GetModuleHandle(Process.GetCurrentProcess().MainModule.ModuleName), 0);
        pickBtn.Text = "Click anywhere... (right-click / Esc cancels)";
        Cursor = Cursors.Cross;
    }

    void StopPicking()
    {
        if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
        ResetButtonText();
        Cursor = Cursors.Default;
    }

    IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg == WM_LBUTTONDOWN)
            {
                POINT p; GetCursorPos(out p);
                Color c = ColorAt(p.X, p.Y);
                swallowUp = true;
                BeginInvoke((MethodInvoker)delegate { Picked(c); StopPicking(); });
                return (IntPtr)1; // swallow the click so nothing underneath gets clicked
            }
            if (msg == WM_LBUTTONUP && swallowUp) { swallowUp = false; return (IntPtr)1; }
            if (msg == WM_RBUTTONDOWN)
            {
                BeginInvoke((MethodInvoker)StopPicking);
                return (IntPtr)1;
            }
        }
        return CallNextHookEx(hook, code, wParam, lParam);
    }

    void Picked(Color c)
    {
        history.Items.Insert(0, new HistItem(c));
        Display(c);
        int f = autoCopy.SelectedIndex - 1;
        if (f >= 0) CopyText(Format(f, c), FormatNames[f]);
        else SetStatus("Picked " + Hex(c), Color.SeaGreen);
    }

    void Display(Color c)
    {
        current = c;
        pickedSwatch.BackColor = c;
        for (int i = 0; i < fields.Length; i++) fields[i].Text = Format(i, c);
    }

    void CopyText(string s, string what)
    {
        try { Clipboard.SetText(s); SetStatus("Copied " + what + ":  " + s, Color.SeaGreen); }
        catch { SetStatus("Couldn't access the clipboard, try again.", Color.Firebrick); }
    }

    void SetStatus(string s, Color col) { status.ForeColor = col; status.Text = s; }

    [STAThread]
    static void Main()
    {
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.Run(new ColorPicker());
    }
}

