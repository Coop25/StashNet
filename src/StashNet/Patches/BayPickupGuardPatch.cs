using HarmonyLib;
using StashNet.Blocks;

namespace StashNet.Patches
{
    /// <summary>
    /// Owner (2026-11-13): empty Drive Bays can be picked up. The "Take" command only shows on an empty bay
    /// (TEFeatureStashNetDriveBay), but a drive could be put in while the take timer runs, and vanilla's
    /// timer end replaces the block with air, destroying whatever is inside. Re-check when the timer ends.
    /// </summary>
    [HarmonyPatch(typeof(Block), nameof(Block.TakeItemWithTimerDone))]
    public static class BayPickupGuardPatch
    {
        public static bool Prefix(TimerEventData _timerData)
        {
            if (!(_timerData?.Data is object[] data) || data.Length < 3 || !(data[1] is Vector3i pos)) return true;
            World world = GameManager.Instance?.World;
            if (!(world?.GetTileEntity(pos) is TileEntityComposite te) || te.GetFeature<TEFeatureStashNetDriveBay>() == null) return true;
            if (TEFeatureStashNetDriveBay.IsEmpty(te)) return true;

            if (data[2] is EntityPlayerLocal player) GameManager.ShowTooltip(player, Localization.Get("stashnetBayNotEmpty"), string.Empty, "ui_denied");
            return false;
        }
    }
}
