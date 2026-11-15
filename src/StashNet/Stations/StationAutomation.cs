using System;
using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Drives;
using StashNet.Core.Stations;
using StashNet.Crafting;
using StashNet.Network;

namespace StashNet.Stations
{
    /// <summary>
    /// SERVER ONLY. Runs a Network station's automation while nobody has it open: auto-stock
    /// (queue crafts from stored ingredients until storage holds the target), auto-fuel (refill the
    /// chosen fuel from storage) and auto-fire (light with work, put out without).
    /// Never touches XUi. Items leave storage only after every check passed; drives are saved
    /// before the station is marked modified.
    /// </summary>
    public static class StationAutomation
    {
        private const float IntervalSeconds = 5f;
        /// <summary>Queue slots auto-stock leaves free for crafts the players start by hand.</summary>
        private const int FreeSlotsKept = 1;

        private static readonly Dictionary<Vector3i, float> nextRun = new Dictionary<Vector3i, float>();

        public static void Tick(World world, TileEntityWorkstation te)
        {
            if (world == null || te == null) return;
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
            if (te.IsUserAccessing() || LockManager.Instance.IsLockedServer(te)) return;

            Vector3i pos = te.ToWorldPos();
            float now = UnityEngine.Time.time;
            if (nextRun.TryGetValue(pos, out float t) && now < t) return;
            nextRun[pos] = now + IntervalSeconds;

            Block block = world.GetBlock(pos).Block;
            if (!CraftingNetwork.IsCraftingTerminal(block)) return;
            try
            {
                StationSettings settings = StationSettingsStore.Get(pos, block);
                if (settings.IsDefault) return;

                StashNetNetwork net = null;
                bool needsNetwork = settings.Rules.Count > 0 || FuelModes.IsFuelItem(settings.FuelMode);
                if (needsNetwork && !StashNetNetwork.TryOpen(world, pos, out net, out _)) net = null;

                bool changed = false;
                if (net != null) changed |= RunStockRules(world, te, block, settings, net);
                changed |= RunFire(te, settings, net);
                if (changed) te.SetModified();
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] automation at {pos}: {e.Message}");
            }
        }

        // ---------------------------------------------------------------- auto-stock

        private static bool RunStockRules(World world, TileEntityWorkstation te, Block block, StationSettings settings, StashNetNetwork net)
        {
            bool changed = false;
            foreach (StockRule rule in settings.Rules)
            {
                if (FreeQueueSlots(te) <= FreeSlotsKept) break;
                if (!OwnerAllowed(net, rule.Owner)) continue;
                Recipe serverRecipe = StationRules.FindRecipe(block, rule.Recipe);
                if (serverRecipe == null || !HasTool(te, serverRecipe.craftingToolType)) continue;
                if (TryQueue(te, rule, serverRecipe, net)) changed = true;
            }
            return changed;
        }

        private static bool OwnerAllowed(StashNetNetwork net, string owner)
        {
            if (!NetworkLock.IsLocked(net.CoreLock)) return true;
            if (string.IsNullOrEmpty(owner) || !PlatformUserIdentifierAbs.TryFromCombinedString(owner, out PlatformUserIdentifierAbs id)) return false;
            return NetworkLock.IsAllowed(net.CoreLock, id);
        }

        private static bool HasTool(TileEntityWorkstation te, int toolType)
        {
            if (toolType == 0) return true;
            ItemStack[] tools = te.Tools;
            return tools != null && tools.Any(s => s != null && !s.IsEmpty() && s.itemValue.type == toolType);
        }

