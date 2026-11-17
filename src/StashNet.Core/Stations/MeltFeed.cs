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

        /// <summary>
        /// Most ore slots one metal may fill: all of them while only one melt needs items, one each while
        /// several do (owner, 2026-11-16; counted by active melts, not melts set up, since 2026-11-17).
        /// </summary>
        public static int SlotsPerMetal(int activeMelts, int oreSlots) => activeMelts >= 2 ? 1 : oreSlots;

        /// <summary>
        /// The ore slot to hand back to storage so a waiting metal gets its own slot: only when no slot is
        /// empty and some wanting metal holds none, the smallest stack of a metal that fills more than one
        /// slot. <paramref name="slotMetals"/>: each slot's metal, null = empty. <paramref name="movable"/>:
        /// slots automation may hand back (null = all); a player's own items that no melt target asks for
        /// are never moved (owner, 2026-11-17). Returns -1 for none.
        /// </summary>
        public static int SlotToFree(IReadOnlyList<string> slotMetals, IReadOnlyList<int> slotCounts, IEnumerable<string> wantingMetals, IReadOnlyList<bool> movable = null)
        {
            var held = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string m in slotMetals)
            {
                if (m == null) return -1; // an empty slot is free already
                held.TryGetValue(m, out int n);
                held[m] = n + 1;
            }
            bool someoneWaits = false;
            foreach (string w in wantingMetals)
                if (w != null && !held.ContainsKey(w)) { someoneWaits = true; break; }
            if (!someoneWaits) return -1;

            int best = -1;
            for (int i = 0; i < slotMetals.Count; i++)
            {
                if (held[slotMetals[i]] < 2 || (movable != null && !movable[i])) continue;
                if (best < 0 || slotCounts[i] < slotCounts[best]) best = i;
            }
            return best;
        }

        /// <summary>
        /// Splits <paramref name="count"/> items over ore slots (their current counts of this item, 0 = empty)
        /// so the slots end up as even as possible: each item goes to the lowest slot (ties: first slot),
        /// never past the stack size. Returns what to add per slot; anything that doesn't fit is left out.
        /// More filled slots = more melting at once.
        /// </summary>
        public static int[] Spread(int count, int[] slotCounts, int stackSize)
        {
            int[] now = (int[])slotCounts.Clone();
            int[] add = new int[now.Length];
            int stack = Math.Max(1, stackSize);
            for (int left = count; left > 0; left--)
            {
                int best = -1;
                for (int i = 0; i < now.Length; i++)
                {
                    if (now[i] >= stack) continue;
                    if (best < 0 || now[i] < now[best]) best = i;
                }
                if (best < 0) break;
                now[best]++;
                add[best]++;
            }
            return add;
        }
    }

    /// <summary>How much fuel the refill moves: top the chosen fuel up to the refill amount (at most a stack).</summary>
    public static class FuelRefill
    {
        public static int Want(int loaded, int room, int refillAmount, int stackSize) =>
            Math.Max(0, Math.Min(Math.Min(refillAmount, Math.Max(1, stackSize)) - loaded, room));
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
