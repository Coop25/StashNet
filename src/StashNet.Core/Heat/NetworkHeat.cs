using System;

namespace StashNet.Core.Heat
{
    /// <summary>How much heat a network reports each time (Milestones 7-8). Numbers come from the Core block's XML.</summary>
    public static class NetworkHeat
    {
        /// <summary>
        /// totalWeight × heatPerDrive, capped. totalWeight is the sum of every drive's heat weight
        /// (1K 1, 4K 2, 16K 4, 64K 6, 256K 8, from items.xml), so bigger drives run hotter (M8).
        /// Bad (negative) inputs count as 0, so a bad config never crashes.
        /// </summary>
        public static float PerReport(float totalWeight, float heatPerDrive, float cap)
        {
            if (totalWeight <= 0f || heatPerDrive <= 0f || cap <= 0f) return 0f;
            return Math.Min(totalWeight * heatPerDrive, cap);
        }
    }
}