        private static bool TryQueue(TileEntityWorkstation te, StockRule rule, Recipe serverRecipe, StashNetNetwork net)
        {
            int outputType = serverRecipe.itemValueType;
            int have = StoredCount(net, rule.Item) + QueuedCount(te, outputType) + OutputCount(te, outputType);
            int needed = StockMath.CraftsNeeded(rule.Target, have, rule.OutputCount);
            if (needed <= 0) return false;

            // Each ingredient comes from the forge's melted-metal tallies (unit_iron etc.) when the
            // station holds that material, else from storage.
            var sources = new List<(string Name, int PerCraft, int TallySlot, int Stored)>();
            foreach (var (name, amount) in rule.Ingredients)
            {
                int slot = TallySlot(te, name);
                int stored = slot >= 0 ? te.input[slot].count : StoredCount(net, name, plainOnly: true);
                sources.Add((name, amount, slot, stored));
            }
            int crafts = Math.Min(needed, StockMath.CraftsAffordable(sources.Select(s => (s.PerCraft, s.Stored))));
            crafts = Math.Min(crafts, short.MaxValue);
            if (crafts <= 0) return false;

            // Take from storage first; anything short puts back what was taken and gives up.
            var taken = new List<(string Name, int Count)>();
            foreach (var s in sources.Where(s => s.TallySlot < 0))
            {
                int want = s.PerCraft * crafts;
                int got = net.Storage.Extract(s.Name, null, want);
                taken.Add((s.Name, got));
                if (got < want)
                {
                    foreach (var (name, count) in taken) net.Storage.Insert(name, null, count);
                    return false;
                }
            }

            int index = HighestFreeSlot(te);
            if (index < 0)
            {
                foreach (var (name, count) in taken) net.Storage.Insert(name, null, count);
                return false;
            }
            net.SaveDirty();
            foreach (var s in sources.Where(s => s.TallySlot >= 0))
            {
                ItemStack tally = te.input[s.TallySlot].Clone();
                tally.count -= s.PerCraft * crafts;
                te.input[s.TallySlot] = tally;
            }

            var recipe = new Recipe
            {
                itemValueType = outputType,
                count = rule.OutputCount,
                craftingArea = serverRecipe.craftingArea,
                craftExpGain = serverRecipe.craftExpGain,
                craftingTime = rule.CraftTime,
                craftingToolType = serverRecipe.craftingToolType,
                craftingTier = rule.Tier,
                tags = serverRecipe.tags,
            };
            recipe.AddIngredients(rule.Ingredients.Select(i => new ItemStack(ItemClass.GetItem(i.Name), i.Amount)).ToList());

            te.queue[index] = new RecipeQueueItem
            {
                Recipe = recipe,
                Multiplier = (short)crafts,
                CraftingTimeLeft = rule.CraftTime,
                OneItemCraftTime = rule.CraftTime,
                IsCrafting = index == te.queue.Length - 1,
                Quality = (byte)rule.Tier,
                StartingEntityId = -1, // no crafting XP for automatic crafts
            };
            return true;
        }

        private static bool SlotFree(RecipeQueueItem q) => q == null || q.Recipe == null || q.Multiplier <= 0;

        private static int FreeQueueSlots(TileEntityWorkstation te) => te.queue.Count(SlotFree);

        /// <summary>Like XUiC_CraftingQueue.AddRecipeToCraft: the slot nearest the crafting end.</summary>
        private static int HighestFreeSlot(TileEntityWorkstation te)
        {
            for (int i = te.queue.Length - 1; i >= 0; i--)
            {
                if (SlotFree(te.queue[i])) return i;
            }
            return -1;
        }

        private static int StoredCount(StashNetNetwork net, string name, bool plainOnly = false) =>
            net.Storage.Aggregate().Where(i => i.Name == name && (!plainOnly || i.Variant == null)).Sum(i => i.Count);

        private static int QueuedCount(TileEntityWorkstation te, int type) =>
            te.queue.Where(q => !SlotFree(q) && q.Recipe.itemValueType == type).Sum(q => q.Multiplier * q.Recipe.count);

        private static int OutputCount(TileEntityWorkstation te, int type) =>
            te.output.Where(s => s != null && !s.IsEmpty() && s.itemValue.type == type).Sum(s => s.count);

