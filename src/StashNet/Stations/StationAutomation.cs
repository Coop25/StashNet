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
                bool needsNetwork = settings.Rules.Count > 0 || settings.Melts.Count > 0 || FuelModes.IsFuelItem(settings.FuelMode);
                if (needsNetwork && !StashNetNetwork.TryOpen(world, pos, out net, out _)) net = null;

                bool changed = false;
                if (net != null) changed |= RunStockRules(world, te, block, settings, net);
                if (net != null) changed |= RunMelts(te, block, settings, net); // before the fire, so it sees the new ore
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
            int stackSize = Math.Max(1, ItemClass.GetForId(outputType)?.Stacknumber.Value ?? 1);
            crafts = Math.Min(crafts, Math.Min(StockMath.MaxCraftsPerEntry(rule.OutputCount, stackSize), short.MaxValue));
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
                craftExpGain = 0, // no crafting XP for automatic crafts; also marks them for AutoCraftRecordPatch
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

        // ---------------------------------------------------------------- melt targets

        /// <summary>
        /// Forge: for each melt target, pull only the listed raw items (StashNetMeltFeed) from storage
        /// into the ore slots, just enough that the melted tally reaches the target. Stops on its own.
        /// </summary>
        private static bool RunMelts(TileEntityWorkstation te, Block block, StationSettings settings, StashNetNetwork net)
        {
            if (settings.Melts.Count == 0 || XUiM_Recipes.DisableSmelter) return false;
            if (!te.isModuleUsed[MaterialInputModule] || te.materialNames == null || te.input == null) return false;
            int rawCount = te.input.Length - te.materialNames.Length;
            if (rawCount <= 0) return false;
            Dictionary<string, string[]> feed = StationRules.MeltFeed(block);

            bool changed = false;
            foreach (MeltTarget melt in settings.Melts)
            {
                if (!OwnerAllowed(net, melt.Owner)) continue;
                int metalIndex = Array.FindIndex(te.materialNames, m => m.Equals(melt.Metal, StringComparison.OrdinalIgnoreCase));
                ItemClass unit = StationRules.UnitOf(melt.Metal);
                if (metalIndex < 0 || unit == null || !feed.TryGetValue(melt.Metal, out string[] rawNames)) continue;

                int tally = te.input[rawCount + metalIndex]?.count ?? 0;
                int pending = 0, emptySlots = 0;
                for (int i = 0; i < rawCount; i++)
                {
                    ItemStack s = te.input[i];
                    if (s == null || s.IsEmpty()) { emptySlots++; continue; }
                    ItemClass ic = s.itemValue.ItemClass;
                    if (melt.Metal.Equals(ic?.MadeOfMaterial?.ForgeCategory, StringComparison.OrdinalIgnoreCase)) pending += s.count * ic.GetWeight();
                }
                int wanted = MeltFeed.UnitsWanted(tally, pending, melt.Target, unit.MaxCount);
                if (wanted <= 0) continue;

                var raws = new List<MeltRaw>();
                foreach (string name in rawNames)
                {
                    ItemValue iv = ItemClass.GetItem(name);
                    ItemClass ic = iv?.ItemClass;
                    if (iv == null || iv.IsEmpty() || ic == null || ic.GetWeight() <= 0) continue;
                    // Belt and braces: only items that really melt into this metal.
                    if (!melt.Metal.Equals(ic.MadeOfMaterial?.ForgeCategory, StringComparison.OrdinalIgnoreCase)) continue;
                    int stack = Math.Max(1, ic.Stacknumber.Value);
                    int partial = te.input.Take(rawCount).Where(s => s != null && !s.IsEmpty() && s.itemValue.type == iv.type).Sum(s => Math.Max(0, stack - s.count));
                    raws.Add(new MeltRaw(name, ic.GetWeight(), StoredCount(net, name, plainOnly: true), stack, partial));
                }
                foreach (var (name, count) in MeltFeed.Plan(wanted, raws, emptySlots))
                {
                    int got = net.Storage.Extract(name, null, count);
                    if (got <= 0) continue;
                    net.SaveDirty(); // drives first: a failure after this duplicates, never loses
                    int left = PutIntoOreSlots(te, rawCount, ItemClass.GetItem(name), got);
                    if (left > 0)
                    {
                        net.Storage.Insert(name, null, left);
                        net.SaveDirty();
                    }
                    if (left < got) changed = true;
                }
            }
            return changed;
        }

        /// <summary>Adds to partial stacks of the item in the raw ore slots, then empty ones. Returns what didn't fit.</summary>
        private static int PutIntoOreSlots(TileEntityWorkstation te, int rawCount, ItemValue item, int count)
        {
            int stack = Math.Max(1, item.ItemClass.Stacknumber.Value);
            int left = count;
            for (int i = 0; i < rawCount && left > 0; i++)
            {
                ItemStack s = te.input[i];
                if (s == null || s.IsEmpty() || s.itemValue.type != item.type) continue;
                int add = Math.Min(left, stack - s.count);
                if (add <= 0) continue;
                ItemStack c = s.Clone();
                c.count += add;
                te.input[i] = c;
                left -= add;
            }
            for (int i = 0; i < rawCount && left > 0; i++)
            {
                if (te.input[i] != null && !te.input[i].IsEmpty()) continue;
                int add = Math.Min(left, stack);
                te.input[i] = new ItemStack(item.Clone(), add);
                left -= add;
            }
            return left;
        }

        // ---------------------------------------------------------------- auto-fuel / auto-fire

        private const int FuelModule = 3, MaterialInputModule = 4;

        private static bool RunFire(TileEntityWorkstation te, StationSettings settings, StashNetNetwork net)
        {
            bool hasFuelModule = te.isModuleUsed[FuelModule];
            if (!hasFuelModule || settings.FuelMode == FuelModes.Manual) return false;

            bool queued = CurrentCraftCanOutput(te);
            int[] rawOre = null;
            if (te.isModuleUsed[MaterialInputModule])
            {
                int raw = te.input.Length - (te.materialNames?.Length ?? 0);
                rawOre = te.input.Take(Math.Max(0, raw)).Select(s => CanMelt(te, s) ? s.count : 0).ToArray();
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

        /// <summary>A recipe is queued and its next output has room in the output slots (else vanilla stalls it).</summary>
        private static bool CurrentCraftCanOutput(TileEntityWorkstation te)
        {
            RecipeQueueItem current = te.queue.Length > 0 ? te.queue[te.queue.Length - 1] : null;
            if (SlotFree(current)) return te.queue.Any(q => !SlotFree(q)); // vanilla moves the next one up on its tick
            ItemClass ic = ItemClass.GetForId(current.Recipe.itemValueType);
            if (ic == null) return false;
            var slots = te.output.Select(s => s == null || s.IsEmpty()
                ? (0, 0, 0)
                : (s.itemValue.type, s.count, Math.Max(1, s.itemValue.ItemClass?.Stacknumber.Value ?? 1))).ToArray();
            // Quality items never stack, so only an empty slot takes them.
            int type = ic.HasQuality ? -1 : current.Recipe.itemValueType;
            return StationWork.OutputFits(slots, type, current.Recipe.count, Math.Max(1, ic.Stacknumber.Value));
        }

        /// <summary>Raw ore whose melted-metal tally still has room (vanilla stops melting at the tally's MaxCount).</summary>
        private static bool CanMelt(TileEntityWorkstation te, ItemStack ore)
        {
            if (ore == null || ore.IsEmpty() || te.materialNames == null) return false;
            ItemClass ic = ItemClass.GetForId(ore.itemValue.type);
            string category = ic?.MadeOfMaterial?.ForgeCategory;
            if (category == null) return false;
            for (int j = 0; j < te.materialNames.Length; j++)
            {
                if (!category.Equals(te.materialNames[j], StringComparison.OrdinalIgnoreCase)) continue;
                ItemClass unit = ItemClass.GetItemClass("unit_" + te.materialNames[j]);
                int slot = te.input.Length - te.materialNames.Length + j;
                if (unit == null || slot < 0 || slot >= te.input.Length) return false;
                return te.input[slot].count + ic.GetWeight() <= unit.MaxCount;
            }
            return false;
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
