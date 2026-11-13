using StashNet.Core.Heat;
using Xunit;

namespace StashNet.Core.Tests
{
    public class HeatTests
    {
        [Fact]
        public void PerReport_ScalesWithDrives() => Assert.Equal(3f, NetworkHeat.PerReport(12, 0.25f, 6.5f));

        [Fact]
        public void PerReport_Capped() => Assert.Equal(6.5f, NetworkHeat.PerReport(40, 0.25f, 6.5f));

        [Fact]
        public void PerReport_NoDrives_Zero() => Assert.Equal(0f, NetworkHeat.PerReport(0, 0.25f, 6.5f));

        // M8: heat counts drive size (Basic 1, Standard 2, High-Density 4).
        [Fact]
        public void PerReport_WeightedTotal() => Assert.Equal(5.76f, NetworkHeat.PerReport(32f, 0.18f, 6.5f), 3); // 16 Standard on a Storage Core

        [Fact]
        public void PerReport_JustUnderCap() => Assert.Equal(6.48f, NetworkHeat.PerReport(54f, 0.12f, 6.5f), 3); // Advanced Core

        [Fact]
        public void PerReport_OverCap_Capped() => Assert.Equal(6.5f, NetworkHeat.PerReport(56f, 0.12f, 6.5f), 3);

        [Fact]
        public void PerReport_FractionalWeight() => Assert.Equal(0.125f, NetworkHeat.PerReport(0.5f, 0.25f, 6.5f), 4);

        [Fact]
        public void PerReport_BadInputs_ClampToZero()
        {
            Assert.Equal(0f, NetworkHeat.PerReport(-3, 0.25f, 6.5f));
            Assert.Equal(0f, NetworkHeat.PerReport(12, -1f, 6.5f));
            Assert.Equal(0f, NetworkHeat.PerReport(12, 0.25f, -1f));
        }

        [Fact]
        public void Throttle_FirstCall_Emits()
        {
            ulong next = 0;
            Assert.True(HeatThrottle.ShouldEmit(1000, ref next, 1111));
            Assert.Equal(2111UL, next);
        }

        [Fact]
        public void Throttle_TooSoon_Waits()
        {
            ulong next = 0;
            HeatThrottle.ShouldEmit(1000, ref next, 1111);
            Assert.False(HeatThrottle.ShouldEmit(2000, ref next, 1111));
            Assert.Equal(2111UL, next);
        }

        [Fact]
        public void Throttle_TimeWentBackwards_EmitsNow()
        {
            ulong next = 50_000;
            Assert.True(HeatThrottle.ShouldEmit(1000, ref next, 1111));
            Assert.Equal(2111UL, next);
        }
    }
}
