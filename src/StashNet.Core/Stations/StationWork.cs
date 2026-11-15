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

        public static FireAction Decide(string fuelMode, bool hasFuelModule, bool hasWork, bool burning, float burnLeft, bool besideWater)
        {
            if (!hasFuelModule || fuelMode == null || fuelMode == FuelModes.Manual) return FireAction.None;
            if (!hasWork) return burning ? FireAction.PutOut : FireAction.None;
            if (burning || burnLeft <= 0f || besideWater) return FireAction.None;
            return FireAction.Light;
        }
    }
}
