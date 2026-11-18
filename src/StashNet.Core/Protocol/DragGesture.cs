namespace StashNet.Core.Protocol
{
    /// <summary>
    /// Press-and-drag on one Storage Terminal cell (2026-11-18), fed once per frame with the mouse state, like vanilla's
    /// XUiC_ItemStack.Update: a press that starts on the cell and moves further than <see cref="Distance"/> with the
    /// button held takes once, but only while the cell still shows the item it showed when pressed (the listing can
    /// re-sort under a held button). The click that ends a drag is swallowed (<see cref="ConsumeClick"/>), and that
    /// flag is dropped as soon as the mouse is idle, so a drag that ended elsewhere never eats a later click.
    /// </summary>
    public sealed class DragGesture
    {
        /// <summary>Vanilla's pick-up distance (XUiC_ItemStack.PickupSnapDistance).</summary>
        public const float Distance = 4f;

        private bool pressedHere;
        private bool taken;
        private float pressX, pressY;
        private string pressItem;

        /// <summary>One frame. True when the drag should take now (at most once per press).</summary>
        public bool Frame(bool down, bool held, bool up, bool hovered, float x, float y, string itemKey, bool canTake)
        {
            if (down)
            {
                pressedHere = hovered;
                if (hovered)
                {
                    pressX = x;
                    pressY = y;
                    pressItem = itemKey;
                    taken = false;
                }
            }
            else if (!held)
            {
                pressedHere = false;
                // The release frame's click still sees the flag; after that it must not linger.
                if (!up) taken = false;
            }

            if (!pressedHere || !held || taken || !canTake || itemKey == null || itemKey != pressItem) return false;
            float dx = x - pressX, dy = y - pressY;
            if (dx * dx + dy * dy <= Distance * Distance) return false;
            taken = true;
            return true;
        }

        /// <summary>From the cell's click: true (and forget it) when this click only ends a drag.</summary>
        public bool ConsumeClick()
        {
            if (!taken) return false;
            taken = false;
            return true;
        }
    }
}
