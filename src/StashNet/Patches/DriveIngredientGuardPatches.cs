using System.Collections.Generic;
using HarmonyLib;
using StashNet.Blocks;
using StashNet.Drives;

namespace StashNet.Patches
{
    /// <summary>
    /// 2026-11-14: drive upgrades cost 2 drives of the tier below. Vanilla matches ingredients by item
    /// type only, so crafting could eat a drive full of items. These patches make every type-based
    /// count and removal in the backpack and toolbelt skip drives that hold items (or can't be read),
    /// so only EMPTY drives are ever counted or consumed. Other item types run vanilla untouched.
    /// </summary>
    internal static class DriveIngredientRules
    {
        /// <summary>True for a slot vanilla would match for <paramref name="wanted"/> but we must leave alone.</summary>
        internal static bool IsProtectedDrive(ItemStack slot, ItemValue wanted)
        {
            if (slot == null || slot.IsEmpty() || slot.itemValue.type != wanted.type) return false;
            return DriveItem.HoldsItems(slot.itemValue);
        }

        internal static bool Matches(ItemValue slot, int seed, int meta) =>
            (seed == -1 || seed == slot.Seed) && (meta == -1 || meta == slot.Meta);
    }

    [HarmonyPatch(typeof(Bag), nameof(Bag.GetItemCount), new[] { typeof(ItemValue), typeof(int), typeof(int), typeof(bool) })]
    public static class BagDriveCountPatch
    {
        public static void Postfix(Bag __instance, ItemValue _itemValue, int _seed, int _meta, ref int __result)
        {
            if (__result <= 0 || !DriveRules.IsDrive(_itemValue)) return;
            ItemStackGrid slots = __instance.itemGrid;
            for (int i = 0; i < slots.Length; i++)
            {
                if (DriveIngredientRules.IsProtectedDrive(slots[i], _itemValue) && DriveIngredientRules.Matches(slots[i].itemValue, _seed, _meta))
                    __result -= slots[i].count;
            }
            if (__result < 0) __result = 0;
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.GetItemCount), new[] { typeof(ItemValue), typeof(bool), typeof(int), typeof(int), typeof(bool) })]
    public static class ToolbeltDriveCountPatch
    {
        public static void Postfix(Inventory __instance, ItemValue _itemValue, int _seed, int _meta, ref int __result)
        {
            if (__result <= 0 || !DriveRules.IsDrive(_itemValue)) return;
            for (int i = 0; i < __instance.Length; i++)
            {
                ItemStack slot = __instance.GetStackAt(i);
                if (DriveIngredientRules.IsProtectedDrive(slot, _itemValue) && DriveIngredientRules.Matches(slot.itemValue, _seed, _meta))
                    __result -= slot.count;
            }
            if (__result < 0) __result = 0;
        }
    }

    [HarmonyPatch(typeof(Bag), nameof(Bag.DecItem))]
    public static class BagDriveRemovePatch
    {
        public static bool Prefix(Bag __instance, ItemValue _itemValue, int _count, IList<ItemStack> _removedItems, ref int __result)
        {
            if (!DriveRules.IsDrive(_itemValue)) return true;
            int left = _count;
            ItemStackGrid slots = __instance.itemGrid;
            for (int i = 0; i < slots.Length && left > 0; i++)
            {
                ItemStack slot = slots[i];
                if (slot.IsEmpty() || slot.itemValue.type != _itemValue.type) continue;
                if (DriveIngredientRules.IsProtectedDrive(slot, _itemValue)) continue;
                _removedItems?.Add(slot.Clone());
                __instance.SetSlot(i, ItemStack.Empty.Clone());
                left--;
            }
            __result = _count - left;
            return false;
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.DecItem))]
    public static class ToolbeltDriveRemovePatch
    {
        public static bool Prefix(Inventory __instance, ItemValue _itemValue, int _count, IList<ItemStack> _removedItems, ref int __result)
        {
            if (!DriveRules.IsDrive(_itemValue)) return true;
            int left = _count;
            for (int i = 0; i < __instance.Length && left > 0; i++)
            {
                ItemStack slot = __instance.GetStackAt(i);
                if (slot.IsEmpty() || slot.itemValue.type != _itemValue.type) continue;
                if (DriveIngredientRules.IsProtectedDrive(slot, _itemValue)) continue;
                _removedItems?.Add(slot.Clone());
                __instance.clearSlotByIndex(i);
                left--;
            }
            __result = _count - left;
            return false;
        }
    }
}
