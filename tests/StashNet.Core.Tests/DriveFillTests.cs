using StashNet.Core.Drives;
using Xunit;

namespace StashNet.Core.Tests
{
    public class DriveFillTests
    {
        private static string Text(params (string name, int count)[] entries)
        {
            var d = new DriveContents();
            foreach (var (name, count) in entries) d.AddLoaded(name, null, count);
            return DriveSerializer.Serialize(d);
        }

        private static DriveFill Fill(string text, int max) => DriveFill.FromText(text, max, _ => 1);

        [Fact]
        public void Fill_NullText_IsEmpty()
        {
            DriveFill f = Fill(null, 24);
            Assert.Equal(FillBand.Empty, f.Band);
            Assert.Equal("", f.PercentText);
            Assert.Equal(0f, f.Fraction);
        }

        [Fact]
        public void Fill_BadText_IsUnreadable()
        {
            DriveFill f = Fill("garbage", 24);
            Assert.Equal(FillBand.Unreadable, f.Band);
            Assert.Equal("?", f.PercentText);
        }

        [Fact]
        public void Fill_OneStack_ShowsAtLeastOnePercent()
        {
            Assert.Equal("1%", Fill(Text(("a", 1)), 216).PercentText);
        }

        [Fact]
        public void Fill_TwoThirds_Floors()
        {
            DriveFill f = Fill(Text(("a", 144)), 216);
            Assert.Equal("66%", f.PercentText);
            Assert.Equal(FillBand.Low, f.Band);
            Assert.Equal(144, f.Used);
            Assert.Equal(216, f.Max);
        }

        [Fact]
        public void Fill_SeventyFivePercent_IsHigh()
        {
            DriveFill f = Fill(Text(("a", 54)), 72);
            Assert.Equal("75%", f.PercentText);
            Assert.Equal(FillBand.High, f.Band);
        }

        [Fact]
        public void Fill_Full_IsFull()
        {
            DriveFill f = Fill(Text(("a", 72)), 72);
            Assert.Equal("100%", f.PercentText);
            Assert.Equal(FillBand.Full, f.Band);
            Assert.Equal(1f, f.Fraction);
        }

        [Fact]
        public void Fill_OverFull_ShowsRealPercent()
        {
            DriveFill f = Fill(Text(("a", 288)), 216);
            Assert.Equal("133%", f.PercentText);
            Assert.Equal(FillBand.Full, f.Band);
            Assert.Equal(1f, f.Fraction);
        }

        [Fact]
        public void Fill_CountsTypes()
        {
            Assert.Equal(3, Fill(Text(("a", 1), ("b", 1), ("c", 1)), 24).Types);
        }
    }
}
