using System.Linq;
using StashNet.Core.Network;
using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class ScreenWatchBookTests
    {
        static readonly Pos3 A = new Pos3(1, 0, 0), B = new Pos3(2, 0, 0), C = new Pos3(10, 0, 0), C2 = new Pos3(20, 0, 0);

        static ScreenWatchBook Two()
        {
            var b = new ScreenWatchBook();
            b.Watch(1, A);
            b.Watch(1, B);
            b.SetCore(A, C);
            b.SetCore(B, C);
            return b;
        }

        static ScreenView Live(int used) => new ScreenView(true, false, false, used, 100, 4, 0, 1);

        [Fact]
        public void PlayerListedOncePerCore()
        {
            var b = Two();
            Assert.Equal(new[] { 1 }, b.WatchersOf(C).ToArray());
        }

        [Fact]
        public void UnwatchingOneTerminalKeepsTheOther()
        {
            var b = Two();
            b.Unwatch(1, A);
            Assert.Equal(new[] { 1 }, b.WatchersOf(C).ToArray());
            b.Unwatch(1, B);
            Assert.Empty(b.Cores);
            Assert.Empty(b.Terminals);
        }

        [Fact]
        public void DropPlayerClearsEverything()
        {
            var b = Two();
            b.DropPlayer(1);
            Assert.Empty(b.Cores);
            Assert.Empty(b.Terminals);
            Assert.False(b.TryGetCore(A, out _));
        }

        [Fact]
        public void MovingTerminalToAnotherCoreMovesItsWatchers()
        {
            var b = new ScreenWatchBook();
            b.Watch(1, A);
            b.SetCore(A, C);
            Assert.Equal(new[] { 1 }, b.SetCore(A, C2).ToArray());
            Assert.Empty(b.WatchersOf(C));
            Assert.Equal(new[] { 1 }, b.WatchersOf(C2).ToArray());
            Assert.Empty(b.SetCore(A, C2));
            Assert.Equal(new[] { 1 }, b.SetCore(A, null).ToArray());
            Assert.Empty(b.Cores);
        }

        [Fact]
        public void FirstSetCoreCountsAsChangeEvenForNull()
        {
            var b = new ScreenWatchBook();
            b.Watch(1, A);
            Assert.Equal(new[] { 1 }, b.SetCore(A, null).ToArray());
            Assert.Empty(b.SetCore(A, null));
            Assert.False(b.TryGetCore(A, out _));
        }

        [Fact]
        public void WatchOnAssignedTerminalJoinsCoreAtOnce()
        {
            var b = new ScreenWatchBook();
            b.Watch(1, A);
            b.SetCore(A, C);
            b.Watch(2, A);
            b.Watch(2, A);
            Assert.Equal(new[] { 1, 2 }, b.WatchersOf(C).OrderBy(x => x).ToArray());
            Assert.True(b.TryGetCore(A, out var core));
            Assert.Equal(C, core);
        }

        [Fact]
        public void ShouldSendOnlyChanges()
        {
            var b = Two();
            Assert.True(b.ShouldSend(1, C, Live(5)));
            Assert.False(b.ShouldSend(1, C, Live(5)));
            Assert.True(b.ShouldSend(1, C, Live(6)));
            b.Unwatch(1, A);
            b.Unwatch(1, B);
            b.Watch(1, A);
            b.SetCore(A, C);
            Assert.True(b.ShouldSend(1, C, Live(6)));
        }

        [Fact]
        public void LockChangeResendsOnlyChangedViews()
        {
            var b = new ScreenWatchBook();
            b.Watch(1, A);
            b.Watch(2, A);
            b.SetCore(A, C);
            Assert.True(b.ShouldSend(1, C, Live(5)));
            Assert.True(b.ShouldSend(2, C, ScreenView.ShutOut()));
            Assert.False(b.LockChanged(C, "a"));
            Assert.True(b.LockChanged(C, "b"));
            Assert.False(b.ShouldSend(1, C, Live(5)));
            Assert.True(b.ShouldSend(2, C, Live(5)));
        }

        [Fact]
        public void DirtyCoreIsDueOncePerSecond()
        {
            var b = Two();
            b.MarkDirty(C);
            Assert.Equal(new[] { C }, b.TakeDue(0f).ToArray());
            b.MarkDirty(C);
            Assert.Empty(b.TakeDue(0.3f));
            b.MarkDirty(C);
            Assert.Empty(b.TakeDue(0.6f));
            Assert.Equal(new[] { C }, b.TakeDue(1.0f).ToArray());
            Assert.Empty(b.TakeDue(1.1f));
        }

        [Fact]
        public void MarkDirtyOnUnwatchedCoreIsIgnored()
        {
            var b = new ScreenWatchBook();
            b.MarkDirty(C);
            Assert.Empty(b.TakeDue(5f));
        }

        [Fact]
        public void LockChangedFirstCallFalse()
        {
            var b = Two();
            Assert.False(b.LockChanged(C, "x"));
            Assert.False(b.LockChanged(C, "x"));
            Assert.True(b.LockChanged(C, "y"));
        }

        [Fact]
        public void StaleRebuildEveryFiveSeconds()
        {
            var b = Two();
            Assert.True(b.StaleRebuildAllowed(C, 100f));
            Assert.False(b.StaleRebuildAllowed(C, 104f));
            Assert.True(b.StaleRebuildAllowed(C, 105f));
        }

        [Fact]
        public void LastWatcherGoneForgetsCoreState()
        {
            var b = Two();
            b.LockChanged(C, "x");
            b.StaleRebuildAllowed(C, 0f);
            b.DropPlayer(1);
            b.Watch(1, A);
            b.SetCore(A, C);
            Assert.False(b.LockChanged(C, "y"));
            Assert.True(b.StaleRebuildAllowed(C, 1f));
        }
    }
}
