using System;
using System.Collections.Generic;

namespace StashNet.Core.Drives
{
    /// <summary>
    /// What's stored on one drive. Capacity and stack sizes are passed in per call (they come
    /// from the game's item config), so changing them later never deletes anything - at most it
    /// blocks further inserts.
    /// </summary>
    public sealed class DriveContents
    {
        private readonly List<DriveEntry> entries = new List<DriveEntry>();

        public IReadOnlyList<DriveEntry> Entries => entries;
        public int TypeCount => entries.Count;
        public int TotalCount { get; private set; }

        /// <summary>Stacks in use: per entry, ceil(count / stack size).</summary>
        public int UsedStacks(Func<string, int> stackSizeOf)
        {
            int used = 0;
            foreach (DriveEntry e in entries) used += StacksFor(e.Count, SizeOf(stackSizeOf, e.Name));
            return used;
        }

        /// <summary>Stores up to <paramref name="count"/> items. Returns how many were accepted (0..count).</summary>
        public int TryInsert(DriveCapacity capacity, Func<string, int> stackSizeOf, string name, string variant, int count)
        {
            if (count <= 0 || string.IsNullOrEmpty(name)) return 0;

            int size = SizeOf(stackSizeOf, name);
            long freeStacks = Math.Max(0, capacity.MaxStacks - UsedStacks(stackSizeOf));
            DriveEntry entry = Find(name, variant);

            long partialRoom = entry == null ? 0 : (long)StacksFor(entry.Count, size) * size - entry.Count;
            long room = partialRoom + freeStacks * size;

            int accepted = (int)Math.Min(count, room);
            if (accepted <= 0) return 0;

            if (entry == null)
            {
                entries.Add(new DriveEntry(name, variant, accepted));
            }
            else
            {
                entry.Count += accepted;
            }
            TotalCount += accepted;
            return accepted;
        }

        /// <summary>Removes up to <paramref name="count"/> items. Returns how many were removed.</summary>
        public int Extract(string name, string variant, int count)
        {
            if (count <= 0) return 0;
            DriveEntry entry = Find(name, variant);
            if (entry == null) return 0;

            int removed = Math.Min(count, entry.Count);
            entry.Count -= removed;
            TotalCount -= removed;
            if (entry.Count == 0) entries.Remove(entry);
            return removed;
        }

        public int CountOf(string name, string variant) => Find(name, variant)?.Count ?? 0;

        /// <summary>
        /// Adds an entry ignoring capacity. Used when loading, and when putting items back onto the
        /// drive they came from (stored data is never dropped).
        /// </summary>
        public void AddLoaded(string name, string variant, int count)
        {
            DriveEntry entry = Find(name, variant);
            if (entry == null)
            {
                entries.Add(new DriveEntry(name, variant, count));
            }
            else
            {
                entry.Count += count;
            }
            TotalCount += count;
        }

        /// <summary>
        /// Turns variant entries that <paramref name="isPlain"/> says are really plain into plain entries
        /// (merged with an existing plain entry of the same name). Counts never change and merging only
        /// frees stacks. Returns true if anything changed.
        /// </summary>
        public bool MakePlain(Func<string, string, bool> isPlain)
        {
            bool changed = false;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                DriveEntry e = entries[i];
                if (e.Variant == null || !isPlain(e.Name, e.Variant)) continue;
                entries.RemoveAt(i);
                TotalCount -= e.Count;
                AddLoaded(e.Name, null, e.Count);
                changed = true;
            }
            return changed;
        }

        /// <summary>Unknown or invalid stack sizes count as 1 (the conservative choice).</summary>
        internal static int SizeOf(Func<string, int> stackSizeOf, string name)
        {
            int size = stackSizeOf?.Invoke(name) ?? 1;
            return size < 1 ? 1 : size;
        }

        private static int StacksFor(int count, int size) => (count + size - 1) / size;

        private DriveEntry Find(string name, string variant)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Matches(name, variant)) return entries[i];
            }
            return null;
        }
    }
}
