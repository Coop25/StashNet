using System;

namespace StashNet.Core.Stations
{
    public enum FireAction
    {
        None,
        Light,
        PutOut,
    }

    /// <summary>Whether a fuel station has something to do, and what auto-fire does about it.</summary>
    public static class StationWork
    {
        /// <param name="rawOreCounts">Counts in the forge's raw-ore input slots only (never the melted-metal tallies); null = none.</param>
        public static bool HasWork(bool recipeQueued, bool smelterDisabled, int[] rawOreCounts)
        {
            if (recipeQueued) return true;
            if (smelterDisabled || rawOreCounts == null) return false;
            foreach (int c in rawOreCounts)
            {
                if (c > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Whether <paramref name="count"/> of item <paramref name="type"/> fits the output slots
        /// (type, count, stackSize; type 0 = empty), like vanilla ItemStack.AddToItemStackArray: one
        /// partial stack of the same item with room, else an empty slot. A craft that can't output
        /// stalls, so it isn't work.
        /// </summary>
        public static bool OutputFits((int Type, int Count, int StackSize)[] slots, int type, int count, int stackSize)
        {
            if (slots == null) return false;
            foreach (var s in slots)
            {
                if (s.Type == type && s.Count > 0 && s.Count + count <= Math.Max(1, s.StackSize)) return true;
            }
            foreach (var s in slots)
            {
                if (s.Type == 0 || s.Count <= 0) return count <= Math.Max(1, stackSize);
            }
            return false;
        }

        public static FireAction Decide(string fuelMode, bool hasFuelModule, bool hasWork, bool burning, float burnLeft, bool besideWater)
        {
            if (!hasFuelModule || fuelMode == null || fuelMode == FuelModes.Manual) return FireAction.None;
            if (!hasWork) return burning ? FireAction.PutOut : FireAction.None;
            if (burning || burnLeft <= 0f || besideWater) return FireAction.None;
            return FireAction.Light;
        }
    }
}
