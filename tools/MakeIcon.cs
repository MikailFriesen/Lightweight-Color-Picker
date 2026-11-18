using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;

// Generates assets/icon.ico (multi-size) and assets/logo.png from Logo.Draw.
// Build: csc /out:MakeIcon.exe tools\MakeIcon.cs Logo.cs   Run: MakeIcon.exe assets
class MakeIcon
{
    static void Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : ".";
        Directory.CreateDirectory(dir);

        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
        var pngs = new List<byte[]>();
        foreach (int s in sizes)
            using (var bmp = Logo.Render(s))
            using (var ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); pngs.Add(ms.ToArray()); }

        using (var fs = File.Create(Path.Combine(dir, "icon.ico")))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(offset);
                offset += pngs[i].Length;
            }
            foreach (var p in pngs) w.Write(p);
        }

        using (var bmp = Logo.Render(512)) bmp.Save(Path.Combine(dir, "logo.png"), ImageFormat.Png);
        Console.WriteLine("Wrote icon.ico and logo.png to " + dir);
    }
}
