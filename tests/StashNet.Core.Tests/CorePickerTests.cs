using StashNet.Core.Network;
using Xunit;

namespace StashNet.Core.Tests
{
    public class CorePickerTests
    {
        [Fact]
        public void CubeDistance_UsesLargestAxis() => Assert.Equal(5, CorePicker.CubeDistance(0, 0, 0, 3, -5, 2));

        [Fact]
        public void Pick_NoCores_MinusOne() => Assert.Equal(-1, CorePicker.Pick(new CoreSite[0], 1, 2, 3));

        [Fact]
        public void Pick_OutOfReach_MinusOne() =>
            Assert.Equal(-1, CorePicker.Pick(new[] { new CoreSite(0, 0, 0, 6) }, 7, 0, 0));

        [Fact]
        public void Pick_EdgeOfReach_Included() =>
            Assert.Equal(0, CorePicker.Pick(new[] { new CoreSite(0, 0, 0, 6) }, 6, 6, -6));

        [Fact]
        public void Pick_NearestWins() =>
            Assert.Equal(1, CorePicker.Pick(new[] { new CoreSite(0, 0, 0, 12), new CoreSite(5, 0, 0, 6) }, 4, 0, 0));

        /// <summary>A nearer Core that can't reach the bay doesn't claim it; the farther one that can, does.</summary>
        [Fact]
        public void Pick_NearerButOutOfReach_Ignored() =>
            Assert.Equal(0, CorePicker.Pick(new[] { new CoreSite(0, 0, 0, 24), new CoreSite(10, 0, 0, 2) }, 6, 0, 0));

        /// <summary>Equal distance: both Cores' networks must agree on one owner, whatever order they were found in.</summary>
        [Fact]
        public void Pick_Tie_SameOwnerWhateverTheOrder()
        {
            var a = new CoreSite(0, 0, 0, 6);
            var b = new CoreSite(4, 0, 0, 6);
            Assert.Equal(0, CorePicker.Pick(new[] { a, b }, 2, 0, 0));
            Assert.Equal(1, CorePicker.Pick(new[] { b, a }, 2, 0, 0)); // both pick a (lower X)
        }

        [Fact]
        public void Pick_Tie_LowerYBeatsLowerX() =>
            Assert.Equal(1, CorePicker.Pick(new[] { new CoreSite(0, 2, 0, 6), new CoreSite(4, 0, 0, 6) }, 2, 1, 0));

        [Fact]
        public void Pick_AtCorePosition_PicksItself() =>
            Assert.Equal(0, CorePicker.Pick(new[] { new CoreSite(3, 3, 3, 6), new CoreSite(4, 3, 3, 6) }, 3, 3, 3));
    }
}
