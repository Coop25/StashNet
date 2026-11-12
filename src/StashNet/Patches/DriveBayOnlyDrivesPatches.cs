using HarmonyLib;
using StashNet.Blocks;

namespace StashNet.Patches
{
    /// <summary>
    /// Every UI path that puts an item into a container (drag-drop, drop-one, shift-click) asks
    /// ItemStack.CanMoveTo first. While a Drive Bay is open, refuse anything that isn't a drive.
    /// </summary>
    [HarmonyPatch(typeof(ItemStack), nameof(ItemStack.CanMoveTo))]
    public static class ItemStackCanMoveToPatch
    {
        public static void Postfix(ItemStack __instance, XUiC_ItemStack.StackLocationTypes locationType, ref bool __result)
        {
            if (!__result || locationType != XUiC_ItemStack.StackLocationTypes.LootContainer) return;
            if (__instance == null || __instance.IsEmpty()) return;
            if (DriveRules.IsDrive(__instance.itemValue)) return;
            if (DriveRules.OpenContainerIsDriveBay()) __result = false;
        }
    }

    // Backups: ItemStack.CanMoveTo is small enough that the runtime may inline it into its callers,
    // which would silently bypass the patch above. These hook the (larger / virtual) callers directly.

    /// <summary>Drag-and-drop swap into a slot.</summary>
    [HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.CanSwap))]
    public static class ItemStackSlotCanSwapPatch
    {
        public static void Postfix(XUiC_ItemStack __instance, ItemStack _stack, ref bool __result)
        {
            if (__result && Blocked(__instance, _stack)) __result = false;
        }

        internal static bool Blocked(XUiC_ItemStack slot, ItemStack stack)
        {
            return slot != null
                && slot.StackLocation == XUiC_ItemStack.StackLocationTypes.LootContainer
                && stack != null && !stack.IsEmpty()
                && !DriveRules.IsDrive(stack.itemValue)
                && DriveRules.OpenContainerIsDriveBay();
        }
    }

    /// <summary>Right-click "drop one" into a slot.</summary>
    [HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.HandleDropOne))]
    public static class ItemStackSlotDropOnePatch
    {
        public static bool Prefix(XUiC_ItemStack __instance)
        {
            return !ItemStackSlotCanSwapPatch.Blocked(__instance, __instance?.xui?.DragAndDropWindow?.CurrentStack);
        }
    }

    /// <summary>Shift-click from the backpack into the open container.</summary>
    [HarmonyPatch(typeof(XUiM_LootContainer), nameof(XUiM_LootContainer.AddItem))]
    public static class LootContainerAddItemPatch
    {
        public static bool Prefix(ItemStack _itemStack, XUi _xui, ref bool __result)
        {
            if (_itemStack == null || _itemStack.IsEmpty() || DriveRules.IsDrive(_itemStack.itemValue)) return true;
            if (!DriveRules.IsDriveBay(_xui?.LootContainer) || !DriveRules.OpenContainerIsDriveBay()) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>
    /// The stash buttons move items directly and skip CanMoveTo. When the destination is a
    /// Drive Bay, mark every non-drive source slot as ignored so only drives move.
    /// </summary>
    [HarmonyPatch(typeof(XUiM_LootContainer), nameof(XUiM_LootContainer.StashItems))]
    public static class StashItemsPatch
    {
        public static void Prefix(XUiC_ItemStackGrid _srcGrid, IInventory _dstInventory, ref PackedBoolArray _ignoredSlots)
        {
            if (_srcGrid == null || !(_dstInventory is TEFeatureStorage storage) || !DriveRules.IsDriveBay(storage)) return;

            XUiC_ItemStack[] slots = _srcGrid.GetItemStackControllers();
            var ignored = new PackedBoolArray(slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                bool alreadyIgnored = _ignoredSlots != null && i < _ignoredSlots.Length && _ignoredSlots[i];
                ItemStack stack = slots[i].ItemStack;
                ignored[i] = alreadyIgnored || (!stack.IsEmpty() && !DriveRules.IsDrive(stack.itemValue));
            }
            _ignoredSlots = ignored;
        }
    }
}
