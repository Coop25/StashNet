using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class DragGestureTests
    {
        // Frame(down, held, up, hovered, x, y, itemKey, canTake)
        static bool Press(DragGesture g, string item = "wood") => g.Frame(true, true, false, true, 0, 0, item, true);
        static bool Hold(DragGesture g, float x, string item = "wood", bool canTake = true) => g.Frame(false, true, false, true, x, 0, item, canTake);
        static void Release(DragGesture g) => g.Frame(false, false, true, true, 0, 0, "wood", true);
        static void Idle(DragGesture g) => g.Frame(false, false, false, false, 0, 0, "wood", true);

        [Fact]
        public void TakesOnceAfterMovingPastTheDistance()
        {
            var g = new DragGesture();
            Assert.False(Press(g));
            Assert.False(Hold(g, 3));
            Assert.True(Hold(g, 5));
            Assert.False(Hold(g, 9)); // once per press
            Assert.True(g.ConsumeClick()); // the release that ends the drag does nothing else
            Assert.False(g.ConsumeClick());
        }

        [Fact]
        public void PlainClickIsNotSwallowed()
        {
            var g = new DragGesture();
            Press(g);
            Hold(g, 1);
            Release(g);
            Assert.False(g.ConsumeClick());
        }

        [Fact]
        public void NoTakeWhenTheCellNowShowsAnotherItem()
        {
            // Listing refreshed and re-sorted while the button was held: never take what moved into the cell.
            var g = new DragGesture();
            Press(g, "wood");
            Assert.False(Hold(g, 8, "stone"));
        }

        [Fact]
        public void NoTakeWhenNotAllowed()
        {
            var g = new DragGesture();
            Press(g);
            Assert.False(Hold(g, 8, canTake: false)); // cursor busy, shift held, empty cell
        }

        [Fact]
        public void NoTakeWhenPressStartedElsewhere()
        {
            var g = new DragGesture();
            g.Frame(true, true, false, false, 0, 0, "wood", true); // pressed on another cell
            Assert.False(Hold(g, 8));
        }

        [Fact]
        public void StaleDragFlagIsClearedOnceIdle()
        {
            // A drag whose release fired no click (moved off the cell) must not swallow a later genuine click.
            var g = new DragGesture();
            Press(g);
            Assert.True(Hold(g, 20));
            Release(g);
            Idle(g);
            Assert.False(g.ConsumeClick());
        }
    }
}
