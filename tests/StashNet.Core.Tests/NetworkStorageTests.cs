using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Drives;
using StashNet.Core.Network;
using Xunit;

namespace StashNet.Core.Tests
{
    public class NetworkStorageTests
    {
        private static NetworkDrive Drive(int maxTypes = 15, int maxItems = 1500, params (string name, int count)[] items)
        {
            var cap = new DriveCapacity(maxTypes, maxItems);
            var contents = new DriveContents();
            foreach (var (name, count) in items) contents.TryInsert(cap, name, null, count);
            return new NetworkDrive(contents, cap, null);
        }

        [Fact]
        public void NoDrives_BehavesEmpty()
        {
            var net = new NetworkStorage(new List<NetworkDrive>());
            Assert.Equal(0, net.Insert("wood", null, 10));
            Assert.Equal(0, net.Extract("wood", null, 10));
            Assert.Empty(net.Aggregate());
            Assert.Equal(0, net.MaxItems);
        }

        [Fact]
        public void Insert_PrefersDriveAlreadyHoldingItem()
        {
            var a = Drive(items: ("stone", 10));
            var b = Drive(items: ("wood", 10));
            var net = new NetworkStorage(new[] { a, b });
            Assert.Equal(5, net.Insert("wood", null, 5));
            Assert.Equal(15, b.Contents.CountOf("wood", null));
            Assert.Equal(0, a.Contents.CountOf("wood", null));
        }

        [Fact]
        public void Insert_NewType_GoesToDriveWithMostFreeRoom()
        {
            var a = Drive(items: ("stone", 1000));
            var b = Drive(items: ("iron", 100));
            var net = new NetworkStorage(new[] { a, b });
            net.Insert("wood", null, 5);
            Assert.Equal(5, b.Contents.CountOf("wood", null));
        }

        [Fact]
        public void Insert_SpillsOverToOtherDrives()
        {
            var a = Drive(maxItems: 100, items: ("wood", 90));
            var b = Drive(maxItems: 100);
            var net = new NetworkStorage(new[] { a, b });
            Assert.Equal(50, net.Insert("wood", null, 50));
            Assert.Equal(100, a.Contents.CountOf("wood", null));
            Assert.Equal(40, b.Contents.CountOf("wood", null));
        }

        [Fact]
        public void Insert_AllFull_ReturnsZero()
        {
            var a = Drive(maxItems: 10, items: ("wood", 10));
            var net = new NetworkStorage(new[] { a });
            Assert.Equal(0, net.Insert("wood", null, 5));
            Assert.False(a.Dirty);
        }

        [Fact]
        public void Extract_AcrossDrives()
        {
            var a = Drive(items: ("wood", 30));
            var b = Drive(items: ("wood", 30));
            var net = new NetworkStorage(new[] { a, b });
            Assert.Equal(50, net.Extract("wood", null, 50));
            Assert.Equal(10, a.Contents.CountOf("wood", null) + b.Contents.CountOf("wood", null));
            Assert.Equal(60, 50 + net.Aggregate().Single().Count);
        }

        [Fact]
        public void Extract_MoreThanStored_ReturnsStored()
        {
            var net = new NetworkStorage(new[] { Drive(items: ("wood", 7)) });
            Assert.Equal(7, net.Extract("wood", null, 100));
            Assert.Empty(net.Aggregate());
        }

        [Fact]
        public void Aggregate_SumsAcrossDrives_KeepsVariantsSeparate()
        {
            var cap = new DriveCapacity(15, 1500);
            var a = Drive(items: ("wood", 10));
            a.Contents.TryInsert(cap, "gun", "v1", 1);
            var b = Drive(items: ("wood", 5));
            b.Contents.TryInsert(cap, "gun", "v2", 1);
            var net = new NetworkStorage(new[] { a, b });

            var agg = net.Aggregate();
            Assert.Equal(3, agg.Count);
            Assert.Equal(15, agg.Single(i => i.Name == "wood").Count);
            Assert.Equal(2, agg.Count(i => i.Name == "gun"));
        }

        [Fact]
        public void Dirty_OnlyOnTouchedDrives()
        {
            var a = Drive(items: ("wood", 10));
            var b = Drive(items: ("stone", 10));
            var net = new NetworkStorage(new[] { a, b });
            net.Extract("wood", null, 1);
            Assert.True(a.Dirty);
            Assert.False(b.Dirty);
        }

        [Fact]
        public void Totals_AreSums()
        {
            var a = Drive(15, 1500, ("wood", 10));
            var b = Drive(40, 6000, ("stone", 20), ("iron", 5));
            var net = new NetworkStorage(new[] { a, b });
            Assert.Equal(55, net.MaxTypes);
            Assert.Equal(7500, net.MaxItems);
            Assert.Equal(3, net.UsedTypes);
            Assert.Equal(35, net.UsedItems);
        }
    }
}
