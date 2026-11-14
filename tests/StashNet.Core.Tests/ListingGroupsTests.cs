using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class ListingGroupsTests
    {
        // Key "same" for variants a/b (they differ only by seed); others keep their own variant.
        private static string Key(ItemAmount a) => a.Variant == "a" || a.Variant == "b" ? "same" : a.Variant;

        [Fact]
        public void Fold_MergesSameKey()
        {
            var groups = ListingGroups.Fold(new[] { new ItemAmount("engine", "a", 1), new ItemAmount("engine", "b", 2) }, Key);
            ItemGroup g = Assert.Single(groups);
            Assert.Equal(3, g.Count);
            Assert.Equal(2, g.Members.Count);
            Assert.Equal(new ItemAmount("engine", "a", 3), g.Display);
        }

        [Fact]
        public void Fold_KeepsDifferentKeysApart()
        {
            var groups = ListingGroups.Fold(new[] { new ItemAmount("engine", "a", 1), new ItemAmount("engine", "c", 1) }, Key);
            Assert.Equal(2, groups.Count);
        }

        [Fact]
        public void Fold_SameKeyDifferentNamesApart()
        {
            var groups = ListingGroups.Fold(new[] { new ItemAmount("engine", "a", 1), new ItemAmount("dye", "b", 1) }, Key);
            Assert.Equal(new[] { "engine", "dye" }, groups.Select(g => g.Name));
        }

        [Fact]
        public void Fold_PlainItemsGroupByName()
        {
            var groups = ListingGroups.Fold(new[] { new ItemAmount("stone", null, 500) }, Key);
            Assert.Equal(new ItemAmount("stone", null, 500), Assert.Single(groups).Display);
        }

        [Fact]
        public void Split_TakesInOrder()
        {
            var members = new List<ItemAmount> { new ItemAmount("e", "a", 1), new ItemAmount("e", "b", 1), new ItemAmount("e", "c", 1) };
            Assert.Equal(new[] { new ItemAmount("e", "a", 1), new ItemAmount("e", "b", 1) }, ListingGroups.Split(members, 2));
        }

        [Fact]
        public void Split_CapsAtAvailable()
        {
            var members = new List<ItemAmount> { new ItemAmount("e", "a", 2) };
            Assert.Equal(new[] { new ItemAmount("e", "a", 2) }, ListingGroups.Split(members, 5));
        }

        [Fact]
        public void Split_ZeroWant_Empty()
        {
            var members = new List<ItemAmount> { new ItemAmount("e", "a", 2) };
            Assert.Empty(ListingGroups.Split(members, 0));
        }
    }
}
