namespace StashNet.Core.Heat
{
    /// <summary>
    /// "Report heat at most once per delay", in world time. Same rhythm as vanilla workstations
    /// (TileEntity.emitHeatMapEvent), including its reset when world time goes backwards.
    /// </summary>
    public static class HeatThrottle
    {
        public static bool ShouldEmit(ulong now, ref ulong nextTime, ulong delay)
        {
            // The last report looks like it happened in the future: time went backwards (settime, reload).
            bool timeWentBack = nextTime >= delay && nextTime - delay > now;
            if (now < nextTime && !timeWentBack) return false;
            nextTime = now + delay;
            return true;
        }
    }
}
