using System;

namespace StashNet.Core.Protocol
{
    /// <summary>How many to take from a terminal cell (the amount box and the Take buttons).</summary>
    public static class TakeAmount
    {
        /// <summary>At least 1 and at most what's stored; 0 when nothing is stored.</summary>
        public static int Clamp(int requested, int stored)
        {
            if (stored <= 0) return 0;
            return Math.Max(1, Math.Min(requested, stored));
        }

        /// <summary>One stack, or everything if less than a stack is stored.</summary>
        public static int Stack(int stored, int stackSize) => Math.Max(0, Math.Min(stored, Math.Max(1, stackSize)));
    }
}
