using System;
using StashNet.Core.Drives;
using Xunit;

namespace StashNet.Core.Tests
{
    public class DriveContentsTests
    {
        // Basic Drive: 24 stacks.
        private static readonly DriveCapacity Basic = new DriveCapacity(24);

        // Test stack sizes: stone 6000, wood 500, nails 500, guns 1; anything else 100.
        internal static readonly Func<string, int> Sizes = name =>
            name == "stone" ? 6000 : name == "wood" || name == "nails" ? 500 : name.StartsWith("gun") ? 1 : 100;

        [Fact]
        public void Insert_IntoEmpty_AcceptsAll()
        {
            var d = new DriveContents();
            Assert.Equal(100, d.TryInsert(Basic, Sizes, "wood", null, 100));
            Assert.Equal(1, d.TypeCount);
            Assert.Equal(100, d.TotalCount);
            Assert.Equal(1, d.UsedStacks(Sizes));
        }

        [Fact]
        public void BigStackResource_UsesOneStackPer6000()
        {
            var d = new DriveContents();
            Assert.Equal(6000, d.TryInsert(Basic, Sizes, "stone", null, 6000));
            Assert.Equal(1, d.UsedStacks(Sizes));
            d.TryInsert(Basic, Sizes, "stone", null, 1);
            Assert.Equal(2, d.UsedStacks(Sizes));
        }

        [Fact]
        public void UnstackableItems_UseOneStackEach()
        {
            var d = new DriveContents();
            Assert.Equal(3, d.TryInsert(Basic, Sizes, "gunRifle", null, 3));
            Assert.Equal(3, d.UsedStacks(Sizes));
        }

        [Fact]
        public void Insert_RespectsMaxStacks()
        {
            var d = new DriveContents();
            // 23 stacks of wood = 11,500
            Assert.Equal(11500, d.TryInsert(Basic, Sizes, "wood", null, 11500));
            // 1 stack left: only 6000 stone fit
            Assert.Equal(6000, d.TryInsert(Basic, Sizes, "stone", null, 10000));
            Assert.Equal(24, d.UsedStacks(Sizes));
            Assert.Equal(0, d.TryInsert(Basic, Sizes, "nails", null, 1));
        }

        [Fact]
        public void Insert_FillsPartialStackEvenWhenStacksFull()
        {
            var cap = new DriveCapacity(1);
            var d = new DriveContents();
            d.TryInsert(cap, Sizes, "wood", null, 300);
            Assert.Equal(200, d.TryInsert(cap, Sizes, "wood", null, 1000));
            Assert.Equal(500, d.CountOf("wood", null));
        }

        [Fact]
        public void NoTypeLimit_ManyTypesFitWhileStacksRemain()
        {
            var d = new DriveContents();
            for (int i = 0; i < 24; i++) Assert.Equal(1, d.TryInsert(Basic, Sizes, "item" + i, null, 1));
            Assert.Equal(24, d.TypeCount);
            Assert.Equal(0, d.TryInsert(Basic, Sizes, "item24", null, 1));
        }
        [Fact]
        public void Insert_SameNameDifferentVariant_IsSeparateType()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, Sizes, "gunPistol", null, 1);
            d.TryInsert(Basic, Sizes, "gunPistol", "v1", 1);
            d.TryInsert(Basic, Sizes, "gunPistol", "v2", 1);
            d.TryInsert(Basic, Sizes, "gunPistol", "v1", 1);
            Assert.Equal(3, d.TypeCount);
            Assert.Equal(2, d.CountOf("gunPistol", "v1"));
            Assert.Equal(4, d.UsedStacks(Sizes));
        }

        [Fact]
        public void UnknownOrZeroStackSize_CountsAsOne()
        {
            var d = new DriveContents();
            Func<string, int> weird = _ => 0;
            Assert.Equal(2, d.TryInsert(new DriveCapacity(2), weird, "x", null, 5));
            Assert.Equal(2, d.UsedStacks(weird));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Insert_ZeroOrNegative_ReturnsZero(int count)
        {
            var d = new DriveContents();
            Assert.Equal(0, d.TryInsert(Basic, Sizes, "wood", null, count));
            Assert.Equal(0, d.TypeCount);
        }

        [Fact]
        public void Extract_Partial_KeepsEntry()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, Sizes, "wood", null, 100);
            Assert.Equal(30, d.Extract("wood", null, 30));
            Assert.Equal(70, d.CountOf("wood", null));
        }

        [Fact]
        public void Extract_All_RemovesEntry()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, Sizes, "wood", null, 100);
            Assert.Equal(100, d.Extract("wood", null, 100));
            Assert.Equal(0, d.TypeCount);
            Assert.Equal(0, d.UsedStacks(Sizes));
        }

        [Fact]
        public void Extract_MoreThanStored_ReturnsStored()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, Sizes, "wood", null, 10);
            Assert.Equal(10, d.Extract("wood", null, 50));
        }

        [Fact]
        public void Extract_Missing_ReturnsZero()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, Sizes, "wood", null, 10);
            Assert.Equal(0, d.Extract("iron", null, 5));
            Assert.Equal(0, d.Extract("wood", "variant", 5));
            Assert.Equal(10, d.TotalCount);
        }

        [Fact]
        public void OverCapacity_ExistingItemsKept_InsertRefused()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, Sizes, "wood", null, 5000);
            d.TryInsert(Basic, Sizes, "nails", null, 2000);
            var smaller = new DriveCapacity(2);
            Assert.Equal(0, d.TryInsert(smaller, Sizes, "nails", null, 1));
            Assert.Equal(0, d.TryInsert(smaller, Sizes, "c", null, 1));
            Assert.Equal(2, d.TypeCount);
            Assert.Equal(7000, d.TotalCount);
        }
    }
}
