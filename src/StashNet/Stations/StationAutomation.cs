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
        private static readonly System.Random stagger = new System.Random();

        public static void Tick(World world, TileEntityWorkstation te)
        {
            if (world == null || te == null) return;
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
            if (te.IsUserAccessing() || LockManager.Instance.IsLockedServer(te)) return;

            Vector3i pos = te.ToWorldPos();
            float now = UnityEngine.Time.time;
            if (!nextRun.TryGetValue(pos, out float t))
            {
                // First check after load: a random 0-5 s offset, so many stations never check in the same frame.
                nextRun[pos] = now + (float)stagger.NextDouble() * IntervalSeconds;
                return;
            }
            if (now < t) return;
            nextRun[pos] = now + IntervalSeconds;

            Block block = world.GetBlock(pos).Block;
            if (!CraftingNetwork.IsCraftingTerminal(block)) return;
            try
            {
                StationSettings settings = StationSettingsStore.Get(pos, block);
                if (settings.IsDefault) return;

                StationStock net = null;
                bool needsNetwork = settings.Rules.Count > 0 || settings.Melts.Count > 0 || FuelModes.IsFuelItem(settings.FuelMode);
                // The shared live summary answers "anything to do?"; the real drives are read only to take items.
                if (needsNetwork) net = StationStock.For(world, pos);

                bool changed = false;
                if (net != null) changed |= RunStockRules(world, te, block, settings, net);
                if (net != null) changed |= RunMelts(te, block, settings, net); // before the fire, so it sees the new ore
                changed |= RunFire(te, settings, net);
                if (changed) te.SetModified();
            }
            catch (Exception e)
            {
                // An earlier step may already have moved items into this station (and saved the drives), so
                // make sure the station itself is saved too.
                te.SetModified();
                Log.Warning($"[StashNet] automation at {pos}: {e}");
            }
        }

        // ---------------------------------------------------------------- auto-stock

        private static bool RunStockRules(World world, TileEntityWorkstation te, Block block, StationSettings settings, StationStock net)
        {
            bool changed = false;
            foreach (StockRule rule in settings.Rules)
            {
                if (FreeQueueSlots(te) <= FreeSlotsKept) break;
                if (!OwnerAllowed(net, rule.Owner)) continue;
                // Same-name recipes (e.g. with and without a cooking pot): the first one this station has the tool for.
                Recipe serverRecipe = StationRules.FindRecipes(block, rule).FirstOrDefault(r => HasTool(te, r.craftingToolType));
                if (serverRecipe == null) continue;
                if (TryQueue(te, rule, serverRecipe, net)) changed = true;
            }
            return changed;
        }

        private static bool OwnerAllowed(StationStock net, string owner)
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

        private static bool TryQueue(TileEntityWorkstation te, StockRule rule, Recipe serverRecipe, StationStock net)
        {
            int outputType = serverRecipe.itemValueType;
            int have = net.Stored(rule.Item) + QueuedCount(te, outputType) + OutputCount(te, outputType);
            int needed = StockMath.CraftsNeeded(rule.Target, have, rule.OutputCount);
            if (needed <= 0) return false;

            // Each ingredient comes from the forge's melted-metal tallies (unit_iron etc.) when the
            // station holds that material, else from storage.
            var sources = new List<(string Name, int PerCraft, int TallySlot, int Stored)>();
            foreach (var (name, amount) in rule.Ingredients)
            {
                int slot = TallySlot(te, name);
                int stored = slot >= 0 ? te.input[slot].count : net.Stored(name, plainOnly: true);
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
                int got = net.Extract(s.Name, want);
                taken.Add((s.Name, got));
                if (got < want)
                {
                    foreach (var (name, count) in taken) net.PutBack(name, count);
                    net.SaveDirty();
                    return false;
                }
            }

            int index = HighestFreeSlot(te);
            if (index < 0)
            {
                foreach (var (name, count) in taken) net.PutBack(name, count);
                net.SaveDirty();
                return false;
            }
            // Drives are saved only after the craft is queued (below), so a failure in between leaves them untouched.
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
            net.SaveDirty();
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
        private static bool RunMelts(TileEntityWorkstation te, Block block, StationSettings settings, StationStock net)
        {
            if (!te.isModuleUsed[MaterialInputModule] || te.materialNames == null || te.input == null) return false;
            // The game keeps 3 ore slots but the forge window shows only 2 (vanilla windows.xml rows="2"), so
            // a 3rd would melt out of sight. Automation uses only the shown ones (StashNetOreSlots, owner
            // 2026-11-16: "something is happening behind the scenes"), and empties any hidden one into storage.
            int tallyStart = te.input.Length - te.materialNames.Length;
            int rawCount = Math.Min(tallyStart, StationRules.OreSlots(block));
            bool returned = ReturnHiddenOre(te, rawCount, tallyStart, net);
            if (settings.Melts.Count == 0 || XUiM_Recipes.DisableSmelter) return returned;
            if (rawCount <= 0) return returned;
            Dictionary<string, string[]> feed = StationRules.MeltFeed(block);

            // Targets that need feeding right now, in order. Each one may spread over every empty ore slot
            // (both slots melt at once), except that later targets with no slot yet keep one each.
            var wanting = new List<(MeltTarget Melt, ItemClass Unit, string[] Raws, int Wanted)>();
            foreach (MeltTarget melt in settings.Melts)
            {
                if (!OwnerAllowed(net, melt.Owner)) continue;
                int metalIndex = Array.FindIndex(te.materialNames, m => m.Equals(melt.Metal, StringComparison.OrdinalIgnoreCase));
                ItemClass unit = StationRules.UnitOf(melt.Metal);
                if (metalIndex < 0 || unit == null || !feed.TryGetValue(melt.Metal, out string[] rawNames)) continue;
                int tally = te.input[tallyStart + metalIndex]?.count ?? 0;
                int pending = Enumerable.Range(0, rawCount).Where(i => IsMetal(te.input[i], melt.Metal))
                    .Sum(i => te.input[i].count * te.input[i].itemValue.ItemClass.GetWeight());
                int wanted = MeltFeed.UnitsWanted(tally, pending, melt.Target, unit.MaxCount);
                if (wanted > 0) wanting.Add((melt, unit, rawNames, wanted));
            }

            // One melt target may fill every ore slot; with several, each metal gets one slot (owner,
            // 2026-11-16), so a forge never melts clay + clay while iron waits.
            int targets = settings.Melts.Count(m => OwnerAllowed(net, m.Owner));
            int perMetal = MeltFeed.SlotsPerMetal(targets, rawCount);
            bool changed = returned;
            changed |= perMetal < rawCount && FreeSlotForWaitingMetal(te, rawCount, wanting.Where(w => HasStoredRaw(net, w.Raws)).Select(w => w.Melt.Metal), net);

            for (int k = 0; k < wanting.Count; k++)
            {
                var (melt, _, rawNames, wanted) = wanting[k];
                int emptySlots = Enumerable.Range(0, rawCount).Count(i => te.input[i] == null || te.input[i].IsEmpty());
                int mine = Enumerable.Range(0, rawCount).Count(i => IsMetal(te.input[i], melt.Metal));
                int usableEmpty = Math.Min(emptySlots, Math.Max(0, perMetal - mine));

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
                    raws.Add(new MeltRaw(name, ic.GetWeight(), net.Stored(name, plainOnly: true), stack, partial));
                }
                foreach (var (name, count) in MeltFeed.Plan(wanted, raws, usableEmpty))
                {
                    ItemValue rawItem = ItemClass.GetItem(name);
                    if (rawItem == null || rawItem.IsEmpty() || rawItem.ItemClass == null) continue;
                    int got = net.Extract(name, count);
                    if (got <= 0) continue;
                    // Into the ore slots, leftovers back, THEN save: the drives are never saved while items are
                    // in neither place (a failure before the save leaves the drives as they were).
                    int usedBefore = Enumerable.Range(0, rawCount).Count(i => te.input[i] == null || te.input[i].IsEmpty());
                    int left = PutIntoOreSlots(te, rawCount, rawItem, got, usableEmpty);
                    usableEmpty -= usedBefore - Enumerable.Range(0, rawCount).Count(i => te.input[i] == null || te.input[i].IsEmpty());
                    net.PutBack(name, left);
                    net.SaveDirty();
                    if (left < got) changed = true;
                }
            }
            return changed;
        }

        /// <summary>Plain items in ore slots the window doesn't show go back to storage (or a loot bag on the station).</summary>
        private static bool ReturnHiddenOre(TileEntityWorkstation te, int shownSlots, int tallyStart, StationStock net)
        {
            bool any = false;
            for (int i = Math.Max(0, shownSlots); i < tallyStart; i++)
            {
                ItemStack s = te.input[i];
                if (s == null || s.IsEmpty() || !Drives.ItemCodec.IsPlain(s.itemValue)) continue;
                net.PutBack(s.itemValue.ItemClass.GetItemName(), s.count);
                te.input[i] = ItemStack.Empty.Clone();
                any = true;
            }
            if (any) net.SaveDirty();
            return any;
        }

        private static bool HasStoredRaw(StationStock net, string[] rawNames) =>
            rawNames.Any(n => net.Stored(n, plainOnly: true) > 0);

        /// <summary>
        /// With several melt targets: when every ore slot is full and a metal that can be fed holds none,
        /// the smaller stack of a metal filling two slots goes back to storage (MeltFeed.SlotToFree).
        /// What storage can't take stays in the slot.
        /// </summary>
        private static bool FreeSlotForWaitingMetal(TileEntityWorkstation te, int rawCount, IEnumerable<string> waitingMetals, StationStock net)
        {
            var metals = new string[rawCount];
            var counts = new int[rawCount];
            for (int i = 0; i < rawCount; i++)
            {
                ItemStack s = te.input[i];
                if (s == null || s.IsEmpty()) continue;
                metals[i] = s.itemValue.ItemClass?.MadeOfMaterial?.ForgeCategory ?? s.itemValue.ItemClass?.GetItemName();
                counts[i] = s.count;
            }
            int slot = MeltFeed.SlotToFree(metals, counts, waitingMetals);
            if (slot < 0) return false;

            ItemStack stack = te.input[slot];
            // Only plain items go back (storage takes them by name); a quality or modded item a player put
            // in by hand stays where it is.
            if (!Drives.ItemCodec.IsPlain(stack.itemValue)) return false;
            int back = net.Insert(stack.itemValue.ItemClass.GetItemName(), stack.count);
            if (back <= 0) return false;
            net.SaveDirty();
            if (back >= stack.count) te.input[slot] = ItemStack.Empty.Clone();
            else
            {
                ItemStack c = stack.Clone();
                c.count -= back;
                te.input[slot] = c;
            }
            return true;
        }

        private static bool IsMetal(ItemStack s, string metal) =>
            s != null && !s.IsEmpty() && metal.Equals(s.itemValue.ItemClass?.MadeOfMaterial?.ForgeCategory, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Spreads the items evenly over the ore slots already holding this item plus up to
        /// <paramref name="usableEmpty"/> empty ones (MeltFeed.Spread), so several slots melt at once.
        /// Returns what didn't fit.
        /// </summary>
        private static int PutIntoOreSlots(TileEntityWorkstation te, int rawCount, ItemValue item, int count, int usableEmpty)
        {
            int stack = Math.Max(1, item.ItemClass.Stacknumber.Value);
            var slots = new List<int>();
            for (int i = 0; i < rawCount; i++)
            {
                ItemStack s = te.input[i];
                if (s != null && !s.IsEmpty() && s.itemValue.type == item.type) slots.Add(i);
            }
            for (int i = 0; i < rawCount && usableEmpty > 0; i++)
            {
                if (te.input[i] != null && !te.input[i].IsEmpty()) continue;
                slots.Add(i);
                usableEmpty--;
            }
            int[] add = MeltFeed.Spread(count, slots.Select(i => te.input[i] == null || te.input[i].IsEmpty() ? 0 : te.input[i].count).ToArray(), stack);
            int placed = 0;
            for (int j = 0; j < slots.Count; j++)
            {
                if (add[j] <= 0) continue;
                ItemStack s = te.input[slots[j]];
                if (s == null || s.IsEmpty()) te.input[slots[j]] = new ItemStack(item.Clone(), add[j]);
                else
                {
                    ItemStack c = s.Clone();
                    c.count += add[j];
                    te.input[slots[j]] = c;
                }
                placed += add[j];
            }
            return count - placed;
        }

        // ---------------------------------------------------------------- auto-fuel / auto-fire

        private const int FuelModule = 3, MaterialInputModule = 4;

        private static bool RunFire(TileEntityWorkstation te, StationSettings settings, StationStock net)
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

        /// <summary>Top the chosen fuel up to the station's refill amount (StashNetFuelRefill, at most a stack). Never moves other fuel.</summary>
        private static bool Refuel(TileEntityWorkstation te, string fuelName, StationStock net)
        {
            ItemValue fuel = ItemClass.GetItem(fuelName);
            if (fuel == null || fuel.IsEmpty() || ItemClass.GetFuelValue(fuel) <= 0) return false;
            int stackSize = Math.Max(1, fuel.ItemClass.Stacknumber.Value);

            ItemStack[] slots = te.fuel;
            int loaded = slots.Where(s => s != null && !s.IsEmpty() && s.itemValue.type == fuel.type).Sum(s => s.count);
            int room = slots.Sum(s => s == null || s.IsEmpty() ? stackSize : s.itemValue.type == fuel.type ? Math.Max(0, stackSize - s.count) : 0);
            int refill = StationRules.FuelRefillAmount(GameManager.Instance.World?.GetBlock(te.ToWorldPos()).Block);
            int want = FuelRefill.Want(loaded, room, refill, stackSize);
            if (want <= 0) return false;

            if (net.Stored(fuelName, plainOnly: true) <= 0) return false; // none stored: no need to read the drives
            int got = net.Extract(fuelName, want);
            if (got <= 0) return false;

            // Into the fuel slots first, then leftovers back, then save (never save with items in between).
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
            net.PutBack(fuelName, left);
            net.SaveDirty();
            return true;
        }
    }
}
