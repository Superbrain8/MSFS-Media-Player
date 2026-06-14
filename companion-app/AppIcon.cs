using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace MsfsMediaPlayer.Companion;

/// <summary>Builds the tray icon at runtime (a music note on a coloured disc) — no binary asset.</summary>
internal static class AppIcon
{
    // Disc colour signals sim connection: red = disconnected, green = connected.
    public static readonly Color Disconnected = Color.FromArgb(231, 76, 60);
    public static readonly Color Connected = Color.FromArgb(46, 204, 113);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon CreateTrayIcon(Color disc)
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            using var discBrush = new SolidBrush(disc);
            g.FillEllipse(discBrush, 1, 1, 30, 30);

            using var font = new Font("Segoe UI Symbol", 17, FontStyle.Bold, GraphicsUnit.Pixel);
            using var fg = new SolidBrush(Color.White);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("♫", font, fg, new RectangleF(0, 0, 32, 33), sf); // ♫
        }

        // FromHandle doesn't own the HICON; clone to a managed copy, then free the temp handle.
        var handle = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(handle);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}
