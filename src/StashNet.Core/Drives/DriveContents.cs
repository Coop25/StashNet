using System;
using System.Collections.Generic;

namespace StashNet.Core.Drives
{
    /// <summary>
    /// What's stored on one drive. Capacity is passed in per call (it comes from the drive item's
    /// config), so lowering it later never deletes anything - it only blocks further inserts.
    /// </summary>
    public sealed class DriveContents
    {
        private readonly List<DriveEntry> entries = new List<DriveEntry>();

        public IReadOnlyList<DriveEntry> Entries => entries;
        public int TypeCount => entries.Count;
        public int TotalCount { get; private set; }

        /// <summary>Stores up to <paramref name="count"/> items. Returns how many were accepted (0..count).</summary>
        public int TryInsert(DriveCapacity capacity, string name, string variant, int count)
        {
            if (count <= 0 || string.IsNullOrEmpty(name)) return 0;
            int room = capacity.MaxItems - TotalCount;
            if (room <= 0) return 0;

            DriveEntry entry = Find(name, variant);
            if (entry == null && TypeCount >= capacity.MaxTypes) return 0;

            int accepted = Math.Min(count, room);
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

        /// <summary>Adds an entry while loading, ignoring capacity (stored data is never dropped).</summary>
        internal void AddLoaded(string name, string variant, int count)
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
