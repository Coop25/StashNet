using StashNet.Core.Network;
using Xunit;

namespace StashNet.Core.Tests
{
    /// <summary>Owner's rule (2026-11-13): no Core within 2 x the largest reach of another, so networks never overlap.</summary>
    public class CoreSpacingTests
    {
        private const int MaxReach = 24; // Advanced Core

        [Fact]
        public void NoCores_NotTooClose()
        {
            Assert.False(CoreSpacing.TooClose(new CoreSite[0], 0, 0, 0, MaxReach, out int nearest));
            Assert.Equal(-1, nearest);
        }

        [Fact]
        public void AtTwiceMaxReach_TooClose()
        {
            Assert.True(CoreSpacing.TooClose(new[] { new CoreSite(48, 0, 0, 24) }, 0, 0, 0, MaxReach, out int nearest));
            Assert.Equal(48, nearest);
        }

        [Fact]
        public void JustBeyondTwiceMaxReach_Allowed() =>
            Assert.False(CoreSpacing.TooClose(new[] { new CoreSite(49, 0, 0, 24) }, 0, 0, 0, MaxReach, out _));

        [Fact]
        public void Vertical_Counts() =>
            Assert.True(CoreSpacing.TooClose(new[] { new CoreSite(0, 30, 0, 24) }, 0, 0, 0, MaxReach, out _));

        /// <summary>A Scrap Core (reach 6) still counts: it can be upgraded to reach 24 later.</summary>
        [Fact]
        public void LowTierCore_StillCounts() =>
            Assert.True(CoreSpacing.TooClose(new[] { new CoreSite(30, 0, 0, 6) }, 0, 0, 0, MaxReach, out _));

        [Fact]
        public void ReportsNearest()
        {
            CoreSpacing.TooClose(new[] { new CoreSite(40, 0, 0, 24), new CoreSite(-10, 0, 0, 24) }, 0, 0, 0, MaxReach, out int nearest);
            Assert.Equal(10, nearest);
        }

        [Fact]
        public void MinGap_IsTwiceMaxReach() => Assert.Equal(48, CoreSpacing.MinGap(MaxReach));
    }
}
