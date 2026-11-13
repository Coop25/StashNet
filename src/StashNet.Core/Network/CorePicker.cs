using System;
using System.Collections.Generic;

namespace StashNet.Core.Network
{
    /// <summary>A Storage Core's position and reach (blocks, cube distance), as the picker sees it.</summary>
    public readonly struct CoreSite
    {
        public CoreSite(int x, int y, int z, int reach)
        {
            X = x;
            Y = y;
            Z = z;
            Reach = reach;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public int Reach { get; }
    }

    /// <summary>
    /// Which Core a bay or terminal belongs to (Milestone 8): the nearest Core (cube distance) whose
    /// reach covers it. Ties go to the lowest Y, then X, then Z, so every lookup agrees no matter
    /// which order the Cores were found in. This keeps a bay in exactly one network.
    /// </summary>
    public static class CorePicker
    {
        public static int CubeDistance(int ax, int ay, int az, int bx, int by, int bz) =>
            Math.Max(Math.Abs(ax - bx), Math.Max(Math.Abs(ay - by), Math.Abs(az - bz)));

        /// <returns>Index into <paramref name="cores"/> of the owning Core, or -1 if none reaches.</returns>
        public static int Pick(IReadOnlyList<CoreSite> cores, int x, int y, int z)
        {
            int best = -1;
            int bestDist = int.MaxValue;
            for (int i = 0; i < cores.Count; i++)
            {
                CoreSite c = cores[i];
                int dist = CubeDistance(c.X, c.Y, c.Z, x, y, z);
                if (dist > c.Reach) continue;
                if (best >= 0 && !Better(dist, c, bestDist, cores[best])) continue;
                best = i;
                bestDist = dist;
            }
            return best;
        }

        private static bool Better(int dist, CoreSite c, int bestDist, CoreSite b)
        {
            if (dist != bestDist) return dist < bestDist;
            if (c.Y != b.Y) return c.Y < b.Y;
            if (c.X != b.X) return c.X < b.X;
            return c.Z < b.Z;
        }
    }
}
