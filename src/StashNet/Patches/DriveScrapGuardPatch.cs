using HarmonyLib;
using StashNet.Blocks;
using StashNet.Drives;

namespace StashNet.Patches
{
    /// <summary>
    /// Milestone 8: scrapping a drive would destroy everything stored on it. The Scrap action (backpack
    /// and workbench windows) refuses a drive that still holds items, or whose data can't be read.
    /// Empty drives scrap normally. To move up a drive tier: unload it in a terminal, then scrap it.
    /// </summary>
    [HarmonyPatch(typeof(ItemActionEntryScrap), nameof(ItemActionEntryScrap.OnActivated))]
    public static class DriveScrapGuardPatch
    {
        public static bool Prefix(ItemActionEntryScrap __instance)
        {
            if (!(__instance.ItemController is IItemStackController controller)) return true;
            ItemStack stack = controller.ItemStack;
            if (stack == null || stack.IsEmpty() || !DriveRules.IsDrive(stack.itemValue)) return true;
            if (!DriveItem.HoldsItems(stack.itemValue)) return true;

            EntityPlayerLocal player = __instance.ItemController.xui?.playerUI?.entityPlayer;
            if (player != null) GameManager.ShowTooltip(player, Localization.Get("stashnetUnloadBeforeScrap"), string.Empty, "ui_denied");
            return false;
        }
    }
}
