using System;
using System.Globalization;
using StashNet.Core.Protocol;

namespace StashNet.Core.Skins
{
    /// <summary>Draws the Storage Terminal's live screen (256x192 RGBA, rows bottom-up).</summary>
    public static class ScreenPainter
    {
        public const int Width = 256;
        public const int Height = 192;

        public static readonly (byte R, byte G, byte B) Background = (3, 18, 8);
        public static readonly (byte R, byte G, byte B) Text = (57, 255, 106);
        public static readonly (byte R, byte G, byte B) BarHigh = (255, 210, 58);
        public static readonly (byte R, byte G, byte B) BarFull = (255, 74, 58);
        public static readonly (byte R, byte G, byte B) Dim = (20, 70, 35);

        const int Margin = 10;
        const int FirstLineY = 12;
        const int LinePitch = 30;

        public static string Thousands(int n) => n.ToString("N0", CultureInfo.InvariantCulture);

        public static byte[] Draw(ScreenView v)
        {
            var buf = new byte[Width * Height * 4];
            Rect(buf, 0, 0, Width, Height, Background);

            if (!v.HasCore)
            {
                TextCentered(buf, "NO SIGNAL", 3, (Height - 3 * ScreenFont.GlyphHeight) / 2, Text);
                return buf;
            }
            if (v.Shut)
            {
                TextAt(buf, "STASHNET", 2, Margin, LineY(1), Text);
                TextCentered(buf, "[LOCKED]", 4, (Height - 4 * ScreenFont.GlyphHeight) / 2, Text);
                return buf;
            }

            TextAt(buf, "STASHNET", 2, Margin, LineY(1), Text);
            TextRight(buf, v.Locked ? "[LOCKED]" : "[OPEN]", 2, LineY(1), Text);

            long used = v.UsedStacks, max = v.MaxStacks;
            long percent = max > 0 ? Math.Max(0, used) * 100 / max : 0;
            TextAt(buf, "STACKS", 2, Margin, LineY(2), Text);
            TextRight(buf, percent + "%", 2, LineY(2), Text);

            DrawBar(buf, LineY(3), used, max);

            TextAt(buf, Thousands(v.UsedStacks) + " / " + Thousands(v.MaxStacks), 2, Margin, LineY(4), Text);

            TextAt(buf, "DRIVES " + v.Drives, 2, Margin, LineY(5), Text);
            if (v.FullDrives > 0) TextRight(buf, v.FullDrives + " FULL", 2, LineY(5), Text);

            int x = Margin + TextWidth("HEAT", 2) + 12;
            int y = LineY(6);
            for (int i = 0; i < 6; i++)
            {
                Rect(buf, x, y, 12, 14, i < v.HeatSegments ? Text : Dim);
                x += 16;
            }
            TextAt(buf, ScreenView.HeatWord(v.HeatSegments), 2, x + 8, y, Text);
            return buf;
        }

        static int LineY(int n) => FirstLineY + LinePitch * (n - 1);

        static void DrawBar(byte[] buf, int y, long used, long max)
        {
            int w = Width - 2 * Margin;
            const int h = 14, frame = 2;
            Rect(buf, Margin, y, w, h, Dim);
            Rect(buf, Margin + frame, y + frame, w - 2 * frame, h - 2 * frame, Background);
            double frac = max > 0 ? Math.Min(1.0, Math.Max(0.0, (double)used / max)) : 0.0;
            var color = max > 0 && used >= max ? BarFull : frac >= 0.75 ? BarHigh : Text;
            int fill = (int)((w - 2 * frame) * frac);
            if (fill > 0) Rect(buf, Margin + frame, y + frame, fill, h - 2 * frame, color);
        }

        static int TextWidth(string s, int scale) => s.Length == 0 ? 0 : s.Length * 6 * scale - scale;

        static void TextCentered(byte[] buf, string s, int scale, int y, (byte R, byte G, byte B) c) =>
            TextAt(buf, s, scale, (Width - TextWidth(s, scale)) / 2, y, c);

        static void TextRight(byte[] buf, string s, int scale, int y, (byte R, byte G, byte B) c) =>
            TextAt(buf, s, scale, Width - Margin - TextWidth(s, scale), y, c);

        static void TextAt(byte[] buf, string s, int scale, int x, int y, (byte R, byte G, byte B) c)
        {
            foreach (char ch in s)
            {
                for (int gy = 0; gy < ScreenFont.GlyphHeight; gy++)
                    for (int gx = 0; gx < ScreenFont.GlyphWidth; gx++)
                        if (ScreenFont.IsLit(ch, gx, gy))
                            Rect(buf, x + gx * scale, y + gy * scale, scale, scale, c);
                x += 6 * scale;
            }
        }

        /// <summary>Fills a rectangle given by its top-left corner in y-from-top coordinates.</summary>
        static void Rect(byte[] buf, int x, int yTop, int w, int h, (byte R, byte G, byte B) c)
        {
            for (int py = Math.Max(0, yTop); py < Math.Min(Height, yTop + h); py++)
            {
                int row = Height - 1 - py;
                for (int px = Math.Max(0, x); px < Math.Min(Width, x + w); px++)
                {
                    int i = (row * Width + px) * 4;
                    buf[i] = c.R;
                    buf[i + 1] = c.G;
                    buf[i + 2] = c.B;
                    buf[i + 3] = 255;
                }
            }
        }
    }
}
