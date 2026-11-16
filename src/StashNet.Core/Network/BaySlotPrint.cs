using System.Collections.Generic;
using System.Linq;

namespace StashNet.Core.Network
{
    /// <summary>
    /// What one bay slot held when the network summary was built: item type, count and the drive's data
    /// string. Compared by value, because the game updates slot items in place (ItemStack.Read / Clear),
    /// so comparing slot objects misses changes made by other players or mods.
    /// </summary>
    public sealed class BaySlotPrint
    {
        public BaySlotPrint(int type, int count, string data)
        {
            Type = type;
            Count = count;
            Data = data;
        }

        public int Type { get; }
        public int Count { get; }
        /// <summary>The drive's stored data (null for an empty slot or a non-drive).</summary>
        public string Data { get; private set; }

        /// <summary>
        /// True when every slot holds the same thing. Equal data held in a new string object is adopted into
        /// <paramref name="seen"/>, so the next compare is a cheap reference check.
        /// </summary>
        public static bool Same(IReadOnlyList<BaySlotPrint> seen, IReadOnlyList<BaySlotPrint> now)
        {
            if (seen.Count != now.Count) return false;
            for (int i = 0; i < seen.Count; i++)
            {
                BaySlotPrint a = seen[i], b = now[i];
                if (a.Type != b.Type || a.Count != b.Count) return false;
                if (ReferenceEquals(a.Data, b.Data)) continue;
                if (!string.Equals(a.Data, b.Data)) return false;
                a.Data = b.Data;
            }
            return true;
        }
    }

    /// <summary>The finished-items mover can skip reading the drives only when nothing could go in.</summary>
    public static class OutputMove
    {
        /// <summary>No free stack, and none of the output items is stored already (so no partial stack to top up).</summary>
        public static bool CanSkip(int freeStacks, IEnumerable<int> storedOfOutputItems) =>
            freeStacks <= 0 && storedOfOutputItems.All(n => n <= 0);
    }
}
