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
