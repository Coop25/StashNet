using System.Collections.Generic;
using StashNet.Blocks;
using StashNet.Drives;
using StashNet.Network;

namespace StashNet.Crafting
{
    /// <summary>
    /// SERVER ONLY. Moves finished crafts from a Crafting Terminal's output slots into its network
    /// while nobody has the station open. Never touches XUi (dedicated servers have no UI).
    /// </summary>
    public static class CraftingOutputServer
    {
        /// <summary>Don't rescan the network every tick when it's full: retry at most this often.</summary>
        private const float RetrySeconds = 5f;
        private static readonly Dictionary<Vector3i, float> nextTry = new Dictionary<Vector3i, float>();

        public static void TryMoveOutput(World world, TileEntityWorkstation te)
        {
            if (world == null || te == null) return;
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
            if (te.IsUserAccessing() || LockManager.Instance.IsLockedServer(te)) return;

            ItemStack[] output = te.output;
            if (output == null || !HasAny(output)) return;

            Vector3i pos = te.ToWorldPos();
            if (!CraftingNetwork.IsCraftingTerminal(world.GetBlock(pos).Block)) return;

            float now = UnityEngine.Time.time;
            if (nextTry.TryGetValue(pos, out float t) && now < t) return;
            nextTry[pos] = now + RetrySeconds;
            if (!Stations.StationSettingsStore.Get(pos, world.GetBlock(pos).Block).OutputOn) return; // "Stay here": vanilla output

            // Storage full per the shared live summary: don't read the drives just to find no room.
            if (!NetworkSummaryCache.TryGet(world, pos, out _, out Core.Network.NetworkSummary summary) || summary.FreeStacks <= 0) return;
            if (!StashNetNetwork.TryOpen(world, pos, out StashNetNetwork net, out _, "finished items")) return;

            bool moved = false;
            for (int i = 0; i < output.Length; i++)
            {
                ItemStack s = output[i];
                if (s == null || s.IsEmpty() || DriveRules.IsDrive(s.itemValue) || !ItemCodec.CanStore(s.itemValue)) continue;
                int accepted = net.Storage.Insert(ItemCodec.NameOf(s.itemValue), ItemCodec.EncodeVariant(s.itemValue), s.count);
                if (accepted <= 0) continue;
                moved = true;
                ItemStack rest = s.Clone();
                rest.count -= accepted;
                output[i] = rest.count > 0 ? rest : ItemStack.Empty.Clone();
            }
            if (!moved) return;

            // Drives first, then the output: if anything fails in between, items are duplicated, never lost.
            net.SaveDirty();
            te.SetModified();
            nextTry.Remove(pos);
        }

        private static bool HasAny(ItemStack[] stacks)
        {
            foreach (ItemStack s in stacks)
            {
                if (s != null && !s.IsEmpty()) return true;
            }
            return false;
        }
    }
}
