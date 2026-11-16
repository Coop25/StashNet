using System;
using System.Collections.Generic;
using System.Linq;

namespace StashNet.Core.Stations
{
    /// <summary>
    /// SERVER check of a stock rule a player's game sent: it must use exactly the server recipe's
    /// ingredients, with amounts and output a perk could explain. Perks change these (that's why the
    /// player's game captures them), so only clearly impossible values are refused: an amount below a
    /// quarter of the recipe's, or output above four times it.
    /// </summary>
    public static class RulePlausibility
    {
        public const int MaxOutputFactor = 4;
        public const int MinAmountDivisor = 4;

        public static bool Plausible(int recipeCount, IList<(string Name, int Amount)> recipeIngredients, StockRule rule)
        {
            if (rule.OutputCount < 1 || rule.OutputCount > Math.Max(1, recipeCount) * MaxOutputFactor) return false;
            // Every rule ingredient must be one of the recipe's (one a perk reduced to 0 may be missing; none may be added).
            if (rule.Ingredients.Count == 0) return false;
            var names = new HashSet<string>(recipeIngredients.Select(i => i.Name), StringComparer.Ordinal);
            if (rule.Ingredients.Any(i => !names.Contains(i.Name)) || rule.Ingredients.Select(i => i.Name).Distinct().Count() != rule.Ingredients.Count) return false;
            foreach (var (name, amount) in rule.Ingredients)
            {
                int baseAmount = recipeIngredients.First(i => i.Name == name).Amount;
                int min = Math.Max(1, baseAmount / MinAmountDivisor);
                if (amount < min) return false;
            }
            return true;
        }
    }
}
