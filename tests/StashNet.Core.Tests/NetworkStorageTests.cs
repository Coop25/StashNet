using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Drives;
using StashNet.Core.Network;
using Xunit;

namespace StashNet.Core.Tests
{
    public class NetworkStorageTests
    {
        private static readonly System.Func<string, int> Sizes = DriveContentsTests.Sizes;

        private static NetworkDrive Drive(int maxStacks = 24, params (string name, int count)[] items)
        {
            var cap = new DriveCapacity(maxStacks);
            var contents = new DriveContents();
            foreach (var (name, count) in items) contents.TryInsert(cap, Sizes, name, null, count);
            return new NetworkDrive(contents, cap, null);
        }

        private static NetworkStorage Net(params NetworkDrive[] drives) => new NetworkStorage(drives, Sizes);

        [Fact]
        public void NoDrives_BehavesEmpty()
        {
            var net = new NetworkStorage(new List<NetworkDrive>(), Sizes);
            Assert.Equal(0, net.Insert("wood", null, 10));
            Assert.Equal(0, net.Extract("wood", null, 10));
            Assert.Empty(net.Aggregate());
            Assert.Equal(0, net.MaxStacks);
        }

        [Fact]
        public void Insert_PrefersDriveAlreadyHoldingItem()
        {
            var a = Drive(items: ("nails", 10));
            var b = Drive(items: ("wood", 10));
            var net = Net(a, b);
            Assert.Equal(5, net.Insert("wood", null, 5));
            Assert.Equal(15, b.Contents.CountOf("wood", null));
            Assert.Equal(0, a.Contents.CountOf("wood", null));
        }

        [Fact]
        public void Insert_NewType_GoesToDriveWithMostFreeStacks()
        {
            var a = Drive(items: ("wood", 5000)); // 10 stacks used
            var b = Drive(items: ("wood", 500));  // 1 stack used
            var net = Net(a, b);
            net.Insert("nails", null, 5);
            Assert.Equal(5, b.Contents.CountOf("nails", null));
        }

        [Fact]
        public void Insert_SpillsOverToOtherDrives()
        {
            var a = Drive(maxStacks: 1, items: ("wood", 450));
            var b = Drive(maxStacks: 1);
            var net = Net(a, b);
            Assert.Equal(300, net.Insert("wood", null, 300));
            Assert.Equal(500, a.Contents.CountOf("wood", null));
            Assert.Equal(250, b.Contents.CountOf("wood", null));
        }

        [Fact]
        public void Insert_AllFull_ReturnsZero()
        {
            var a = Drive(maxStacks: 1, items: ("wood", 500));
            var net = Net(a);
            Assert.Equal(0, net.Insert("wood", null, 5));
            Assert.False(a.Dirty);
        }

        [Fact]
        public void Extract_AcrossDrives()
        {
            var a = Drive(items: ("wood", 30));
            var b = Drive(items: ("wood", 30));
            var net = Net(a, b);
            Assert.Equal(50, net.Extract("wood", null, 50));
            Assert.Equal(10, net.Aggregate().Single().Count);
        }

        [Fact]
        public void Extract_MoreThanStored_ReturnsStored()
        {
            var net = Net(Drive(items: ("wood", 7)));
            Assert.Equal(7, net.Extract("wood", null, 100));
            Assert.Empty(net.Aggregate());
        }

        [Fact]
        public void Aggregate_SumsAcrossDrives_KeepsVariantsSeparate()
        {
            var cap = new DriveCapacity(24);
            var a = Drive(items: ("wood", 10));
            a.Contents.TryInsert(cap, Sizes, "gun", "v1", 1);
            var b = Drive(items: ("wood", 5));
            b.Contents.TryInsert(cap, Sizes, "gun", "v2", 1);
            var net = Net(a, b);

            var agg = net.Aggregate();
            Assert.Equal(3, agg.Count);
            Assert.Equal(15, agg.Single(i => i.Name == "wood").Count);
            Assert.Equal(2, agg.Count(i => i.Name == "gun"));
        }

        [Fact]
        public void Dirty_OnlyOnTouchedDrives()
        {
            var a = Drive(items: ("wood", 10));
            var b = Drive(items: ("nails", 10));
            var net = Net(a, b);
            net.Extract("wood", null, 1);
            Assert.True(a.Dirty);
            Assert.False(b.Dirty);
        }

        [Fact]
        public void Totals_AreSums()
        {
            var a = Drive(24, ("wood", 10));
            var b = Drive(96, ("stone", 6001), ("nails", 5));
            var net = Net(a, b);
            Assert.Equal(120, net.MaxStacks);
            Assert.Equal(4, net.UsedStacks);
            Assert.Equal(3, net.DistinctItems);
        }

        [Fact]
        public void DistinctItems_CountsItemSplitAcrossDrivesOnce()
        {
            var a = Drive(maxStacks: 1, items: ("wood", 500));
            var b = Drive(maxStacks: 1, items: ("wood", 100));
            var net = Net(a, b);
            Assert.Equal(1, net.DistinctItems);
            Assert.Equal(2, net.UsedStacks);
        }
    }
}
