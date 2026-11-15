using System.Collections.Generic;
using StashNet.Core.Stations;
using StashNet.Crafting;

namespace StashNet.Stations
{
    /// <summary>
    /// CLIENT. Turns a recipe into a stock rule exactly as the Craft button would queue it for this
    /// player (ItemActionEntryCraft.OnActivated / hasItems): output count, craft time, crafting tier
    /// and per-craft ingredient amounts after the player's perks.
    /// </summary>
    public static class StockRuleCapture
    {
        /// <summary>Null when the recipe can't be automated: locked, no ingredients, or it uses quality items (tools, weapons) as ingredients.</summary>
        public static StockRule FromRecipe(XUi xui, Recipe recipe, int target)
        {
            EntityPlayerLocal player = xui?.playerUI?.entityPlayer;
            if (player == null || recipe == null || recipe.IsScrap) return null;
            if (!XUiM_Recipes.GetRecipeIsUnlocked(xui, recipe)) return null;
            ItemClass output = recipe.GetOutputItemClass();
            if (output == null) return null;

            int tier = recipe.GetCraftingTier(player);
            var ingredients = new List<(string Name, int Amount)>();
            foreach (ItemStack ing in recipe.ingredients)
            {
                if (ing == null || ing.itemValue == null || ing.itemValue.type == 0) continue;
                if (ing.itemValue.HasQuality) return null;
                int amount = (int)CraftingNetwork.PerCraftAmount(recipe, ing, player, tier);
                if (amount > 0) ingredients.Add((ing.itemValue.ItemClass.GetItemName(), amount));
            }
            if (ingredients.Count == 0) return null;

            return new StockRule
            {
                Item = output.GetItemName(),
                Target = target,
                Recipe = recipe.GetName(),
                OutputCount = System.Math.Max(1, XUiM_Recipes.GetRecipeCraftOutputCount(xui, recipe)),
                CraftTime = System.Math.Max(0f, XUiM_Recipes.GetRecipeCraftTime(xui, recipe)),
                Tier = System.Math.Max(0, System.Math.Min(StockRule.MaxTier, tier)),
                Ingredients = ingredients,
            };
        }
    }
}
