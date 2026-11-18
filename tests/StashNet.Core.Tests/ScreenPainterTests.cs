using StashNet.Core.Protocol;
using StashNet.Core.Skins;
using Xunit;

namespace StashNet.Core.Tests
{
    public class ScreenPainterTests
    {
        static (byte R, byte G, byte B) Px(byte[] buf, int x, int yFromTop)
        {
            int row = ScreenPainter.Height - 1 - yFromTop;
            int i = (row * ScreenPainter.Width + x) * 4;
            return (buf[i], buf[i + 1], buf[i + 2]);
        }

        static ScreenView Live(int used, int max) => new ScreenView(true, false, false, used, max, 14, 3, 2);

        // Bar: 14 px tall starting at line 3 top (y = 72), so its middle row is 79.
        const int BarY = 79;

        [Fact]
        public void ThousandsSeparators()
        {
            Assert.Equal("0", ScreenPainter.Thousands(0));
            Assert.Equal("1,284", ScreenPainter.Thousands(1284));
            Assert.Equal("248,832", ScreenPainter.Thousands(248832));
        }

        [Fact]
        public void DrawReturnsFullSizeBuffer()
        {
            var buf = ScreenPainter.Draw(Live(10, 100));
            Assert.Equal(256 * 192 * 4, buf.Length);
            for (int i = 3; i < buf.Length; i += 4) Assert.Equal(255, buf[i]);
        }

        [Fact]
        public void BarFillMatchesPercent()
        {
            var half = ScreenPainter.Draw(Live(50, 100));
            Assert.Equal(ScreenPainter.Text, Px(half, 125, BarY));
            Assert.NotEqual(ScreenPainter.Text, Px(half, 131, BarY));

            Assert.Equal(ScreenPainter.BarHigh, Px(ScreenPainter.Draw(Live(80, 100)), 125, BarY));
            Assert.Equal(ScreenPainter.BarFull, Px(ScreenPainter.Draw(Live(100, 100)), 125, BarY));
        }

        [Fact]
        public void LockedVsOpenDiffer()
        {
            var a = ScreenPainter.Draw(new ScreenView(true, false, true, 5, 10, 1, 0, 0));
            var b = ScreenPainter.Draw(new ScreenView(true, false, false, 5, 10, 1, 0, 0));
            Assert.NotEqual(a, b);
        }

        [Fact]
        public void ShutHidesNumbers()
        {
            var a = ScreenPainter.Draw(new ScreenView(true, true, true, 7, 9, 3, 2, 4));
            Assert.Equal(ScreenPainter.Draw(ScreenView.ShutOut()), a);
        }

        [Fact]
        public void NoSignalDiffersFromLive()
        {
            Assert.NotEqual(ScreenPainter.Draw(ScreenView.NoSignal), ScreenPainter.Draw(Live(0, 0)));
        }

        [Fact]
        public void ZeroMaxDoesNotThrow()
        {
            Assert.Equal(256 * 192 * 4, ScreenPainter.Draw(Live(5, 0)).Length);
        }
    }
}
