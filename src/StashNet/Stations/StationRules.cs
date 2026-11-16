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

        /// <summary>The server's own recipe with this name that this station crafts, or null.</summary>
        public static Recipe FindRecipe(Block block, string recipeName)
        {
            HashSet<string> areas = CraftingAreas(block);
            List<Recipe> all = CraftingManager.GetAllRecipes(recipeName);
            return all?.FirstOrDefault(r => r != null && !r.IsScrap && areas.Contains(r.craftingArea ?? ""));
        }
    }
}
