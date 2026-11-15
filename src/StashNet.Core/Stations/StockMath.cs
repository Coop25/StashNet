using System;
using System.Collections.Generic;

namespace StashNet.Core.Stations
{
    public static class StockMath
    {
        /// <summary>Crafts that bring <paramref name="have"/> up to at least <paramref name="target"/>.</summary>
        public static int CraftsNeeded(int target, int have, int outputCount)
        {
            if (outputCount <= 0 || have >= target) return 0;
            long missing = (long)target - have;
            return (int)((missing + outputCount - 1) / outputCount);
        }

        /// <summary>Crafts the stored ingredients pay for (0 when there are no ingredients).</summary>
        public static int CraftsAffordable(IEnumerable<(int perCraft, int stored)> ingredients)
        {
            int best = int.MaxValue;
            bool any = false;
            foreach (var (perCraft, stored) in ingredients)
            {
                if (perCraft <= 0) continue;
                any = true;
                best = Math.Min(best, Math.Max(0, stored) / perCraft);
            }
            return any ? best : 0;
        }
    }
}
