using System;

namespace StashNet.Drives
{
    /// <summary>Moves items into a backpack and reports exactly how many arrived.</summary>
    public static class BagTransfer
    {
        /// <summary>
        /// Adds up to <paramref name="count"/> copies of <paramref name="template"/> to the bag, in
        /// stack-sized chunks. Plain items may merge into existing stacks; variant items only go
        /// into empty slots (so their stats aren't merged away). Returns the number that arrived.
        /// </summary>
        public static int GiveToBag(Bag bag, ItemValue template, int count, bool plain, out bool bagFull)
        {
            bagFull = false;
            int maxStack = Math.Max(1, template.ItemClass.Stacknumber.Value);
            int given = 0;
            while (given < count)
            {
                int n = Math.Min(count - given, maxStack);
                var stack = new ItemStack(template.Clone(), n);
                if (plain) bag.TryStackItem(0, stack);
                if (stack.count > 0 && bag.AddItem(stack)) stack.count = 0;
                int moved = n - stack.count;
                given += moved;
                if (moved < n)
                {
                    bagFull = true;
                    break;
                }
            }
            return given;
        }
    }
}
