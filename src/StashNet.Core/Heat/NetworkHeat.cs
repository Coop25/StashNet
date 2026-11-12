using System;

namespace StashNet.Core.Heat
{
    /// <summary>How much heat a network reports each time (Milestone 7). Numbers come from the Core block's XML.</summary>
    public static class NetworkHeat
    {
        /// <summary>drives × heatPerDrive, capped. Bad (negative) inputs count as 0, so a bad config never crashes.</summary>
        public static float PerReport(int drives, float heatPerDrive, float cap)
        {
            if (drives <= 0 || heatPerDrive <= 0f || cap <= 0f) return 0f;
            return Math.Min(drives * heatPerDrive, cap);
        }
    }
}
