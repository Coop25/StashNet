using System;
using System.Collections.Generic;
using System.Linq;
using StashNet.Blocks;
using StashNet.Core.Drives;
using StashNet.Drives;

namespace StashNet.Dev
{
    /// <summary>
    /// `stashnet drive ...` - test commands for Milestone 2 (the Terminal UI replaces these in M3).
    /// Rule throughout: never lose items. Move first into the destination, then remove from the
    /// source only what actually arrived.
    /// </summary>
    internal static class DriveCommands
    {
        public static void Run(List<string> args, Action<string> output)
        {
            string sub = args.Count > 1 ? args[1].ToLowerInvariant() : "show";
            if (!TryGetHeldDrive(output, out EntityPlayerLocal player, out ItemValue drive, out DriveCapacity cap, out DriveContents contents)) return;

            switch (sub)
            {
                case "show": Show(output, drive, cap, contents); break;
                case "deposit": Deposit(output, player, drive, cap, contents); break;
                case "withdraw": Withdraw(output, player, drive, cap, contents); break;
                case "wipe": Wipe(output, player, drive, contents); break;
                default: output("Usage: stashnet drive [show|deposit|withdraw|wipe]"); break;
            }
        }

        private static void Show(Action<string> output, ItemValue drive, DriveCapacity cap, DriveContents contents)
        {
            output($"{drive.ItemClass.GetLocalizedItemName()}: {contents.TypeCount} different items, {contents.UsedStacks(ItemCodec.StackSizeOf)}/{cap.MaxStacks} stacks ({contents.TotalCount} items)");
            foreach (DriveEntry e in contents.Entries)
            {
                ItemClass ic = ItemClass.GetItemClass(e.Name);
                string label = ic != null ? ic.GetLocalizedItemName() : e.Name + " (MISSING - item no longer exists)";
                string detail = "";
                if (e.Variant != null && ItemCodec.TryDecode(e.Name, e.Variant, out ItemValue iv, out _))
                {
                    var parts = new List<string>();
                    if (iv.HasQuality) parts.Add("quality " + iv.Quality);
                    if (iv.MaxUseTimes > 0) parts.Add($"durability {Math.Max(0, iv.MaxUseTimes - (int)iv.UseTimes)}/{iv.MaxUseTimes}");
                    int mods = (iv.modifications ?? new ItemValue[0]).Count(m => m != null && !m.IsEmpty());
                    if (mods > 0) parts.Add(mods + " mod(s)");
                    detail = parts.Count > 0 ? " [" + string.Join(", ", parts.ToArray()) + "]" : " [*]";
                }
                output($"  {e.Count} x {label}{detail}");
            }
        }

        private static void Deposit(Action<string> output, EntityPlayerLocal player, ItemValue drive, DriveCapacity cap, DriveContents contents)
        {
            Bag bag = player.bag;
            PackedBoolArray locked = bag.LockedSlots;
            var moves = new List<KeyValuePair<int, int>>(); // slot -> amount moved
            int skippedUnstorable = 0;

            for (int i = 0; i < bag.SlotCount; i++)
            {
                ItemStack stack = bag.ItemGrid[i];
                if (stack == null || stack.IsEmpty()) continue;
                if (locked != null && i < locked.Length && locked[i]) continue;
                if (DriveRules.IsDrive(stack.itemValue)) continue;
                if (!ItemCodec.CanStore(stack.itemValue))
                {
                    skippedUnstorable++;
                    continue;
                }
                int accepted = contents.TryInsert(cap, ItemCodec.StackSizeOf, ItemCodec.NameOf(stack.itemValue), ItemCodec.EncodeVariant(stack.itemValue), stack.count);
                if (accepted > 0) moves.Add(new KeyValuePair<int, int>(i, accepted));
            }

            if (moves.Count == 0)
            {
                output("Nothing deposited (drive full, or nothing storable in your backpack).");
                return;
            }

            // Save the drive first: if anything fails after this, items are duplicated, never lost.
            SaveHeldDrive(player, drive, contents);
            int total = 0;
            foreach (var mv in moves)
            {
                ItemStack stack = bag.ItemGrid[mv.Key].Clone();
                stack.count -= mv.Value;
                bag.SetSlot(mv.Key, stack.count > 0 ? stack : ItemStack.Empty.Clone());
                total += mv.Value;
            }
            output($"Deposited {total} items. Drive now {contents.TypeCount} different items, {contents.UsedStacks(ItemCodec.StackSizeOf)}/{cap.MaxStacks} stacks ({contents.TotalCount} items).");
            if (skippedUnstorable > 0) output($"{skippedUnstorable} stack(s) can't be stored on drives yet (e.g. painted blocks) and stayed in your backpack.");
        }

        private static void Withdraw(Action<string> output, EntityPlayerLocal player, ItemValue drive, DriveCapacity cap, DriveContents contents)
        {
            Bag bag = player.bag;
            int total = 0;
            var missing = new HashSet<string>();
            bool backpackFull = false;

            foreach (DriveEntry e in contents.Entries.ToList())
            {
                if (!ItemCodec.TryDecode(e.Name, e.Variant, out ItemValue template, out string missingName))
                {
                    missing.Add(missingName ?? e.Name);
                    continue;
                }
                int moved = BagTransfer.GiveToBag(bag, template, e.Count, e.Variant == null, out backpackFull);
                if (moved > 0)
                {
                    contents.Extract(e.Name, e.Variant, moved);
                    total += moved;
                }
                if (backpackFull) break;
            }

            if (total > 0) SaveHeldDrive(player, drive, contents);
            output($"Withdrew {total} items. Drive now {contents.TypeCount} different items, {contents.UsedStacks(ItemCodec.StackSizeOf)}/{cap.MaxStacks} stacks ({contents.TotalCount} items).");
            if (backpackFull) output("Your backpack is full - the rest stayed on the drive.");
            if (missing.Count > 0) output("Kept on the drive (item no longer exists in this game): " + string.Join(", ", missing.ToArray()));
        }

        private static void Wipe(Action<string> output, EntityPlayerLocal player, ItemValue drive, DriveContents contents)
        {
            int count = contents.TotalCount;
            SaveHeldDrive(player, drive, new DriveContents());
            output($"Drive wiped ({count} items deleted). Testing only!");
        }

        private static bool TryGetHeldDrive(Action<string> output, out EntityPlayerLocal player, out ItemValue drive, out DriveCapacity cap, out DriveContents contents)
        {
            cap = default;
            contents = null;
            player = GameManager.Instance?.World?.GetPrimaryPlayer();
            drive = player?.inventory.holdingItemItemValue;
            if (player == null || drive == null || drive.IsEmpty() || !DriveRules.IsDrive(drive))
            {
                output("Hold a drive in your hand first (put it on your toolbelt and select it).");
                return false;
            }
            if (!DriveItem.TryGetCapacity(drive, out cap))
            {
                output("This drive has no capacity configured (StashNetMaxStacks missing in items.xml).");
                return false;
            }
            if (!DriveItem.TryLoad(drive, out contents, out string error))
            {
                output(error);
                return false;
            }
            return true;
        }

        private static void SaveHeldDrive(EntityPlayerLocal player, ItemValue drive, DriveContents contents)
        {
            DriveItem.Save(drive, contents);
            player.inventory.SetItem(player.inventory.holdingItemIdx, drive, player.inventory.holdingCount);
        }
    }
}
