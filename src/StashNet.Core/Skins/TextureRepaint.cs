using System;
using System.Collections.Generic;

namespace StashNet.Core.Skins
{
    /// <summary>
    /// A sprite inside the atlas pixels (RGBA, row 0 = bottom), sampled with bilinear filtering and never
    /// reading outside its own rectangle.
    /// </summary>
    public sealed class SpriteImage
    {
        private readonly byte[] px;
        private readonly int aw, ah;
        private readonly float x0, x1, y0, y1;

        public SpriteImage(byte[] atlasPixels, int atlasWidth, int atlasHeight, UvRect rect)
        {
            px = atlasPixels; aw = atlasWidth; ah = atlasHeight;
            x0 = rect.U0 * aw; x1 = rect.U1 * aw; y0 = rect.V0 * ah; y1 = rect.V1 * ah;
        }

        /// <param name="s">0..1 across the sprite, left to right.</param>
        /// <param name="t">0..1 up the sprite, bottom to top.</param>
        public void Sample(float s, float t, out byte r, out byte g, out byte b, out byte a)
        {
            float fx = Clamp(x0 + s * (x1 - x0) - 0.5f, x0, x1 - 1f);
            float fy = Clamp(y0 + t * (y1 - y0) - 0.5f, y0, y1 - 1f);
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
            int jx = Math.Min(ix + 1, (int)x1 - 1), jy = Math.Min(iy + 1, (int)y1 - 1);
            float tx = fx - ix, ty = fy - iy;
            r = Lerp2(0, ix, iy, jx, jy, tx, ty);
            g = Lerp2(1, ix, iy, jx, jy, tx, ty);
            b = Lerp2(2, ix, iy, jx, jy, tx, ty);
            a = Lerp2(3, ix, iy, jx, jy, tx, ty);
        }

        private byte Lerp2(int c, int ix, int iy, int jx, int jy, float tx, float ty)
        {
            float p00 = px[(iy * aw + ix) * 4 + c], p10 = px[(iy * aw + jx) * 4 + c];
            float p01 = px[(jy * aw + ix) * 4 + c], p11 = px[(jy * aw + jx) * 4 + c];
            float top = p01 + (p11 - p01) * tx, bottom = p00 + (p10 - p00) * tx;
            return (byte)Math.Round(bottom + (top - bottom) * ty);
        }

        private static float Clamp(float v, float lo, float hi) => hi < lo ? lo : Math.Max(lo, Math.Min(hi, v));
    }

    /// <summary>
    /// The runtime repaint of a vanilla texture, on RGBA byte arrays (row 0 = bottom). Alpha is never changed:
    /// in the game's tint-mask shader it can carry the tint mask.
    /// </summary>
    public static class TextureRepaint
    {
        private const int GridCells = 8;

        /// <summary>Replace the hue with the tint while keeping each pixel's brightness (so detail survives).</summary>
        public static void Recolor(byte[] px, int w, int h, byte r, byte g, byte b, float strength)
        {
            for (int i = 0; i < w * h * 4; i += 4)
            {
                float l = (0.299f * px[i] + 0.587f * px[i + 1] + 0.114f * px[i + 2]) / 255f;
                float k = 0.35f + 1.3f * l;
                px[i] = Mix(px[i], r * k, strength);
                px[i + 1] = Mix(px[i + 1], g * k, strength);
                px[i + 2] = Mix(px[i + 2], b * k, strength);
            }
        }

        /// <summary>Patchy dirt: large dark patches plus fine variation. Only ever darkens.</summary>
        public static void Grime(byte[] px, int w, int h, int seed, float amount)
        {
            if (amount <= 0f) return;
            float big = Math.Max(w, h) / 6f, fine = Math.Max(w, h) / 40f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float patches = Noise(x / big, y / big, seed);
                    float detail = Noise(x / fine, y / fine, seed + 101);
                    float dark = amount * (0.45f * SmoothStep(0.5f, 1f, patches) + 0.14f * detail);
                    float f = Math.Max(0.3f, 1f - dark);
                    int i = (y * w + x) * 4;
                    px[i] = (byte)(px[i] * f);
                    px[i + 1] = (byte)(px[i + 1] * f);
                    px[i + 2] = (byte)(px[i + 2] * f);
                }
        }

