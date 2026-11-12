using System;
using System.Collections.Generic;
using StashNet.Core.Protocol;
using StashNet.Network;

namespace StashNet.Crafting
{
    /// <summary>
    /// CLIENT side of the Crafting Terminal (Milestone 6). While a Crafting Terminal's workbench
    /// window is open, keeps a listing of the network's plain items (refreshed every 2 s via the
    /// normal List request) so the crafting hooks can count them as "available".
    ///
    /// Ingredients are never taken from the network directly: when the player clicks Craft, the
    /// shortfall is withdrawn into the backpack first (normal Withdraw protocol), and vanilla
    /// crafting then takes everything from the backpack as usual.
    /// </summary>
    public static class CraftingNetwork
    {
        private const float RefreshInterval = 2f;

        private static Vector3i stationPos;
        private static XUiController windowGroup;
        private static float refreshTimer;
        private static readonly HashSet<int> listRequests = new HashSet<int>();
        /// <summary>Plain (non-variant) network stock by item type id.</summary>
        private static readonly Dictionary<int, int> stockByType = new Dictionary<int, int>();

        /// <summary>A StashNet Crafting Terminal window is open for the local player.</summary>
        public static bool Active { get; private set; }

        /// <summary>Set while vanilla is handling a Craft button click (ItemActionEntryCraft.OnActivated).</summary>
        public static bool InCraftClick { get; set; }

        public static bool IsCraftingTerminal(Block block) =>
            block != null && block.Properties.Contains("StashNetCraftingTerminal");

        public static void Open(Vector3i pos, XUiController group)
        {
            stationPos = pos;
            windowGroup = group;
            Active = true;
            stockByType.Clear();
            RequestListing();
        }

        public static void Close()
        {
            Active = false;
            windowGroup = null;
            stockByType.Clear();
            listRequests.Clear();
        }

        public static void Tick(float dt)
        {
            if (!Active) return;
            refreshTimer += dt;
            if (refreshTimer >= RefreshInterval) RequestListing();
        }

        /// <summary>Called by TerminalClient for every response; uses listings that answer our requests.</summary>
        public static void OnResponse(TerminalResponse response)
        {
            if (!listRequests.Remove(response.RequestId) || !Active) return;
            stockByType.Clear();
            if (response.HasListing)
            {
                foreach (ItemAmount a in response.Listing)
                {
                    if (a.Variant != null) continue; // tools/weapons etc. aren't recipe ingredients
                    ItemValue iv = ItemClass.GetItem(a.Name);
                    if (iv == null || iv.IsEmpty()) continue;
                    stockByType.TryGetValue(iv.type, out int n);
                    stockByType[iv.type] = n + a.Count;
                }
            }
            RefreshUi();
        }

        /// <summary>Network stock of this item (plain items only; 0 when inactive).</summary>
        public static int Count(ItemValue iv)
        {
            if (!Active || iv == null || iv.HasQuality) return 0;
            return stockByType.TryGetValue(iv.type, out int n) ? n : 0;
        }

        /// <summary>Network stock as item stacks, for vanilla code that scans a stack list.</summary>
        public static List<ItemStack> Stacks()
        {
            var list = new List<ItemStack>();
            if (!Active) return list;
            foreach (var kv in stockByType) list.Add(new ItemStack(new ItemValue(kv.Key), kv.Value));
            return list;
        }

        /// <summary>
        /// For a Craft click: withdraws what the backpack+toolbelt lack into the backpack.
        /// Returns true if the backpack now has everything (always the case when it did already,
        /// and in single-player/host when the network had enough). On a remote client the
        /// withdraw arrives later, so this returns false and the player clicks Craft again.
        /// </summary>
        public static bool TryFetchShortfall(XUiM_PlayerInventory inventory, IList<ItemStack> needs, int multiplier)
        {
            var shortfalls = new List<ItemAmount>();
            foreach (ItemStack need in needs)
            {
                int missing = need.count * multiplier - RawCount(inventory, need.itemValue);
                if (missing <= 0) continue;
                if (need.itemValue.HasQuality || Count(need.itemValue) < missing) return false;
                shortfalls.Add(new ItemAmount(need.itemValue.ItemClass.GetItemName(), null, missing));
            }
            if (shortfalls.Count == 0) return true;

            foreach (ItemAmount s in shortfalls)
            {
                var req = new TerminalRequest { Op = TerminalOp.Withdraw, X = stationPos.x, Y = stationPos.y, Z = stationPos.z };
                req.Items.Add(s);
                TerminalClient.Send(req);
            }
            RequestListing();

            foreach (ItemStack need in needs)
            {
                if (RawCount(inventory, need.itemValue) < need.count * multiplier) return false;
            }
            return true;
        }

