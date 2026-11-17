using HarmonyLib;
using StashNet.Crafting;

namespace StashNet.Stations
{
    /// <summary>
    /// SERVER: a Network station was removed (picked up, broken, replaced). Drop its automation
    /// settings so a station placed on the same spot later starts fresh.
    /// </summary>
    [HarmonyPatch(typeof(BlockWorkstation), nameof(BlockWorkstation.OnBlockRemoved))]
    public static class StationRemovedPatch
    {
        public static void Postfix(Vector3i _blockPos, BlockValue _blockValue)
        {
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
            if (!CraftingNetwork.IsCraftingTerminal(_blockValue.Block)) return;
            StationSettingsStore.Remove(_blockPos);
            StationAutomation.Forget(_blockPos);
        }
    }
}
