using System;

namespace StashNet.Core.Skins
{
    /// <summary>
    /// Damaged and static frames for the terminal screen. Buffers are RGBA bytes, rows bottom-up, alpha 255.
    /// Everything is deterministic per seed.
    /// </summary>
    public static class ScreenGlitch
    {
        /// <summary>A copy of <paramref name="clean"/> with horizontal band tears, an RGB split and sparse noise. Strength 0 gives an identical copy.</summary>
        public static byte[] Damage(byte[] clean, int w, int h, int seed, float strength)
        {
            var o = (byte[])clean.Clone();
            if (strength <= 0f) return o;
            if (strength > 1f) strength = 1f;
            var rng = new Random(seed);

            int bands = 2 + (int)(strength * 6f);
            for (int n = 0; n < bands; n++)
            {
                int y0 = rng.Next(h);
                int bh = Math.Min(h - y0, 2 + rng.Next(1 + (int)(strength * 14f)));
                int shift = (rng.Next(2) == 0 ? -1 : 1) * (4 + rng.Next(1 + (int)(strength * 40f)));
                for (int y = y0; y < y0 + bh; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int sx = ((x - shift) % w + w) % w;
                        int d = (y * w + x) * 4, s = (y * w + sx) * 4;
                        o[d] = clean[s]; o[d + 1] = clean[s + 1]; o[d + 2] = clean[s + 2];
                    }
            }

            // RGB split: red moves one way, blue the other.
            int split = 1 + (int)(strength * 3f);
            var tmp = (byte[])o.Clone();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int d = (y * w + x) * 4;
                    int rx = Math.Min(w - 1, x + split), bx = Math.Max(0, x - split);
                    o[d] = tmp[(y * w + rx) * 4];
                    o[d + 2] = tmp[(y * w + bx) * 4 + 2];
                }

            int specks = (int)(w * h * 0.01f * strength);
            for (int n = 0; n < specks; n++)
            {
                int d = (rng.Next(h) * w + rng.Next(w)) * 4;
                byte v = (byte)(120 + rng.Next(136));
                o[d] = (byte)(v / 4); o[d + 1] = v; o[d + 2] = (byte)(v / 3);
            }
            return o;
        }

        /// <summary>Dim green noise on the screen background, opaque.</summary>
        public static byte[] Static(int w, int h, int seed)
        {
            var o = new byte[w * h * 4];
            var rng = new Random(seed);
            for (int i = 0; i < w * h; i++)
            {
                int k = rng.Next(100);
                float f = k < 8 ? 0.55f : k < 35 ? 0.2f : 0f;
                f *= 0.5f + 0.5f * (float)rng.NextDouble();
                o[i * 4] = (byte)(ScreenPainter.Background.R + (ScreenPainter.Text.R - ScreenPainter.Background.R) * f);
                o[i * 4 + 1] = (byte)(ScreenPainter.Background.G + (ScreenPainter.Text.G - ScreenPainter.Background.G) * f);
                o[i * 4 + 2] = (byte)(ScreenPainter.Background.B + (ScreenPainter.Text.B - ScreenPainter.Background.B) * f);
                o[i * 4 + 3] = 255;
            }
            return o;
        }

        /// <summary>Seconds until the next glitch, uniform in [min, max].</summary>
        public static float NextGap(Random rng, float min, float max)
        {
            if (max < min) { float t = min; min = max; max = t; }
            return min + (float)rng.NextDouble() * (max - min);
        }

        /// <summary>How long a glitch lasts, uniform in [0.1, max]; max is floored at 0.1.</summary>
        public static float NextLength(Random rng, float max)
        {
            if (max < 0.1f) max = 0.1f;
            return 0.1f + (float)rng.NextDouble() * (max - 0.1f);
        }
    }
}
