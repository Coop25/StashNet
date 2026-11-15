using HarmonyLib;
using StashNet.Crafting;

namespace StashNet.Stations
{
    /// <summary>
    /// Automatic crafts (StationAutomation queues them with craftExpGain 0) must not enter the
    /// station's "crafts completed" list: vanilla merges that list by item regardless of who crafted,
    /// so an automatic entry would swallow a player's later XP for the same item, or hand them XP for
    /// automatic crafts. Only at Network stations; a 0-XP craft gives no XP either way.
    /// </summary>
    [HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.AddCraftComplete))]
    public static class AutoCraftRecordPatch
    {
        public static bool Prefix(TileEntityWorkstation __instance, int craftExpGain)
        {
            if (craftExpGain != 0) return true;
            Block block = GameManager.Instance?.World?.GetBlock(__instance.ToWorldPos()).Block;
            return !CraftingNetwork.IsCraftingTerminal(block);
        }
    }
}
