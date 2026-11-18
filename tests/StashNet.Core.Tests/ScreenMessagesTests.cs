using System.IO;
using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class ScreenMessagesTests
    {
        static ScreenView V(bool core = true, bool shut = false, bool locked = false, int used = 10, int max = 48, int drives = 3, int full = 1, int heat = 2)
            => new ScreenView(core, shut, locked, used, max, drives, full, heat);

        [Fact]
        public void ViewsWithSameNumbersAreEqual()
        {
            Assert.Equal(V(), V());
            Assert.True(V() == V());
            Assert.Equal(V().GetHashCode(), V().GetHashCode());
        }

        [Fact]
        public void ViewsDifferingInOneFieldAreNotEqual()
        {
            Assert.NotEqual(V(), V(core: false));
            Assert.NotEqual(V(), V(shut: true));
            Assert.NotEqual(V(), V(locked: true));
            Assert.NotEqual(V(), V(used: 11));
            Assert.NotEqual(V(), V(max: 49));
            Assert.NotEqual(V(), V(drives: 4));
            Assert.NotEqual(V(), V(full: 2));
            Assert.NotEqual(V(), V(heat: 3));
            Assert.True(V() != V(heat: 3));
        }

        [Fact]
        public void HeatSegmentsRoundAndClamp()
        {
            Assert.Equal(0, ScreenView.HeatSegmentsOf(0f, 6.5f));
            Assert.Equal(3, ScreenView.HeatSegmentsOf(3.25f, 6.5f));
            Assert.Equal(6, ScreenView.HeatSegmentsOf(6.5f, 6.5f));
            Assert.Equal(6, ScreenView.HeatSegmentsOf(9f, 6.5f));
            Assert.Equal(0, ScreenView.HeatSegmentsOf(1f, 0f));
            Assert.Equal(6, V(heat: 9).HeatSegments);
            Assert.Equal(0, V(heat: -2).HeatSegments);
        }

        [Fact]
        public void HeatWords()
        {
            Assert.Equal("CALM", ScreenView.HeatWord(0));
            Assert.Equal("CALM", ScreenView.HeatWord(2));
            Assert.Equal("WARM", ScreenView.HeatWord(3));
            Assert.Equal("WARM", ScreenView.HeatWord(4));
            Assert.Equal("HOT", ScreenView.HeatWord(5));
            Assert.Equal("HOT", ScreenView.HeatWord(6));
        }

        [Fact]
        public void SpecialViews()
        {
            Assert.False(ScreenView.NoSignal.HasCore);
            Assert.Equal(new ScreenView(false, false, false, 0, 0, 0, 0, 0), ScreenView.NoSignal);
            var s = ScreenView.ShutOut();
            Assert.True(s.HasCore && s.Shut && s.Locked);
            Assert.Equal(0, s.UsedStacks + s.MaxStacks + s.Drives + s.FullDrives + s.HeatSegments);
        }

        static ScreenMessage RoundTrip(ScreenMessage m)
        {
            using (var ms = new MemoryStream())
            {
                ScreenCodec.Write(new BinaryWriter(ms), m);
                ms.Position = 0;
                return ScreenCodec.Read(new BinaryReader(ms));
            }
        }

        [Fact]
        public void EachOpRoundTrips()
        {
            var w = RoundTrip(new ScreenMessage { Op = ScreenOp.Watch, Terminal = new Pos3(1, -2, 3) });
            Assert.Equal(ScreenOp.Watch, w.Op);
            Assert.Equal(new Pos3(1, -2, 3), w.Terminal);

            var u = RoundTrip(new ScreenMessage { Op = ScreenOp.Unwatch, Terminal = new Pos3(4, 5, 6) });
            Assert.Equal(ScreenOp.Unwatch, u.Op);
            Assert.Equal(new Pos3(4, 5, 6), u.Terminal);

            var a = RoundTrip(new ScreenMessage { Op = ScreenOp.Assign, Terminal = new Pos3(1, 2, 3), HasCore = true, Core = new Pos3(7, 8, 9) });
            Assert.Equal(ScreenOp.Assign, a.Op);
            Assert.Equal(new Pos3(1, 2, 3), a.Terminal);
            Assert.True(a.HasCore);
            Assert.Equal(new Pos3(7, 8, 9), a.Core);

            var a2 = RoundTrip(new ScreenMessage { Op = ScreenOp.Assign, Terminal = new Pos3(1, 2, 3), HasCore = false });
            Assert.False(a2.HasCore);

            var s = RoundTrip(new ScreenMessage { Op = ScreenOp.State, Core = new Pos3(7, 8, 9), View = V(used: 1284, heat: 5) });
            Assert.Equal(ScreenOp.State, s.Op);
            Assert.Equal(new Pos3(7, 8, 9), s.Core);
            Assert.Equal(V(used: 1284, heat: 5), s.View);
        }

        [Fact]
        public void UnknownOpThrows()
        {
            using (var ms = new MemoryStream(new byte[] { 99 }))
                Assert.Throws<System.FormatException>(() => ScreenCodec.Read(new BinaryReader(ms)));
        }
    }
}
