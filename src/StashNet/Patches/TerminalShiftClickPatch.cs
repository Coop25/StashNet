using HarmonyLib;
using StashNet.UI;

namespace StashNet.Patches
{
    /// <summary>
    /// Shift-click on a backpack/toolbelt slot while a StashNet terminal is open = deposit that
    /// stack into the network. Only acts while our terminal window is open.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleMoveToPreferredLocation))]
    public static class TerminalShiftClickPatch
    {
        public static bool Prefix(XUiC_ItemStack __instance)
        {
            XUiC_StashNetTerminal terminal = XUiC_StashNetTerminal.Current;
            if (terminal == null || terminal.xui != __instance.xui) return true;
            if (!__instance.xui.playerUI.windowManager.IsWindowOpen(XUiC_StashNetTerminal.WindowGroupId)) return true;
            if (__instance.StackLocation != XUiC_ItemStack.StackLocationTypes.Backpack &&
                __instance.StackLocation != XUiC_ItemStack.StackLocationTypes.ToolBelt) return true;
            if (__instance.ItemStack == null || __instance.ItemStack.IsEmpty()) return true;

            terminal.DepositFromSlot(__instance);
            return false;
        }
    }
}
