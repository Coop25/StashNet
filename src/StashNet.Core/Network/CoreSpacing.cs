using System.Collections.Generic;

namespace StashNet.Core.Network
{
    /// <summary>
    /// Owner's rule (2026-11-13): a Core may not be placed within twice the largest Core reach of any
    /// other Core (any tier, any owner), so two networks never overlap even once both are fully upgraded.
    /// </summary>
    public static class CoreSpacing
    {
        /// <summary>Closest two Cores may be is MinGap + 1: at MinGap their reaches would just touch.</summary>
        public static int MinGap(int maxReach) => 2 * maxReach;

        /// <param name="nearest">Cube distance to the nearest Core, or -1 if there is none.</param>
        /// <returns>True if a Core at (x, y, z) would be too close to one of <paramref name="cores"/>.</returns>
        public static bool TooClose(IReadOnlyList<CoreSite> cores, int x, int y, int z, int maxReach, out int nearest)
        {
            nearest = -1;
            for (int i = 0; i < cores.Count; i++)
            {
                int d = CorePicker.CubeDistance(cores[i].X, cores[i].Y, cores[i].Z, x, y, z);
                if (nearest < 0 || d < nearest) nearest = d;
            }
            return nearest >= 0 && nearest <= MinGap(maxReach);
        }
    }
}