        /// <summary>Paint a sprite over the texture (alpha-over). u,v = center, w,h = fraction of the texture.</summary>
        public static void Stamp(byte[] px, int w, int h, SpriteImage sprite, float u, float v, float wFrac, float hFrac, float rotDeg)
        {
            float cx = u * w, cy = v * h, sw = wFrac * w, sh = hFrac * h;
            if (sw <= 0f || sh <= 0f) return;
            double a = rotDeg * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            float reach = 0.5f * (float)Math.Sqrt(sw * sw + sh * sh);
            int xa = Math.Max(0, (int)Math.Floor(cx - reach)), xb = Math.Min(w - 1, (int)Math.Ceiling(cx + reach));
            int ya = Math.Max(0, (int)Math.Floor(cy - reach)), yb = Math.Min(h - 1, (int)Math.Ceiling(cy + reach));
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float s = (dx * cos + dy * sin) / sw + 0.5f;     // turn the pixel back by rot: sprite space
                    float t = (-dx * sin + dy * cos) / sh + 0.5f;
                    if (s < 0f || s >= 1f || t < 0f || t >= 1f) continue;
                    sprite.Sample(s, t, out byte r, out byte g, out byte b, out byte sa);
                    if (sa == 0) continue;
                    float k = sa / 255f;
                    int i = (y * w + x) * 4;
                    px[i] = Mix(px[i], r, k);
                    px[i + 1] = Mix(px[i + 1], g, k);
                    px[i + 2] = Mix(px[i + 2], b, k);
                }
        }

        private static readonly (string sprite, float min, float max, float aspect, int weight)[] Damage =
        {
            ("scorch1", 0.08f, 0.16f, 0.8f, 3), ("scorch2", 0.08f, 0.16f, 0.8f, 3),
            ("rustStreak1", 0.03f, 0.05f, 5f, 4), ("rustStreak2", 0.03f, 0.05f, 5f, 4),
            ("bareMetal1", 0.03f, 0.06f, 0.8f, 2), ("bareMetal2", 0.03f, 0.06f, 0.8f, 2), ("bareMetal3", 0.03f, 0.06f, 0.8f, 1),
            ("dent1", 0.04f, 0.07f, 0.66f, 2), ("clawGouge", 0.06f, 0.1f, 1.5f, 2),
            ("bulletHole1", 0.022f, 0.035f, 1f, 4), ("bulletHole2", 0.022f, 0.035f, 1f, 4),
        };

        /// <summary>
        /// The scattered wear for one texture: deterministic for a seed. Without holes (armored), bullet hits
        /// become dents (bulletDent) instead.
        /// </summary>
        public static List<TextureStamp> ScatterDamage(int seed, float amount, bool holes = true)
        {
            var list = new List<TextureStamp>();
            int count = (int)Math.Round(36 * Math.Max(0f, amount));
            if (count == 0) return list;
            int total = 0;
            foreach (var d in Damage) total += d.weight;
            var rnd = new Random(seed);
            for (int n = 0; n < count; n++)
            {
                int pick = rnd.Next(total);
                var d = Damage[0];
                foreach (var c in Damage) { if (pick < c.weight) { d = c; break; } pick -= c.weight; }
                float size = d.min + (float)rnd.NextDouble() * (d.max - d.min);
                string sprite = d.sprite.StartsWith("bulletHole") && !holes ? "bulletDent" : d.sprite;
                bool upright = d.sprite.StartsWith("rust");    // rust runs down
                list.Add(new TextureStamp
                {
                    Sprite = sprite,
                    U = 0.02f + (float)rnd.NextDouble() * 0.96f,
                    V = 0.02f + (float)rnd.NextDouble() * 0.96f,
                    W = size,
                    H = size * d.aspect,
                    Rot = upright ? 0f : (float)(rnd.NextDouble() * 360.0),
                });
            }
            return list;
        }

        /// <summary>Copies one sprite out of the atlas pixels (e.g. the CRT screen picture, used as its own texture).</summary>
        public static byte[] Crop(byte[] atlas, int aw, int ah, UvRect rect, out int w, out int h)
        {
            int x0 = (int)Math.Round(rect.U0 * aw), x1 = (int)Math.Round(rect.U1 * aw);
            int y0 = (int)Math.Round(rect.V0 * ah), y1 = (int)Math.Round(rect.V1 * ah);
            w = Math.Max(0, x1 - x0); h = Math.Max(0, y1 - y0);
            var crop = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
                Buffer.BlockCopy(atlas, ((y0 + y) * aw + x0) * 4, crop, y * w * 4, w * 4);
            return crop;
        }

        /// <summary>Grid cell name: column A-H left to right, row 1-8 from the top of the texture.</summary>
        public static string GridCell(float u, float v)
        {
            int col = Math.Max(0, Math.Min(GridCells - 1, (int)Math.Floor(u * GridCells)));
            int row = Math.Max(0, Math.Min(GridCells - 1, (int)Math.Floor((1f - v) * GridCells)));
            return $"{(char)('A' + col)}{row + 1}";
        }

        public static (float u, float v) CellCenter(string cell)
        {
            if (cell == null || cell.Length != 2) throw new ArgumentException($"bad grid cell '{cell}'");
            int col = char.ToUpperInvariant(cell[0]) - 'A', row = cell[1] - '1';
            if (col < 0 || col >= GridCells || row < 0 || row >= GridCells) throw new ArgumentException($"bad grid cell '{cell}'");
            return ((col + 0.5f) / GridCells, 1f - (row + 0.5f) / GridCells);
        }

        /// <summary>The debug paint for finding faces: yellow cell lines and a label like "C4" in every cell.</summary>
        public static void Grid(byte[] px, int w, int h, Func<char, SpriteImage> glyph)
        {
            int thick = Math.Max(1, Math.Min(w, h) / 256);
            int cellW = Math.Max(1, w / GridCells), cellH = Math.Max(1, h / GridCells);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (x % cellW >= thick && y % cellH >= thick) continue;
                    int i = (y * w + x) * 4;
                    px[i] = 255; px[i + 1] = 220; px[i + 2] = 0;
                }

            for (int c = 0; c < GridCells; c++)
                for (int r = 0; r < GridCells; r++)
                {
                    string name = $"{(char)('A' + c)}{r + 1}";
                    (float u, float v) = CellCenter(name);
                    float cw = 1f / GridCells;
                    FillRect(px, w, h, u - cw * 0.3f, v - cw * 0.17f, u + cw * 0.3f, v + cw * 0.17f);
                    Stamp(px, w, h, glyph(name[0]), u - cw * 0.14f, v, cw * 0.24f, cw * 0.3f, 0f);
                    Stamp(px, w, h, glyph(name[1]), u + cw * 0.14f, v, cw * 0.24f, cw * 0.3f, 0f);
                }
        }

        private static void FillRect(byte[] px, int w, int h, float u0, float v0, float u1, float v1)
        {
            int xa = Math.Max(0, (int)(u0 * w)), xb = Math.Min(w, (int)(u1 * w));
            int ya = Math.Max(0, (int)(v0 * h)), yb = Math.Min(h, (int)(v1 * h));
            for (int y = ya; y < yb; y++)
                for (int x = xa; x < xb; x++)
                {
                    int i = (y * w + x) * 4;
                    px[i] = 0; px[i + 1] = 0; px[i + 2] = 0;
                }
        }

        private static byte Mix(byte from, float to, float k)
        {
            float v = from + (Math.Max(0f, Math.Min(255f, to)) - from) * k;
            return (byte)Math.Max(0f, Math.Min(255f, Math.Round(v)));
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Math.Max(0f, Math.Min(1f, (x - a) / (b - a)));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Smooth value noise, 0..1, deterministic per seed.</summary>
        private static float Noise(float x, float y, int seed)
        {
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy, seed), b = Hash(ix + 1, iy, seed), c = Hash(ix, iy + 1, seed), d = Hash(ix + 1, iy + 1, seed);
            return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fy;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }
    }
}
