using HarmonyLib;
using StashNet.UI;

namespace StashNet.Patches
{
    /// <summary>
    /// The backpack's "move all" and "fill and smart" buttons only work with a loot container or bag
    /// storage open. While a Storage Terminal is open (and no loot container), they're enabled and
    /// deposit into the network instead (owner request, 2026-11-14). Locked backpack slots are skipped.
    /// </summary>
    internal static class BackpackMoveTarget
    {
        internal static XUiC_StashNetTerminal Terminal(XUi xui) =>
            xui != null && xui.LootContainer == null && xui.AssembleItem?.CurrentItem == null ? XUiC_StashNetTerminal.Current : null;

        internal static bool IsBackpackControls(XUiC_ContainerStandardControls controls) =>
            controls.GetParentByType<XUiC_BackpackWindow>() != null;
    }

    [HarmonyPatch(typeof(XUiC_BackpackWindow), nameof(XUiC_BackpackWindow.TryGetMoveDestinationInventory))]
    public static class BackpackMoveEnabledPatch
    {
        public static void Postfix(XUiC_BackpackWindow __instance, ref bool __result)
        {
            if (!__result && BackpackMoveTarget.Terminal(__instance.xui) != null) __result = true;
        }
    }

    [HarmonyPatch(typeof(XUiC_ContainerStandardControls), nameof(XUiC_ContainerStandardControls.MoveAll))]
    public static class BackpackMoveAllPatch
    {
        public static bool Prefix(XUiC_ContainerStandardControls __instance)
        {
            XUiC_StashNetTerminal terminal = BackpackMoveTarget.Terminal(__instance.xui);
            if (terminal == null || !BackpackMoveTarget.IsBackpackControls(__instance)) return true;
            __instance.UpdateLockedSlotStates?.Invoke(__instance);
            terminal.DepositAll();
            return false;
        }
    }

    [HarmonyPatch(typeof(XUiC_ContainerStandardControls), nameof(XUiC_ContainerStandardControls.MoveFillAndSmart))]
    public static class BackpackMoveMatchingPatch
    {
        public static bool Prefix(XUiC_ContainerStandardControls __instance)
        {
            XUiC_StashNetTerminal terminal = BackpackMoveTarget.Terminal(__instance.xui);
            if (terminal == null || !BackpackMoveTarget.IsBackpackControls(__instance)) return true;
            __instance.UpdateLockedSlotStates?.Invoke(__instance);
            terminal.DepositMatching();
            return false;
        }
    }
}

namespace StashNet.Patches
{
    /// <summary>
    /// "Fill stacks" and "smart" have no buttons in the vanilla backpack, but they share the same
    /// destination check, so route them too (a null destination would otherwise reach vanilla).
    /// </summary>
    [HarmonyPatch(typeof(XUiC_ContainerStandardControls), nameof(XUiC_ContainerStandardControls.MoveFillStacks))]
    public static class BackpackMoveFillStacksPatch
    {
        public static bool Prefix(XUiC_ContainerStandardControls __instance) => BackpackMoveMatchingPatch.Prefix(__instance);
    }

    [HarmonyPatch(typeof(XUiC_ContainerStandardControls), nameof(XUiC_ContainerStandardControls.MoveSmart))]
    public static class BackpackMoveSmartPatch
    {
        public static bool Prefix(XUiC_ContainerStandardControls __instance) => BackpackMoveMatchingPatch.Prefix(__instance);
    }
}
