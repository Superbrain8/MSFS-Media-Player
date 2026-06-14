using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MsfsMediaPlayer.Companion;

/// <summary>Builds the tray icon at runtime (a music note on a blue disc) — no binary asset needed.</summary>
internal static class AppIcon
{
    public static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            using var disc = new SolidBrush(Color.FromArgb(52, 152, 219));
            g.FillEllipse(disc, 1, 1, 30, 30);

            using var font = new Font("Segoe UI Symbol", 17, FontStyle.Bold, GraphicsUnit.Pixel);
            using var fg = new SolidBrush(Color.White);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("♫", font, fg, new RectangleF(0, 0, 32, 33), sf); // ♫
        }

        // FromHandle doesn't own the HICON; clone to a managed copy so the bitmap can be disposed.
        var handle = bmp.GetHicon();
        using var tmp = Icon.FromHandle(handle);
        return (Icon)tmp.Clone();
    }
}
