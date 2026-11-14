using HarmonyLib;
using StashNet.Blocks;
using StashNet.Drives;

namespace StashNet.Patches
{
    /// <summary>
    /// Selling a drive to a trader would destroy everything stored on it (owner, 2026-11-14: guard it
    /// like Scrap). The Sell action refuses a drive that holds items or can't be read. Empty drives
    /// sell normally. Stocking your OWN vending machine is left alone: the drive keeps its contents
    /// and whoever buys it gets them.
    /// </summary>
    [HarmonyPatch(typeof(ItemActionEntrySell), nameof(ItemActionEntrySell.OnActivated))]
    public static class DriveSellGuardPatch
    {
        public static bool Prefix(ItemActionEntrySell __instance)
        {
            if (__instance.isOwner) return true;
            if (!(__instance.ItemController is IItemStackController controller)) return true;
            ItemStack stack = controller.ItemStack;
            if (stack == null || stack.IsEmpty() || !DriveRules.IsDrive(stack.itemValue)) return true;
            if (!DriveItem.HoldsItems(stack.itemValue)) return true;

            EntityPlayerLocal player = __instance.ItemController.xui?.playerUI?.entityPlayer;
            if (player != null) GameManager.ShowTooltip(player, Localization.Get("stashnetUnloadBeforeSell"), string.Empty, "ui_denied");
            return false;
        }
    }
}
