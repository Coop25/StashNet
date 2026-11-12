using System.Collections.Generic;
using StashNet.Blocks;
using StashNet.Drives;

namespace StashNet.Network
{
    /// <summary>
    /// What a Storage Terminal can do. Each call re-opens the network so it acts on current data.
    /// Never lose items: withdraw = backpack first, then take from drives only what arrived;
    /// deposit = drives saved first, then the backpack is reduced by what was accepted.
    /// </summary>
    public static class TerminalOps
    {
        public static int Withdraw(EntityPlayerLocal player, Vector3i terminalPos, string name, string variant, int count, out string message)
        {
            message = null;
            if (!StashNetNetwork.TryOpen(player.world, terminalPos, out StashNetNetwork net, out message)) return 0;
            if (!ItemCodec.TryDecode(name, variant, out ItemValue template, out _))
            {
                message = Localization.Get("stashnetItemMissing");
                return 0;
            }

            int available = 0;
            foreach (var item in net.Storage.Aggregate())
            {
                if (item.Name == name && item.Variant == variant) available = item.Count;
            }
            int wanted = System.Math.Min(count, available);
            if (wanted <= 0) return 0;

            int moved = BagTransfer.GiveToBag(player.bag, template, wanted, variant == null, out bool full);
            if (moved > 0)
            {
                int removed = net.Storage.Extract(name, variant, moved);
                if (removed != moved) Log.Error($"[StashNet] Withdraw mismatch for {name}: gave {moved}, removed {removed}.");
                net.SaveDirty();
            }
            if (full) message = Localization.Get("stashnetBackpackFull");
            return moved;
        }

        /// <summary>Stores as much of <paramref name="stack"/> as fits. Returns the amount accepted; the caller reduces the stack.</summary>
        public static int DepositStack(EntityPlayerLocal player, Vector3i terminalPos, ItemStack stack, out string message)
        {
            message = null;
            if (stack == null || stack.IsEmpty()) return 0;
            if (DriveRules.IsDrive(stack.itemValue) || !ItemCodec.CanStore(stack.itemValue))
            {
                message = Localization.Get("stashnetCantStore");
                return 0;
            }
            if (!StashNetNetwork.TryOpen(player.world, terminalPos, out StashNetNetwork net, out message)) return 0;

            int accepted = net.Storage.Insert(ItemCodec.NameOf(stack.itemValue), ItemCodec.EncodeVariant(stack.itemValue), stack.count);
            if (accepted > 0) net.SaveDirty();
            if (accepted < stack.count) message = Localization.Get("stashnetNetworkFull");
            return accepted;
        }

        /// <summary>Deposits every unlocked backpack stack that fits. Returns the number of items moved.</summary>
        public static int DepositBackpack(EntityPlayerLocal player, Vector3i terminalPos, out string message)
        {
            message = null;
            if (!StashNetNetwork.TryOpen(player.world, terminalPos, out StashNetNetwork net, out message)) return 0;

            Bag bag = player.bag;
            PackedBoolArray locked = bag.LockedSlots;
            var moves = new List<KeyValuePair<int, int>>();
            bool anyLeft = false;
            for (int i = 0; i < bag.SlotCount; i++)
            {
                ItemStack stack = bag.ItemGrid[i];
                if (stack == null || stack.IsEmpty()) continue;
                if (locked != null && i < locked.Length && locked[i]) continue;
                if (DriveRules.IsDrive(stack.itemValue) || !ItemCodec.CanStore(stack.itemValue)) continue;
                int accepted = net.Storage.Insert(ItemCodec.NameOf(stack.itemValue), ItemCodec.EncodeVariant(stack.itemValue), stack.count);
                if (accepted > 0) moves.Add(new KeyValuePair<int, int>(i, accepted));
                if (accepted < stack.count) anyLeft = true;
            }
            if (moves.Count == 0)
            {
                message = Localization.Get("stashnetNothingDeposited");
                return 0;
            }

            net.SaveDirty();
            int total = 0;
            foreach (var mv in moves)
            {
                ItemStack stack = bag.ItemGrid[mv.Key].Clone();
                stack.count -= mv.Value;
                bag.SetSlot(mv.Key, stack.count > 0 ? stack : ItemStack.Empty.Clone());
                total += mv.Value;
            }
            if (anyLeft) message = Localization.Get("stashnetNetworkFull");
            return total;
        }
    }
}
