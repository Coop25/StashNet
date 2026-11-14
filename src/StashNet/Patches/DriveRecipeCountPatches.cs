using System.Collections.Generic;
using HarmonyLib;
using StashNet.Blocks;
using StashNet.Drives;

namespace StashNet.Patches
{
    /// <summary>
    /// The recipe list ("craftable now" sorting) and the craft-count max read item stacks directly
    /// instead of Bag/Inventory.GetItemCount, so they'd count drives that hold items. Keep them in line
    /// with <see cref="DriveIngredientRules"/>: only EMPTY drives are ingredients.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_RecipeList), nameof(XUiC_RecipeList.BuildRecipeInfosList))]
    public static class RecipeListSkipsFullDrivesPatch
    {
        public static void Prefix(List<ItemStack> _items)
        {
            // _items is a fresh list of clones built for this call, so removing from it is safe.
            _items?.RemoveAll(s => s != null && !s.IsEmpty() && DriveRules.IsDrive(s.itemValue) && DriveItem.HoldsItems(s.itemValue));
        }
    }

    [HarmonyPatch(typeof(XUiC_RecipeCraftCount), nameof(XUiC_RecipeCraftCount.calcMaxCraftable))]
    public static class CraftCountSkipsFullDrivesPatch
    {
        public static void Postfix(XUiC_RecipeCraftCount __instance, ref int __result)
        {
            Recipe recipe = __instance.recipe;
            XUiM_PlayerInventory inventory = __instance.xui?.PlayerInventory;
            if (recipe == null || inventory == null || __result <= 1) return;

            foreach (ItemStack need in recipe.ingredients)
            {
                if (need == null || need.count <= 0 || !DriveRules.IsDrive(need.itemValue)) continue;
                // GetItemCount goes through the patched Bag/Inventory counts, so it sees empty drives only.
                int crafts = inventory.GetItemCount(need.itemValue) / need.count;
                if (crafts < __result) __result = System.Math.Max(1, crafts);
            }
        }
    }
}
