using System;
using System.Collections.Generic;

namespace StashNet.Core.Network
{
    /// <summary>
    /// What station automation needs to know about one network without reading its drives: stored
    /// counts by item name (plain only, and all variants), free stacks, drive count and heat weight.
    /// Built from an open network (NetworkStorage, in memory, no drive reads) and shared by every station
    /// of that Core. Decisions use it; actually moving items always reads the real drives.
    /// </summary>
    public sealed class NetworkSummary
    {
        private readonly Dictionary<string, int> plain = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> total = new Dictionary<string, int>(StringComparer.Ordinal);

        public int FreeStacks { get; private set; }
        public int DriveCount { get; private set; }
        public float HeatWeight { get; private set; }

        public static NetworkSummary From(NetworkStorage storage, float heatWeight, int driveCount)
        {
            var s = new NetworkSummary { HeatWeight = heatWeight, DriveCount = driveCount };
            foreach (NetworkItem item in storage.Aggregate())
            {
                Add(s.total, item.Name, item.Count);
                if (item.Variant == null) Add(s.plain, item.Name, item.Count);
            }
            s.FreeStacks = Math.Max(0, storage.MaxStacks - storage.UsedStacks);
            return s;
        }

        private static void Add(Dictionary<string, int> d, string name, int count)
        {
            d.TryGetValue(name, out int n);
            d[name] = n + count;
        }

        /// <summary>Stored count of the plain (no quality, no mods) item.</summary>
        public int Plain(string name) => name != null && plain.TryGetValue(name, out int n) ? n : 0;

        /// <summary>Stored count of the item, every variant included.</summary>
        public int Total(string name) => name != null && total.TryGetValue(name, out int n) ? n : 0;

        /// <summary>Rebuild when marked stale, or as a backstop once it is older than <paramref name="backstopSeconds"/>.</summary>
        public static bool NeedsRebuild(bool stale, float builtAt, float now, float backstopSeconds) =>
            stale || now - builtAt > backstopSeconds;
    }
}
