using StashNet.Core.Stations;
using Xunit;

namespace StashNet.Core.Tests
{
    public class StationWorkTests
    {
        [Fact]
        public void HasWork_OnlyMetalTallies_False() => Assert.False(StationWork.HasWork(false, false, new[] { 0, 0, 0 }));

        [Fact]
        public void HasWork_RawOre_True() => Assert.True(StationWork.HasWork(false, false, new[] { 0, 12, 0 }));

        [Fact]
        public void HasWork_RawOreButSmelterDisabled_False() => Assert.False(StationWork.HasWork(false, true, new[] { 5, 0, 0 }));

        [Fact]
        public void HasWork_RecipeQueued_True() => Assert.True(StationWork.HasWork(true, true, null));

        [Fact]
        public void Decide_Manual_None() =>
            Assert.Equal(FireAction.None, StationWork.Decide(FuelModes.Manual, true, false, true, 100f, false));

        [Fact]
        public void Decide_IdleBurning_PutOut()
        {
            Assert.Equal(FireAction.PutOut, StationWork.Decide(FuelModes.Auto, true, false, true, 100f, false));
            Assert.Equal(FireAction.PutOut, StationWork.Decide("resourceCoal", true, false, true, 100f, false));
        }

        [Fact]
        public void Decide_WorkWithFuel_Light() =>
            Assert.Equal(FireAction.Light, StationWork.Decide(FuelModes.Auto, true, true, false, 30f, false));

        [Fact]
        public void Decide_WorkAlreadyBurning_None() =>
            Assert.Equal(FireAction.None, StationWork.Decide(FuelModes.Auto, true, true, true, 30f, false));

        [Fact]
        public void Decide_WorkNoFuel_None() =>
            Assert.Equal(FireAction.None, StationWork.Decide(FuelModes.Auto, true, true, false, 0f, false));

        [Fact]
        public void Decide_BesideWater_None() =>
            Assert.Equal(FireAction.None, StationWork.Decide(FuelModes.Auto, true, true, false, 30f, true));

        [Fact]
        public void Decide_NoFuelModule_None() =>
            Assert.Equal(FireAction.None, StationWork.Decide(FuelModes.Auto, false, false, true, 30f, false));
    }
}
