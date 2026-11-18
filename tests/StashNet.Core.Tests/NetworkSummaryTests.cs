using System.Collections.Generic;
using StashNet.Core.Drives;
using StashNet.Core.Network;
using Xunit;

namespace StashNet.Core.Tests
{
    public class NetworkSummaryTests
    {
        private static NetworkStorage Storage()
        {
            var a = new DriveContents();
            a.AddLoaded("resourceScrapIron", null, 250);
            a.AddLoaded("toolPickaxe", "v1", 2);
            var b = new DriveContents();
            b.AddLoaded("resourceScrapIron", null, 50);
            b.AddLoaded("toolPickaxe", null, 1);
            var drives = new List<NetworkDrive>
            {
                new NetworkDrive(a, new DriveCapacity(10), null),
                new NetworkDrive(b, new DriveCapacity(10), null),
            };
            return new NetworkStorage(drives, name => name == "resourceScrapIron" ? 100 : 1);
        }

        [Fact]
        public void CountsPlainAndTotalSeparately()
        {
            NetworkSummary s = NetworkSummary.From(Storage(), 2f, 2);
            Assert.Equal(300, s.Plain("resourceScrapIron"));
            Assert.Equal(1, s.Plain("toolPickaxe"));
            Assert.Equal(3, s.Total("toolPickaxe"));
            Assert.Equal(0, s.Plain("resourceCoal"));
        }

        [Fact]
        public void FreeStacksIsMaxMinusUsed()
        {
            // 300 scrap = 3 + 1 stacks (250 → 3, 50 → 1), pickaxes = 3 stacks; 20 - 7 = 13.
            NetworkSummary s = NetworkSummary.From(Storage(), 2f, 2);
            Assert.Equal(13, s.FreeStacks);
            Assert.Equal(2f, s.HeatWeight);
            Assert.Equal(2, s.DriveCount);
        }

        [Fact]
        public void UsedAndMaxStacks()
        {
            NetworkSummary s = NetworkSummary.From(Storage(), 2f, 2);
            Assert.Equal(7, s.UsedStacks);
            Assert.Equal(20, s.MaxStacks);
        }

        [Fact]
        public void CountsFullDrives()
        {
            var full = new DriveContents();
            full.AddLoaded("a", null, 3);
            var half = new DriveContents();
            half.AddLoaded("a", null, 1);
            var over = new DriveContents();
            over.AddLoaded("a", null, 4);
            var drives = new List<NetworkDrive>
            {
                new NetworkDrive(full, new DriveCapacity(3), null),
                new NetworkDrive(half, new DriveCapacity(3), null),
            };
            Assert.Equal(1, NetworkSummary.From(new NetworkStorage(drives, n => 1), 1f, 2).FullDrives);
            drives.Add(new NetworkDrive(over, new DriveCapacity(3), null));
            Assert.Equal(2, NetworkSummary.From(new NetworkStorage(drives, n => 1), 1f, 3).FullDrives);
        }

        [Fact]
        public void NeedsRebuildWhenStaleOrOld()
        {
            Assert.True(NetworkSummary.NeedsRebuild(stale: true, builtAt: 100f, now: 101f, backstopSeconds: 60f));
            Assert.True(NetworkSummary.NeedsRebuild(stale: false, builtAt: 100f, now: 161f, backstopSeconds: 60f));
            Assert.False(NetworkSummary.NeedsRebuild(stale: false, builtAt: 100f, now: 105f, backstopSeconds: 60f));
        }
    }
}
