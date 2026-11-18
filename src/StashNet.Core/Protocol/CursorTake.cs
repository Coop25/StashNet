using System.Collections.Generic;

namespace StashNet.Core.Protocol
{
    /// <summary>
    /// A take dragged out of the Storage Terminal (2026-11-18): the first stack the server gave goes on the cursor,
    /// everything else into the backpack. If the cursor is no longer free when the answer arrives (the player picked
    /// something else up meanwhile), everything goes into the backpack, so nothing is ever overwritten.
    /// </summary>
    public static class CursorTake
    {
        public static (ItemAmount? Cursor, List<ItemAmount> Bag) Split(IReadOnlyList<ItemAmount> give, bool cursorFree)
        {
            ItemAmount? cursor = null;
            var bag = new List<ItemAmount>();
            foreach (ItemAmount a in give)
            {
                if (a.Count <= 0) continue;
                if (cursorFree && cursor == null) cursor = a;
                else bag.Add(a);
            }
            return (cursor, bag);
        }
    }
}
