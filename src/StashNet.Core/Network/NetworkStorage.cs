using System;
using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Drives;

namespace StashNet.Core.Network
{
    /// <summary>One drive in a network, plus a tag the game layer uses to find its bay slot.</summary>
    public sealed class NetworkDrive
    {
        public NetworkDrive(DriveContents contents, DriveCapacity capacity, object tag)
        {
            Contents = contents;
            Capacity = capacity;
            Tag = tag;
        }

        public DriveContents Contents { get; }
        public DriveCapacity Capacity { get; }
        public object Tag { get; }

        /// <summary>True once this drive's contents changed and need saving.</summary>
        public bool Dirty { get; internal set; }
    }

    /// <summary>An item as the terminal shows it: totals merged across all drives.</summary>
    public sealed class NetworkItem
    {
        public NetworkItem(string name, string variant, int count)
        {
            Name = name;
            Variant = variant;
            Count = count;
        }

        public string Name { get; }
        public string Variant { get; }
        public int Count { get; internal set; }
    }

    /// <summary>Treats several drives as one storage pool.</summary>
    public sealed class NetworkStorage
    {
        private readonly IReadOnlyList<NetworkDrive> drives;
        private readonly Func<string, int> stackSizeOf;

        public NetworkStorage(IReadOnlyList<NetworkDrive> drives, Func<string, int> stackSizeOf)
        {
            this.drives = drives;
            this.stackSizeOf = stackSizeOf;
        }

        public IReadOnlyList<NetworkDrive> Drives => drives;
        public int MaxTypes => drives.Sum(d => d.Capacity.MaxTypes);
        public int MaxStacks => drives.Sum(d => d.Capacity.MaxStacks);
        public int UsedTypes => drives.Sum(d => d.Contents.TypeCount);
        public int UsedStacks => drives.Sum(d => d.Contents.UsedStacks(stackSizeOf));

        /// <summary>
        /// Stores up to <paramref name="count"/>: drives already holding this exact item first,
        /// then the drive with the most free stacks. Returns how many were accepted.
        /// </summary>
        public int Insert(string name, string variant, int count)
        {
            int remaining = count;
            var holding = drives.Where(d => d.Contents.CountOf(name, variant) > 0).ToList();
            foreach (NetworkDrive d in holding)
            {
                if (remaining <= 0) break;
                remaining -= InsertInto(d, name, variant, remaining);
            }

            // OrderBy is stable, so equal free room keeps bay order.
            var others = drives.Except(holding)
                .OrderByDescending(d => d.Capacity.MaxStacks - d.Contents.UsedStacks(stackSizeOf))
                .ToList();
            foreach (NetworkDrive d in others)
            {
                if (remaining <= 0) break;
                remaining -= InsertInto(d, name, variant, remaining);
            }
            return count - remaining;
        }

        /// <summary>Removes up to <paramref name="count"/>, drive by drive. Returns how many were removed.</summary>
        public int Extract(string name, string variant, int count)
        {
            int remaining = count;
            foreach (NetworkDrive d in drives)
            {
                if (remaining <= 0) break;
                int removed = d.Contents.Extract(name, variant, remaining);
                if (removed > 0)
                {
                    d.Dirty = true;
                    remaining -= removed;
                }
            }
            return count - remaining;
        }

        public IReadOnlyList<NetworkItem> Aggregate()
        {
            var result = new List<NetworkItem>();
            var index = new Dictionary<(string, string), NetworkItem>();
            foreach (NetworkDrive d in drives)
            {
                foreach (DriveEntry e in d.Contents.Entries)
                {
                    var key = (e.Name, e.Variant);
                    if (index.TryGetValue(key, out NetworkItem item))
                    {
                        item.Count += e.Count;
                    }
                    else
                    {
                        item = new NetworkItem(e.Name, e.Variant, e.Count);
                        index.Add(key, item);
                        result.Add(item);
                    }
                }
            }
            return result;
        }

        private int InsertInto(NetworkDrive d, string name, string variant, int count)
        {
            int accepted = d.Contents.TryInsert(d.Capacity, stackSizeOf, name, variant, count);
            if (accepted > 0) d.Dirty = true;
            return accepted;
        }
    }
}
