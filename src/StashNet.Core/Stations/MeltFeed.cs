using System;
using System.Collections.Generic;

namespace StashNet.Core.Stations
{
    /// <summary>One raw item that melts into a forge metal, as the feeder sees it right now.</summary>
    public readonly struct MeltRaw
    {
        public MeltRaw(string name, int weight, int stored, int stackSize, int partialRoom)
        {
            Name = name;
            Weight = weight;
            Stored = stored;
            StackSize = stackSize;
            PartialRoom = partialRoom;
        }

        public string Name { get; }
        /// <summary>Melted units one item adds (its Weight).</summary>
        public int Weight { get; }
        /// <summary>Plain stock in storage.</summary>
        public int Stored { get; }
        public int StackSize { get; }
        /// <summary>Free space in ore-slot stacks already holding this item.</summary>
        public int PartialRoom { get; }
    }

    /// <summary>How much raw scrap a forge pulls from storage to reach a melt target.</summary>
    public static class MeltFeed
    {
        /// <summary>Units still missing: the target (capped at the tally's max) minus the tally and what the ore slots will add.</summary>
        public static int UnitsWanted(int tally, int pendingUnits, int target, int unitMax) =>
            Math.Max(0, Math.Min(target, unitMax) - tally - pendingUnits);

        /// <summary>
        /// Raws in priority order: each gives ceil(left / weight), limited by storage and by ore-slot room
        /// (its partial stacks first, then empty slots, which are shared and used up in order).
        /// </summary>
        public static List<(string Name, int Count)> Plan(int unitsWanted, IReadOnlyList<MeltRaw> raws, int emptySlots)
        {
            var plan = new List<(string Name, int Count)>();
            int left = unitsWanted;
            foreach (MeltRaw raw in raws)
            {
                if (left <= 0) break;
                if (raw.Weight <= 0 || raw.Stored <= 0) continue;
                int stack = Math.Max(1, raw.StackSize);
                long room = Math.Max(0, raw.PartialRoom) + (long)Math.Max(0, emptySlots) * stack;
                int count = (int)Math.Min(Math.Min((left + raw.Weight - 1) / raw.Weight, raw.Stored), room);
                if (count <= 0) continue;
                int intoEmpty = Math.Max(0, count - Math.Max(0, raw.PartialRoom));
                emptySlots -= (intoEmpty + stack - 1) / stack;
                left -= count * raw.Weight;
                plan.Add((raw.Name, count));
            }
            return plan;
        }
    }

    /// <summary>Which stored quality items (no mods) a craft uses: lowest quality first.</summary>
    public static class QualityPick
    {
        public static List<(string Variant, int Count)> LowestFirst(IEnumerable<(string Variant, int Quality, int Count)> stored, int need)
        {
            var sorted = new List<(string Variant, int Quality, int Count)>(stored);
            sorted.Sort((a, b) => a.Quality != b.Quality ? a.Quality.CompareTo(b.Quality) : string.CompareOrdinal(a.Variant, b.Variant));
            var pick = new List<(string Variant, int Count)>();
            foreach (var s in sorted)
            {
                if (need <= 0) break;
                int take = Math.Min(need, s.Count);
                if (take <= 0) continue;
                pick.Add((s.Variant, take));
                need -= take;
            }
            return pick;
        }
    }
}