        /// <summary>
        /// Largest craft count (≤ <paramref name="upTo"/>) whose network shortfall fits in the
        /// backpack: empty unlocked slots are shared between ingredients; existing partial stacks
        /// of an ingredient absorb some of its shortfall first. Always at least 1.
        /// </summary>
        public static int MaxCraftsThatFit(XUiM_PlayerInventory inventory, EntityPlayerLocal player, Recipe recipe, int upTo)
        {
            var perCraft = new List<(ItemValue item, float amount)>();
            int tier = recipe.craftingTier == -1 ? recipe.GetCraftingTier(player) : recipe.craftingTier;
            foreach (ItemStack ing in recipe.ingredients)
            {
                if (ing == null || ing.itemValue.type == 0) continue;
                if (ing.itemValue.HasQuality) return upTo; // vanilla already limits these to 1
                float amount = PerCraftAmount(recipe, ing, player, tier);
                if (amount >= 1f) perCraft.Add((ing.itemValue, amount));
            }
            if (perCraft.Count == 0) return upTo;

            int lo = 1, hi = upTo;
            if (!Fits(inventory, player.bag, perCraft, 1)) return 1; // vanilla will refuse; nothing to cap
            while (lo < hi)
            {
                int mid = lo + (hi - lo + 1) / 2;
                if (Fits(inventory, player.bag, perCraft, mid)) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>Ingredient amount per craft, using the same modifiers as vanilla's craft-count math.</summary>
        private static float PerCraftAmount(Recipe recipe, ItemStack ing, EntityPlayerLocal player, int tier)
        {
            float amount = ing.count;
            if (!recipe.UseIngredientModifier) return amount;
            amount = (int)EffectManager.GetValue(PassiveEffects.CraftingIngredientCount, null, ing.count, player, recipe,
                FastTags<TagGroup.Global>.Parse(ing.itemValue.ItemClass.GetItemName()),
                calcEquipment: true, calcHoldingItem: true, calcProgression: true, calcBuffs: true, calcChallenges: true, tier);
            if (amount > 0f)
            {
                amount = (int)(amount * XUiM_Recipes.GetCraftingInputModifier(recipe));
                if (XUiM_Recipes.CraftingInputModifier > 0f) amount = UnityEngine.Mathf.Max(1f, amount);
            }
            return amount;
        }

        private static bool Fits(XUiM_PlayerInventory inventory, Bag bag, List<(ItemValue item, float amount)> perCraft, int crafts)
        {
            int emptySlots = 0;
            PackedBoolArray locked = bag.LockedSlots;
            for (int i = 0; i < bag.SlotCount; i++)
            {
                if (bag.ItemGrid[i].IsEmpty() && !(locked != null && i < locked.Length && locked[i])) emptySlots++;
            }

            int slotsNeeded = 0;
            foreach (var (item, amount) in perCraft)
            {
                int shortfall = (int)Math.Ceiling(amount * crafts) - RawCount(inventory, item);
                if (shortfall <= 0) continue;
                int stackSize = Math.Max(1, item.ItemClass.Stacknumber.Value);
                int partialRoom = 0;
                for (int i = 0; i < bag.SlotCount; i++)
                {
                    ItemStack s = bag.ItemGrid[i];
                    if (!s.IsEmpty() && s.itemValue.type == item.type && !s.itemValue.HasQuality) partialRoom += Math.Max(0, stackSize - s.count);
                }
                int remaining = shortfall - partialRoom;
                if (remaining > 0) slotsNeeded += (remaining + stackSize - 1) / stackSize;
                if (slotsNeeded > emptySlots) return false;
            }
            return true;
        }

        /// <summary>What the player actually carries (backpack + toolbelt), ignoring the network.</summary>
        public static int RawCount(XUiM_PlayerInventory inventory, ItemValue iv) =>
            inventory.backpack.GetItemCount(iv) + inventory.toolbelt.GetItemCount(iv);

        private static void RequestListing()
        {
            refreshTimer = 0f;
            listRequests.Add(TerminalClient.NextRequestId);
            TerminalClient.Send(new TerminalRequest { Op = TerminalOp.List, X = stationPos.x, Y = stationPos.y, Z = stationPos.z });
        }

        private static void RefreshUi()
        {
            if (windowGroup == null) return;
            foreach (XUiC_RecipeList list in windowGroup.GetChildrenByType<XUiC_RecipeList>())
            {
                list.resortRecipes = true;
                list.IsDirty = true;
            }
            windowGroup.SetAllChildrenDirty();
        }
    }
}
