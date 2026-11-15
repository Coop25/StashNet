using HarmonyLib;

namespace StashNet.Crafting
{
    /// <summary>
    /// The recipe list's category tabs come from ui_display.xml, looked up by the station's BLOCK name
    /// ("forge", "workbench"...). Ours are named stashnetForge etc., so the bar was empty. A Network
    /// station names its vanilla station in StashNetCategories; use that station's list.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_CategoryList), nameof(XUiC_CategoryList.SetupCategoriesByWorkstation))]
    public static class StationCategoryPatch
    {
        public const string Property = "StashNetCategories";

        public static void Prefix(ref string _workstation)
        {
            if (string.IsNullOrEmpty(_workstation)) return;
            Block block = Block.GetBlockByName(_workstation);
            if (block != null && block.Properties.Contains(Property)) _workstation = block.Properties.GetString(Property);
        }
    }
}
