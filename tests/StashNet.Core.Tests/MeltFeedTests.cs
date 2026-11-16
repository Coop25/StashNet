using System.Collections.Generic;
using StashNet.Core.Stations;
using Xunit;

namespace StashNet.Core.Tests
{
    public class MeltFeedTests
    {
        private static MeltRaw Raw(string name, int weight, int stored, int stackSize = 6000, int partialRoom = 0) =>
            new MeltRaw(name, weight, stored, stackSize, partialRoom);

        [Fact]
        public void UnitsWantedStopsAtTarget() => Assert.Equal(0, MeltFeed.UnitsWanted(2000, 0, 2000, 30000));

        [Fact]
        public void UnitsWantedCountsPending() => Assert.Equal(500, MeltFeed.UnitsWanted(1000, 500, 2000, 30000));

        [Fact]
        public void UnitsWantedCapsAtUnitMax() => Assert.Equal(10, MeltFeed.UnitsWanted(29990, 0, 1000000, 30000));

        [Fact]
        public void UnitsWantedNeverNegative() => Assert.Equal(0, MeltFeed.UnitsWanted(3000, 100, 2000, 30000));

        [Fact]
        public void PlanUsesFirstRawFirst()
        {
            var plan = MeltFeed.Plan(40, new List<MeltRaw> { Raw("resourceBrokenGlass", 4, 5), Raw("resourceCrushedSand", 4, 100) }, 3);
            Assert.Equal(new[] { ("resourceBrokenGlass", 5), ("resourceCrushedSand", 5) }, plan);
        }

        [Fact]
        public void PlanRoundsUp()
        {
            var plan = MeltFeed.Plan(10, new List<MeltRaw> { Raw("resourceClayLump", 4, 100) }, 1);
            Assert.Equal(new[] { ("resourceClayLump", 3) }, plan);
        }

        [Fact]
        public void PlanLimitedBySlots()
        {
            var plan = MeltFeed.Plan(100, new List<MeltRaw> { Raw("resourceScrapIron", 1, 1000, partialRoom: 2) }, 0);
            Assert.Equal(new[] { ("resourceScrapIron", 2) }, plan);
        }

        [Fact]
        public void PlanEmptySlotsSharedBetweenRaws()
        {
            // One empty slot: the first raw takes it, the second has nowhere to go.
            var plan = MeltFeed.Plan(20, new List<MeltRaw> { Raw("a", 1, 10, stackSize: 10), Raw("b", 1, 100, stackSize: 10) }, 1);
            Assert.Equal(new[] { ("a", 10) }, plan);
        }

        [Fact]
        public void PlanPartialStorage()
        {
            var plan = MeltFeed.Plan(10, new List<MeltRaw> { Raw("resourceScrapLead", 1, 3) }, 3);
            Assert.Equal(new[] { ("resourceScrapLead", 3) }, plan);
        }

        [Fact]
        public void PlanNothingWanted()
        {
            Assert.Empty(MeltFeed.Plan(0, new List<MeltRaw> { Raw("resourceScrapIron", 1, 100) }, 3));
        }
    }

    public class QualityPickTests
    {
        [Fact]
        public void LowestFirstPicksLowQuality()
        {
            var pick = QualityPick.LowestFirst(new[] { ("q4", 4, 1), ("q1", 1, 1) }, 1);
            Assert.Equal(new[] { ("q1", 1) }, pick);
        }

        [Fact]
        public void LowestFirstShort()
        {
            var pick = QualityPick.LowestFirst(new[] { ("q2", 2, 1) }, 2);
            Assert.Equal(new[] { ("q2", 1) }, pick);
        }

        [Fact]
        public void LowestFirstSpansVariants()
        {
            var pick = QualityPick.LowestFirst(new[] { ("b", 3, 2), ("a", 3, 1), ("c", 5, 4) }, 4);
            Assert.Equal(new[] { ("a", 1), ("b", 2), ("c", 1) }, pick);
        }
    }
}

namespace StashNet.Core.Tests
{
    public class MeltSpreadTests
    {
        [Fact]
        public void SplitsEvenlyIntoTwoEmptySlots()
        {
            Assert.Equal(new[] { 50, 50 }, MeltFeed.Spread(100, new[] { 0, 0 }, 6000));
        }

        [Fact]
        public void EvensOutAnExistingStackFirst()
        {
            // Slot 0 already holds 40 of this item, slot 1 is empty: end with 70 / 70.
            Assert.Equal(new[] { 30, 70 }, MeltFeed.Spread(100, new[] { 40, 0 }, 6000));
        }

        [Fact]
        public void OddAmountGoesToTheFirstSlots()
        {
            Assert.Equal(new[] { 51, 50 }, MeltFeed.Spread(101, new[] { 0, 0 }, 6000));
        }

        [Fact]
        public void RespectsStackSize()
        {
            Assert.Equal(new[] { 10, 20 }, MeltFeed.Spread(30, new[] { 90, 80 }, 100));
        }

        [Fact]
        public void LeftoverWhenNoRoom()
        {
            int[] add = MeltFeed.Spread(50, new[] { 95 }, 100);
            Assert.Equal(new[] { 5 }, add);
        }

        [Fact]
        public void PlanCanUseEverySharedEmptySlot()
        {
            // The plan itself is unchanged: empty slots are room; the forge decides which slots to spread over.
            var plan = MeltFeed.Plan(100, new List<MeltRaw> { new MeltRaw("resourceScrapIron", 1, 1000, 6000, 0) }, 2);
            Assert.Equal(new[] { ("resourceScrapIron", 100) }, plan);
        }
    }

    public class FuelRefillTests
    {
        [Fact]
        public void TopsUpToTheRefillAmount() => Assert.Equal(70, FuelRefill.Want(loaded: 30, room: 5000, refillAmount: 100, stackSize: 1000));

        [Fact]
        public void NothingWhenAlreadyEnough() => Assert.Equal(0, FuelRefill.Want(loaded: 120, room: 5000, refillAmount: 100, stackSize: 1000));

        [Fact]
        public void NeverMoreThanAStackOrTheRoom()
        {
            Assert.Equal(50, FuelRefill.Want(loaded: 0, room: 5000, refillAmount: 100, stackSize: 50));
            Assert.Equal(20, FuelRefill.Want(loaded: 0, room: 20, refillAmount: 100, stackSize: 1000));
        }
    }
}