        /// <summary>Index of the forge's melted-metal tally slot holding this item, or -1.</summary>
        private static int TallySlot(TileEntityWorkstation te, string itemName)
        {
            if (!te.isModuleUsed[4] || te.materialNames == null) return -1;
            ItemValue iv = ItemClass.GetItem(itemName);
            if (iv == null || iv.IsEmpty()) return -1;
            for (int i = te.input.Length - te.materialNames.Length; i < te.input.Length; i++)
            {
                if (i >= 0 && te.input[i] != null && te.input[i].itemValue.type == iv.type) return i;
            }
            return -1;
        }

        // ---------------------------------------------------------------- auto-fuel / auto-fire

        private const int FuelModule = 3, MaterialInputModule = 4;

        private static bool RunFire(TileEntityWorkstation te, StationSettings settings, StashNetNetwork net)
        {
            bool hasFuelModule = te.isModuleUsed[FuelModule];
            if (!hasFuelModule || settings.FuelMode == FuelModes.Manual) return false;

            bool queued = te.queue.Any(q => !SlotFree(q));
            int[] rawOre = null;
            if (te.isModuleUsed[MaterialInputModule])
            {
                int raw = te.input.Length - (te.materialNames?.Length ?? 0);
                rawOre = te.input.Take(Math.Max(0, raw)).Select(s => s == null || s.IsEmpty() ? 0 : s.count).ToArray();
            }
            bool work = StationWork.HasWork(queued, XUiM_Recipes.DisableSmelter, rawOre);

            bool changed = false;
            if (work && net != null && FuelModes.IsFuelItem(settings.FuelMode)) changed |= Refuel(te, settings.FuelMode, net);

            switch (StationWork.Decide(settings.FuelMode, hasFuelModule, work, te.IsBurning, te.BurnTotalTimeLeft, te.IsBesideWater))
            {
                case FireAction.Light:
                    te.IsBurning = true;
                    return true;
                case FireAction.PutOut:
                    te.IsBurning = false;
                    return true;
                default:
                    return changed;
            }
        }

        /// <summary>Top the fuel slots up to one full stack of the chosen fuel. Never moves other fuel.</summary>
        private static bool Refuel(TileEntityWorkstation te, string fuelName, StashNetNetwork net)
        {
            ItemValue fuel = ItemClass.GetItem(fuelName);
            if (fuel == null || fuel.IsEmpty() || ItemClass.GetFuelValue(fuel) <= 0) return false;
            int stackSize = Math.Max(1, fuel.ItemClass.Stacknumber.Value);

            ItemStack[] slots = te.fuel;
            int loaded = slots.Where(s => s != null && !s.IsEmpty() && s.itemValue.type == fuel.type).Sum(s => s.count);
            int room = slots.Sum(s => s == null || s.IsEmpty() ? stackSize : s.itemValue.type == fuel.type ? Math.Max(0, stackSize - s.count) : 0);
            int want = Math.Min(stackSize - loaded, room);
            if (want <= 0) return false;

            int got = net.Storage.Extract(fuelName, null, want);
            if (got <= 0) return false;
            net.SaveDirty();

            int left = got;
            for (int i = 0; i < slots.Length && left > 0; i++) // partial stacks of this fuel first
            {
                ItemStack s = slots[i];
                if (s == null || s.IsEmpty() || s.itemValue.type != fuel.type) continue;
                int add = Math.Min(left, stackSize - s.count);
                if (add <= 0) continue;
                ItemStack c = s.Clone();
                c.count += add;
                slots[i] = c;
                left -= add;
            }
            for (int i = 0; i < slots.Length && left > 0; i++) // then empty slots
            {
                if (slots[i] != null && !slots[i].IsEmpty()) continue;
                int add = Math.Min(left, stackSize);
                slots[i] = new ItemStack(fuel.Clone(), add);
                left -= add;
            }
            if (left > 0)
            {
                net.Storage.Insert(fuelName, null, left);
                net.SaveDirty();
            }
            return true;
        }
    }
}
