using System;
using System.Collections.Generic;

namespace StashNet.Core.Skins
{
    /// <summary>
    /// Draws a mesh's triangle edges in texture space, so a dumped texture can be read next to a map of
    /// where each face of the model sits on it. Rows run bottom-up, like Unity textures. Tiled UVs (outside
    /// 0..1) wrap into the texture.
    /// </summary>
    public static class UvWireframe
    {
        public static bool[] Draw(int size, IReadOnlyList<float> u, IReadOnlyList<float> v, IReadOnlyList<int> triangles)
        {
            var mask = new bool[size * size];
            if (u == null || v == null || triangles == null || u.Count != v.Count) return mask;
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                if (!Valid(a, u.Count) || !Valid(b, u.Count) || !Valid(c, u.Count)) continue;
                Line(mask, size, u[a], v[a], u[b], v[b]);
                Line(mask, size, u[b], v[b], u[c], v[c]);
                Line(mask, size, u[c], v[c], u[a], v[a]);
            }
            return mask;
        }

        private static bool Valid(int i, int count) => i >= 0 && i < count;

        private static int Pixel(float coord, int size)
        {
            double f = coord - Math.Floor(coord);
            if (coord != 0f && f == 0.0 && coord > 0f) f = 1.0;  // exactly 1.0 is the last pixel, not the first
            return Math.Min(size - 1, (int)Math.Round(f * (size - 1)));
        }

        private static void Line(bool[] mask, int size, float u0, float v0, float u1, float v1)
        {
            int x0 = Pixel(u0, size), y0 = Pixel(v0, size), x1 = Pixel(u1, size), y1 = Pixel(v1, size);
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1, err = dx + dy;
            while (true)
            {
                mask[y0 * size + x0] = true;
                if (x0 == x1 && y0 == y1) return;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }
}
