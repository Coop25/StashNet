using System;
using System.Collections.Generic;

namespace StashNet.Core.Protocol
{
    /// <summary>
    /// One terminal cell: stored entries that look identical (same name and group key, e.g. engines
    /// that differ only by the game's hidden seed), shown as one pile. The members stay separate in
    /// storage; a withdraw takes from them in order (<see cref="ListingGroups.Split"/>).
    /// </summary>
    public sealed class ItemGroup
    {
        public ItemGroup(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public int Count { get; internal set; }
        public List<ItemAmount> Members { get; } = new List<ItemAmount>();

        /// <summary>What the cell shows: the first member's look with the group's total count.</summary>
        public ItemAmount Display => new ItemAmount(Name, Members.Count > 0 ? Members[0].Variant : null, Count);
    }

    public static class ListingGroups
    {
        /// <summary>Groups the listing by (name, keyOf(item)), keeping first-seen order.</summary>
        public static List<ItemGroup> Fold(IEnumerable<ItemAmount> listing, Func<ItemAmount, string> keyOf)
        {
            var result = new List<ItemGroup>();
            var index = new Dictionary<(string, string), ItemGroup>();
            foreach (ItemAmount item in listing)
            {
                var key = (item.Name, keyOf(item));
                if (!index.TryGetValue(key, out ItemGroup group))
                {
                    group = new ItemGroup(item.Name);
                    index.Add(key, group);
                    result.Add(group);
                }
                group.Members.Add(item);
                group.Count += item.Count;
            }
            return result;
        }

        /// <summary>Shares of <paramref name="want"/> taken from the members in order, never more than each holds.</summary>
        public static List<ItemAmount> Split(IReadOnlyList<ItemAmount> members, int want)
        {
            var shares = new List<ItemAmount>();
            foreach (ItemAmount m in members)
            {
                if (want <= 0) break;
                int take = Math.Min(want, m.Count);
                if (take <= 0) continue;
                shares.Add(new ItemAmount(m.Name, m.Variant, take));
                want -= take;
            }
            return shares;
        }
    }
}
