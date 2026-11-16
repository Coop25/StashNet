using System;
using System.Collections.Generic;
using System.Linq;

namespace StashNet.Stations
{
    /// <summary>Facts about a Network station block, read from its XML (shared by server and client).</summary>
    public static class StationRules
    {
        /// <summary>The station burns fuel (forge, campfire, chemistry station): Workstation Modules lists "fuel".</summary>
        public static bool HasFuel(Block block)
        {
            if (block == null || !block.Properties.Contains("Workstation", "Modules")) return false;
            return block.Properties.GetString("Workstation", "Modules")
                .Split(',').Any(m => m.Trim().Equals("fuel", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Recipe areas this station crafts, matching vanilla XUiC_RecipeList: CraftingAreaRecipes with
        /// "player" meaning the empty area (backpack recipes), else the block's own name.
        /// </summary>
        public static HashSet<string> CraftingAreas(Block block)
        {
            var areas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (block != null && block.Properties.Contains("Workstation", "CraftingAreaRecipes"))
            {
                foreach (string a in block.Properties.GetString("Workstation", "CraftingAreaRecipes").Split(','))
                {
                    string t = a.Trim();
                    areas.Add(t.Equals("player", StringComparison.OrdinalIgnoreCase) ? "" : t);
                }
            }
            else if (block != null) areas.Add(block.GetBlockName());
            return areas;
        }

        /// <summary>Metals this station melts (Workstation InputMaterials, lower case); empty when it doesn't smelt.</summary>
        public static string[] MeltMetals(Block block)
        {
            if (block == null || !block.Properties.Contains("Workstation", "InputMaterials")) return new string[0];
            return block.Properties.GetString("Workstation", "InputMaterials")
                .Split(',').Select(m => m.Trim().ToLowerInvariant()).Where(m => m.Length > 0).ToArray();
        }

        public const string FuelRefillProperty = "StashNetFuelRefill";
        public const int DefaultFuelRefill = 100;

        /// <summary>How much of the chosen fuel automation keeps in the fuel slots (block property StashNetFuelRefill, default 100).</summary>
        public static int FuelRefillAmount(Block block)
        {
            if (block == null || !block.Properties.Contains(FuelRefillProperty)) return DefaultFuelRefill;
            return int.TryParse(block.Properties.GetString(FuelRefillProperty), out int n) && n > 0 ? n : DefaultFuelRefill;
        }

        public const string OreSlotsProperty = "StashNetOreSlots";
        public const int DefaultOreSlots = 2;

        /// <summary>Ore slots automation may use: the ones the forge window shows (block property StashNetOreSlots, default 2).</summary>
        public static int OreSlots(Block block)
        {
            if (block == null || !block.Properties.Contains(OreSlotsProperty)) return DefaultOreSlots;
            return int.TryParse(block.Properties.GetString(OreSlotsProperty), out int n) && n > 0 ? n : DefaultOreSlots;
        }

        public const string MeltFeedProperty = "StashNetMeltFeed";

        /// <summary>
        /// Raw items automation may feed per metal, in priority order: block property
        /// StashNetMeltFeed = "metal:item,item;metal:item". Only these are ever melted automatically.
        /// </summary>
        public static Dictionary<string, string[]> MeltFeed(Block block)
        {
            var feed = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            if (block == null || !block.Properties.Contains(MeltFeedProperty)) return feed;
            foreach (string part in block.Properties.GetString(MeltFeedProperty).Split(';'))
            {
                string[] kv = part.Split(':');
                if (kv.Length != 2) continue;
                string[] items = kv[1].Split(',').Select(i => i.Trim()).Where(i => i.Length > 0).ToArray();
                if (kv[0].Trim().Length > 0 && items.Length > 0) feed[kv[0].Trim()] = items;
            }
            return feed;
        }

        /// <summary>The melted-metal tally's item (unit_iron ...) for a metal, or null.</summary>
        public static ItemClass UnitOf(string metal) => ItemClass.GetItemClass("unit_" + metal);

        /// <summary>
        /// The server's own recipe for this rule: same name, crafted at this station, and (several recipes can
        /// share a name, e.g. with and without a cooking pot) the one using the rule's ingredients. Null when
        /// none fits or the rule's amounts/output aren't believable for it (RulePlausibility).
        /// </summary>
        public static Recipe FindRecipe(Block block, Core.Stations.StockRule rule) => FindRecipes(block, rule).FirstOrDefault();

        /// <summary>Every recipe FindRecipe would accept, in the game's order (automation prefers one whose tool is present).</summary>
        public static IEnumerable<Recipe> FindRecipes(Block block, Core.Stations.StockRule rule)
        {
            HashSet<string> areas = CraftingAreas(block);
            List<Recipe> all = CraftingManager.GetAllRecipes(rule.Recipe);
            if (all == null) yield break;
            foreach (Recipe r in all)
            {
                if (r == null || r.IsScrap || !areas.Contains(r.craftingArea ?? "")) continue;
                if (Core.Stations.RulePlausibility.Plausible(r.count, Ingredients(r), rule)) yield return r;
            }
        }

        /// <summary>A recipe's ingredients by name (empty slots skipped).</summary>
        public static List<(string Name, int Amount)> Ingredients(Recipe r)
        {
            var list = new List<(string Name, int Amount)>();
            foreach (ItemStack s in r.ingredients)
            {
                if (s == null || s.itemValue == null || s.itemValue.type == 0 || s.itemValue.ItemClass == null) continue;
                list.Add((s.itemValue.ItemClass.GetItemName(), s.count));
            }
            return list;
        }
    }
}
