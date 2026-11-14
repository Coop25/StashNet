using StashNet.Core.Network;
using Xunit;

namespace StashNet.Core.Tests
{
    public class StationOutputSettingsTests
    {
        [Fact]
        public void DefaultsToOn()
        {
            Assert.True(new StationOutputSettings().IsOn(1, 2, 3));
        }

        [Fact]
        public void SetOff_ThenOnAgain()
        {
            var s = new StationOutputSettings();
            Assert.True(s.Set(1, 2, 3, false));
            Assert.False(s.IsOn(1, 2, 3));
            Assert.True(s.IsOn(1, 2, 4));
            Assert.True(s.Set(1, 2, 3, true));
            Assert.True(s.IsOn(1, 2, 3));
            Assert.False(s.Set(1, 2, 3, true)); // no change
        }

        [Fact]
        public void RoundTrips()
        {
            var s = new StationOutputSettings();
            s.Set(-5, 60, 1200, false);
            s.Set(7, 8, 9, false);
            var back = StationOutputSettings.Parse(s.Serialize());
            Assert.False(back.IsOn(-5, 60, 1200));
            Assert.False(back.IsOn(7, 8, 9));
            Assert.True(back.IsOn(0, 0, 0));
        }

        [Fact]
        public void Parse_SkipsBadLines()
        {
            var s = StationOutputSettings.Parse("# comment\n1,2,3\nnope\n4,5\n\n6,7,x\r\n8,9,10\r\n");
            Assert.False(s.IsOn(1, 2, 3));
            Assert.False(s.IsOn(8, 9, 10));
            Assert.Equal(2, s.OffCount);
        }

        [Fact]
        public void Parse_NullOrEmpty_AllOn()
        {
            Assert.Equal(0, StationOutputSettings.Parse(null).OffCount);
            Assert.Equal(0, StationOutputSettings.Parse("").OffCount);
        }
    }
}
