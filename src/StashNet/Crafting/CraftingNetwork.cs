using System;
using System.Collections.Generic;
using StashNet.Core.Protocol;
using StashNet.Core.Stations;
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
        /// <summary>Fuel mode / stock rule requests sent from this window (their replies carry the settings too).</summary>
        private static readonly HashSet<int> settingsRequests = new HashSet<int>();

        /// <summary>The open station's automation settings per the server; null until the first reply.</summary>
        public static StationSettings Settings { get; private set; }

        /// <summary>The network's items per the last listing (all variants), for the fuel button and the stock panel.</summary>
        public static List<ItemAmount> Listing { get; private set; } = new List<ItemAmount>();

        /// <summary>The open station burns fuel (forge, campfire, chemistry station).</summary>
        public static bool HasFuel { get; private set; }

        /// <summary>Bumped whenever Settings or Listing change, so the automation UI knows to redraw.</summary>
        public static int Version { get; private set; }
        /// <summary>Plain (non-variant) network stock by item type id.</summary>
        private static readonly Dictionary<int, int> stockByType = new Dictionary<int, int>();

        /// <summary>A StashNet Crafting Terminal window is open for the local player.</summary>
        public static bool Active { get; private set; }

        /// <summary>Set while vanilla is handling a Craft button click (ItemActionEntryCraft.OnActivated).</summary>
        public static bool InCraftClick { get; set; }

        /// <summary>Set while vanilla cancels a queued craft (XUiC_RecipeStack.HandleOnPress): refunds go to the network.</summary>
        public static bool InCancel { get; set; }

        private const float OutputMoveInterval = 0.5f;
        private static float outputTimer;
        /// <summary>Free stacks in the network per the last listing (minus what we've sent since).</summary>
        private static int freeStacks;

        /// <summary>
        /// A Storage Terminal is open: stored repair kits count for the Repair button (only inside
        /// <see cref="InRepairCheck"/>), using the terminal's own listings.
        /// </summary>
        public static bool RepairMode { get; private set; }

        /// <summary>Set while vanilla checks whether Repair is possible (ItemActionEntryRepair.RefreshEnabled).</summary>
        public static bool InRepairCheck { get; set; }

        /// <summary>Set while vanilla performs a repair: counts must be what the player really carries.</summary>
        public static bool SuppressStock { get; set; }

        /// <summary>Whether the inventory count hooks add network stock right now.</summary>
        public static bool CountsStock => !SuppressStock && (Active || (RepairMode && InRepairCheck));

        public static void OpenForRepair(Vector3i terminalPos)
        {
            stationPos = terminalPos;
            RepairMode = true;
            stockByType.Clear();
        }

        public static void CloseRepair()
        {
            RepairMode = false;
            if (!Active) stockByType.Clear();
        }

        /// <summary>Repair needs kits: pull up to <paramref name="want"/> into the backpack. True when they're there now.</summary>
        public static bool TryFetchForRepair(XUiM_PlayerInventory inventory, ItemValue kit, int want)
        {
            if (want <= 0) return true;
            return TryFetchShortfall(inventory, new List<ItemStack> { new ItemStack(kit, RawCount(inventory, kit) + want) }, 1);
        }

        public static bool IsCraftingTerminal(Block block) =>
            block != null && block.Properties.Contains("StashNetCraftingTerminal");

        public static void Open(Vector3i pos, XUiController group)
        {
            stationPos = pos;
            windowGroup = group;
            Active = true;
            Settings = null;
            Listing = new List<ItemAmount>();
            HasFuel = Stations.StationRules.HasFuel(GameManager.Instance.World?.GetBlock(pos).Block);
            Version++;
            stockByType.Clear();
            freeStacks = 0;
            outputTimer = 0f;
            RequestListing();
        }

        public static void Close()
        {
            Active = false;
            windowGroup = null;
            Settings = null;
            Listing = new List<ItemAmount>();
            stockByType.Clear();
            listRequests.Clear();
            settingsRequests.Clear();
        }

        public static void Tick(float dt)
        {
            if (!Active) return;
            refreshTimer += dt;
            if (refreshTimer >= RefreshInterval) RequestListing();
            outputTimer += dt;
            if (outputTimer >= OutputMoveInterval)
            {
                outputTimer = 0f;
                MoveOutputToNetwork();
            }
        }

        /// <summary>
        /// Window open: finished crafts land in the on-screen output slots. Move them into the
        /// network when it clearly has room; otherwise leave them there (vanilla behavior).
        /// </summary>
        private static void MoveOutputToNetwork()
        {
            if (!OutputToStorage) return;
            XUiC_WorkstationOutputGrid grid = windowGroup?.GetChildByType<XUiC_WorkstationOutputGrid>();
            if (grid == null) return;
            ItemStack[] slots = grid.GetSlots();
            var request = NewRequest(TerminalOp.Deposit);
            for (int i = 0; i < slots.Length; i++)
            {
                ItemStack s = slots[i];
                if (s == null || s.IsEmpty() || !Storable(s.itemValue)) continue;
                int stacksNeeded = (s.count + Math.Max(1, s.itemValue.ItemClass.Stacknumber.Value) - 1) / Math.Max(1, s.itemValue.ItemClass.Stacknumber.Value);
                if (stacksNeeded > freeStacks) continue;
                freeStacks -= stacksNeeded;
                request.Items.Add(new ItemAmount(Drives.ItemCodec.NameOf(s.itemValue), Drives.ItemCodec.EncodeVariant(s.itemValue), s.count));
                slots[i] = ItemStack.Empty.Clone();
            }
            if (request.Items.Count == 0) return;
            // Removed from the output first, then sent; leftovers come back to the backpack (never lost).
            grid.SetSlots(slots);
            grid.UpdateData(slots);
            grid.IsDirty = true;
            TerminalClient.Send(request);
            RequestListing();
        }

        /// <summary>Cancelled-craft refund: send it to the network instead of the backpack.</summary>
        public static bool TryRefundToNetwork(ItemStack stack)
        {
            if (!Active || !OutputToStorage || stack == null || stack.IsEmpty() || !Storable(stack.itemValue)) return false;
            var request = NewRequest(TerminalOp.Deposit);
            request.Items.Add(new ItemAmount(Drives.ItemCodec.NameOf(stack.itemValue), Drives.ItemCodec.EncodeVariant(stack.itemValue), stack.count));
            TerminalClient.Send(request);
            RequestListing();
            return true;
        }

        /// <summary>
        /// The output switch says "to storage". False until the server's first reply (settings unknown):
        /// nothing goes to storage before we know (v0.19 rule).
        /// </summary>
        public static bool OutputToStorage => Settings != null && Settings.OutputOn;

        public static void SendOutput(bool on) => SendSettings(TerminalOp.SetOutput, on ? "on" : "off");

        public static void SendMeltTarget(MeltTarget melt) => SendSettings(TerminalOp.SetMeltTarget, melt.ToLine(false));

        public static void SendRemoveMelt(string metal) => SendSettings(TerminalOp.RemoveMeltTarget, metal);

        private static bool Storable(ItemValue iv) =>
            iv != null && !Blocks.DriveRules.IsDrive(iv) && Drives.ItemCodec.CanStore(iv);

        private static TerminalRequest NewRequest(TerminalOp op) =>
            new TerminalRequest { Op = op, X = stationPos.x, Y = stationPos.y, Z = stationPos.z };

        /// <summary>Called by TerminalClient for every response; uses listings that answer our requests.</summary>
        public static void OnResponse(TerminalResponse response)
        {
            bool settingsReply = settingsRequests.Remove(response.RequestId);
            bool ours = (listRequests.Remove(response.RequestId) || settingsReply) && Active;
            if (ours) TakeStationReply(response);
            bool forRepair = RepairMode && !Active && response.HasListing;
            if (!ours && !forRepair) return;
            stockByType.Clear();
            freeStacks = response.HasListing ? Math.Max(0, response.MaxStacks - response.UsedStacks) : 0;
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

        /// <summary>Settings and listing from a reply to this station's own requests.</summary>
        private static void TakeStationReply(TerminalResponse response)
        {
            if (response.Station != null)
            {
                try
                {
                    Settings = StationSettings.Parse(response.Station);
                }
                catch (FormatException e)
                {
                    Log.Warning($"[StashNet] bad station settings from the server: {e.Message}");
                }
            }
            if (response.HasListing) Listing = response.Listing;
            foreach (TerminalMessage m in response.Messages)
            {
                if (m.Key != "stashnetRuleRefused" && m.Key != "stashnetRulesFull" && m.Key != "stashnetMeltsFull") continue;
                EntityPlayerLocal player = GameManager.Instance.World?.GetPrimaryPlayer();
                string text = m.Arg >= 0 ? string.Format(Localization.Get(m.Key), m.Arg) : Localization.Get(m.Key);
                if (player != null) GameManager.ShowTooltip(player, text, string.Empty, "ui_denied");
            }
            Version++;
        }

        public static void SendFuelMode(string mode) => SendSettings(TerminalOp.SetFuelMode, mode);

        public static void SendStockRule(StockRule rule) => SendSettings(TerminalOp.SetStockRule, rule.ToLine());

        public static void SendRemoveRule(string item) => SendSettings(TerminalOp.RemoveStockRule, item);

        private static void SendSettings(TerminalOp op, string text)
        {
            if (!Active) return;
            var request = NewRequest(op);
            request.Text = text;
            settingsRequests.Add(TerminalClient.NextRequestId);
            TerminalClient.Send(request);
        }

        /// <summary>Network stock of this item (plain items only; 0 when inactive).</summary>
        public static int Count(ItemValue iv) => CountsStock ? Stock(iv) : 0;

        /// <summary>Network stock of this plain item per the last listing (station or repair mode), ignoring the count gates.</summary>
        public static int Stock(ItemValue iv)
        {
            if (!(Active || RepairMode) || iv == null || iv.HasQuality) return 0;
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
                if (need.itemValue.HasQuality || Stock(need.itemValue) < missing) return false;
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
        internal static float PerCraftAmount(Recipe recipe, ItemStack ing, EntityPlayerLocal player, int tier)
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
