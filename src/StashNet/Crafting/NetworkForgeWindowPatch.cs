using HarmonyLib;

namespace StashNet.Crafting
{
    /// <summary>
    /// With the "Disable Smelter" game option on, vanilla opens the forge's no-smelting window, but
    /// only for the block named exactly "forge". The Network Forge would otherwise get the smelting
    /// window, whose input grid makes smelter-replacement recipes pass the ingredient check without
    /// taking anything (free crafts). Open the same window vanilla uses for its forge.
    /// </summary>
    [HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.OnLockResponseLocal))]
    public static class NetworkForgeWindowPatch
    {
        private const string NetworkForge = "stashnetForge";
        private const string NoSmeltingWindow = "workstation_forge_nosmelting";

        public static bool Prefix(TileEntityWorkstation __instance, bool _success, ref bool __result)
        {
            if (!_success || !XUiM_Recipes.DisableSmelter) return true;
            if (GameManager.Instance.World?.GetBlock(__instance.ToWorldPos()).Block?.GetBlockName() != NetworkForge) return true;

            LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
            if (ui == null || !ui.windowManager.TryGetWindow(NoSmeltingWindow, out GUIWindow window)) return true;
            ((XUiC_WorkstationWindowGroup)((XUiWindowGroup)window).Controller).SetTileEntity(__instance);
            ui.windowManager.Open(NoSmeltingWindow, _bModal: true);
            __result = true;
            return false;
        }
    }
}
