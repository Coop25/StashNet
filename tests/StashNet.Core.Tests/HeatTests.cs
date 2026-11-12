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
