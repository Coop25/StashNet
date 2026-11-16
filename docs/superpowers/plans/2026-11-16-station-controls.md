# Station Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Always-open ⚡ tab at Network stations with a Fire/Refill fuel picker, forge melt targets, an output switch, and quality ingredients (engine, battery) from storage.

**Architecture:** Settings stay in the per-station `StationSettings` text (server file + every station reply). Game-free math (settings lines, melt feeding, lowest-quality pick) lives in `StashNet.Core` with tests; the server tick (`StationAutomation`) feeds the forge; the client (`CraftingNetwork`) counts/pulls quality ingredients and obeys the output switch; the tab controller is rewritten for the new layout.

**Tech Stack:** C# netstandard2.0 core + xUnit, Harmony, 7DTD XUi XML.

**Spec:** `docs/superpowers/specs/2026-11-16-station-controls-design.md`

## Global Constraints
- Game V3.4 b10. Our UI inside vanilla windows must catch its own Init/Update failures (v0.20.0 lesson).
- Server is the only writer of drives; server code never touches XUi. Items leave storage first; anything that doesn't fit goes back (never lost).
- Settings text: `fuel <mode>`, rule lines, new `output off` (only when off), new `melt|<metal>|<target>|<owner>`. Owners stripped for clients. Old files load unchanged.
- Melt metals = the forge's `InputMaterials` (iron, brass, lead, glass, stone, clay); target 1 .. the unit's MaxCount (30000 vanilla); at most 6, separate from the 8 stock rules.
- Raw feed list (block property `StashNetMeltFeed` on `stashnetForge`, priority order): iron: resourceScrapIron; brass: resourceScrapBrass; lead: resourceScrapLead; glass: resourceBrokenGlass, resourceCrushedSand; stone: resourceRockSmall; clay: resourceClayLump. Plain stacks only.
- Quality ingredients: only stored variants with no mods and no cosmetics; lowest quality first.
- Version via `tools/release.ps1 -Version 0.22.0` only. No deploy without the owner's OK.

## Review Focus
- Settings file from v0.21.0 (no output/melt lines) must load with output on and no melt targets.
- A forge whose ore slots hold a different raw item (e.g. player put scrap brass in) must still be fed into free slots without touching that stack.
- Storage holds only part of what a melt target needs: pull what's there, no error, try again next tick.
- Station listing reply arrives before settings are known (Settings null): output must NOT be moved into storage (old `outputKnown` rule).
- A stored engine with a mod attached must never be counted or pulled; a modless one must be.

---

### Task 1: Core settings — output switch and melt targets

**Files:**
- Modify: `src/StashNet.Core/Stations/StationSettings.cs`
- Test: `tests/StashNet.Core.Tests/StationSettingsTests.cs`

**Interfaces:**
- Produces: `StationSettings.OutputOn` (bool, default true); `StationSettings.Melts` (`List<MeltTarget>`); `const int MaxMelts = 6`; `bool SetMelt(MeltTarget m)` (replace same metal; false when 6 exist); `bool RemoveMelt(string metal)`; `sealed class MeltTarget { string Metal; int Target; string Owner = ""; string ToLine(bool includeOwner); static bool TryParseLine(string line, out MeltTarget m, out string error); MeltTarget Clone(); const int MaxTarget = 1000000; }`. `IsDefault` also requires `OutputOn && Melts.Count == 0`. `Clone`, `Serialize`, `Parse` carry both.

- [ ] **Step 1: Failing tests** — `OutputOffRoundTrips` (Serialize contains `"output off\n"`, Parse back → `OutputOn == false`); `OutputOnWritesNothing` (default serialize has no "output"); `MeltRoundTripsAndStripsOwner` (`melt|iron|2000|Steam_1` → Parse → Target 2000, Owner "Steam_1"; `Serialize(false)` contains `melt|iron|2000|` and not `Steam_1`); `SeventhMeltRefused` (SetMelt false); `SetMeltReplacesSameMetal`; `MeltBadTargetThrows` (`melt|iron|0|` → FormatException); `OldFileLoadsWithDefaults` (text `"fuel Auto\n"` → OutputOn true, Melts empty); `IsDefaultFalseWhenOutputOffOrMelt`.
- [ ] **Step 2:** `dotnet test tests/StashNet.Core.Tests` → new tests fail to compile/fail.
- [ ] **Step 3:** Implement. Metal names use `StockRule.ValidName`; owner validated like `StockRule`. Parse accepts `output off` / `output on`.
- [ ] **Step 4:** `dotnet test tests/StashNet.Core.Tests` → all pass.
- [ ] **Step 5:** Commit "Core: station output switch and melt targets".

### Task 2: Core math — melt feeding and quality pick

**Files:**
- Create: `src/StashNet.Core/Stations/MeltFeed.cs`, `src/StashNet.Core/Stations/QualityPick.cs`
- Test: `tests/StashNet.Core.Tests/MeltFeedTests.cs`, `tests/StashNet.Core.Tests/QualityPickTests.cs`

**Interfaces:**
- Produces:
  - `MeltFeed.UnitsWanted(int tally, int pendingUnits, int target, int unitMax) -> int` = `max(0, min(target, unitMax) − tally − pendingUnits)`.
  - `MeltFeed.Plan(int unitsWanted, IReadOnlyList<MeltRaw> raws, int emptySlots) -> List<(string Name, int Count)>`; `public readonly struct MeltRaw { string Name; int Weight; int Stored; int StackSize; int PartialRoom; }` (PartialRoom = free space in ore-slot stacks already holding this item). Walks raws in order; per raw: count = `min(ceil(left / Weight), Stored, PartialRoom + emptySlots·StackSize)`; empty slots used are deducted; `left -= count·Weight`; stops at `left <= 0`.
  - `QualityPick.LowestFirst(IEnumerable<(string Variant, int Quality, int Count)> stored, int need) -> List<(string Variant, int Count)>` (sorted by Quality ascending, then Variant ordinal; takes until need met; returns what's available if short).
- [ ] **Step 1: Failing tests** — `UnitsWantedStopsAtTarget` (tally 2000, target 2000 → 0); `UnitsWantedCountsPending` (tally 1000, pending 500, target 2000 → 500); `UnitsWantedCapsAtUnitMax` (tally 29990, target 1000000, max 30000 → 10); `PlanUsesFirstRawFirst` (glass 40 units, broken glass w4 stored 5, sand w4 stored 100 → [(brokenGlass,5),(sand,5)]); `PlanRoundsUp` (want 10, weight 4 → 3); `PlanLimitedBySlots` (emptySlots 0, PartialRoom 2 → 2); `PlanPartialStorage` (stored 3 of needed 10 → 3); `LowestFirstPicksLowQuality` (q4×1, q1×1, need 1 → q1); `LowestFirstShort` (one stored, need 2 → one entry count 1).
- [ ] **Step 2:** Run → fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run → pass.
- [ ] **Step 5:** Commit "Core: melt feed and lowest-quality pick".

### Task 3: Protocol + server ops

**Files:**
- Modify: `src/StashNet.Core/Protocol/TerminalMessages.cs` (enum), `src/StashNet/Network/TerminalServer.cs` (`ChangeStation`), `src/StashNet/Stations/StationRules.cs`
- Test: `tests/StashNet.Core.Tests/TerminalMessagesTests.cs`

**Interfaces:**
- Produces: `TerminalOp.SetOutput = 9` (Text "on"/"off"), `SetMeltTarget = 10` (Text = `MeltTarget.ToLine(false)`), `RemoveMeltTarget = 11` (Text = metal). `StationRules.MeltMetals(Block) -> string[]` (block's `InputMaterials`, lower-case; empty if none). `StationRules.MeltFeed(Block) -> Dictionary<string, string[]>` parsed from `StashNetMeltFeed` (`metal:item,item;metal:item`).
- [ ] **Step 1: Failing test** — `NewStationOpsRoundTrip` (request with each new op + Text survives write/read).
- [ ] **Step 2:** Run → fail (enum missing).
- [ ] **Step 3:** Add ops; route them to `ChangeStation`. SetOutput: any Network station. SetMeltTarget: parse, metal in `MeltMetals(block)`, clamp Target to the `unit_<metal>` ItemClass MaxCount (Stacknumber), owner = sender (as for stock rules); full → message `stashnetMeltsFull` with arg 6. RemoveMeltTarget: `RemoveMelt`. Add `StashNetMeltFeed` property to `stashnetForge` in `Mod/Config/blocks.xml` with the Global Constraints list.
- [ ] **Step 4:** `dotnet test` pass; `dotnet build src/StashNet/StashNet.csproj -c Release` succeeds.
- [ ] **Step 5:** Commit "Protocol: output switch and melt target ops".

### Task 4: Output switch behaviour

**Files:**
- Modify: `src/StashNet/Crafting/CraftingOutputServer.cs`, `src/StashNet/Crafting/CraftingNetwork.cs`

**Interfaces:**
- Consumes: `StationSettings.OutputOn`, `StationSettingsStore.Get(pos, block)`.
- Produces: `CraftingNetwork.SendOutput(bool on)`; `CraftingNetwork.OutputToStorage` (bool: `Settings != null && Settings.OutputOn`).
- [ ] **Step 1:** Server mover: after the throttle, `if (!StationSettingsStore.Get(pos, block).OutputOn) return;`.
- [ ] **Step 2:** Client: `MoveOutputToNetwork` and `TryRefundToNetwork` return early unless `OutputToStorage` (Settings null = not yet known = don't move; refund false → vanilla backpack refund).
- [ ] **Step 3:** Build succeeds. Commit "Output switch: stay-here leaves output and refunds vanilla".

### Task 5: Forge melt feeding (server tick)

**Files:**
- Modify: `src/StashNet/Stations/StationAutomation.cs`

**Interfaces:**
- Consumes: `MeltFeed.UnitsWanted/Plan`, `StationRules.MeltFeed(block)`, `StationSettings.Melts`, existing `OwnerAllowed`, `TallySlot`-style lookup.
- [ ] **Step 1:** `needsNetwork` also true when `Melts.Count > 0`. Add `RunMelts(te, block, settings, net)` called before `RunFire` (so the fire sees new ore); skip when `XUiM_Recipes.DisableSmelter` or `!te.isModuleUsed[4]` or `te.materialNames == null`.
- [ ] **Step 2:** Per target with allowed owner: tally slot = `input.Length − materialNames.Length + index of metal`; pending = Σ raw slots `[0, rawCount)` whose item's `MadeOfMaterial.ForgeCategory` equals the metal → `count × GetWeight()`; `unitMax` = `unit_<metal>` MaxCount. Build `MeltRaw` per feed item (Stored = plain stored count, StackSize, PartialRoom from raw slots holding it), `emptySlots` = empty raw slots. Extract each planned amount; put any shortfall back; `SaveDirty` before writing slots; fill partial stacks of the same item, then empty slots (same pattern as `Refuel`); return true if anything moved.
- [ ] **Step 3:** Build succeeds. Commit "Automation: keep forge metals melted from raw scrap".

### Task 6: Quality ingredients from storage

**Files:**
- Modify: `src/StashNet/Crafting/CraftingNetwork.cs`

**Interfaces:**
- Consumes: `QualityPick.LowestFirst`, `ItemCodec.TryDecode`.
- [ ] **Step 1:** In `OnResponse`, keep `qualityByType: Dictionary<int, List<(string Variant, int Quality, int Count)>>` for listing entries whose variant decodes to an item with `HasQuality`, no `Modifications`/`CosmeticMods` with a non-empty item. `Stock(iv)`: plain count, or for `HasQuality` the sum of `qualityByType[type]`. `Stacks()` also yields those (one `ItemStack(new ItemValue(type), sum)` per type). Clear it everywhere `stockByType` is cleared.
- [ ] **Step 2:** `TryFetchShortfall`: remove the `HasQuality → false` refusal; for quality needs build Withdraw items from `QualityPick.LowestFirst(qualityByType[type], missing)` (name + variant), refuse if short.
- [ ] **Step 3:** Build succeeds. Commit "Crafting: stored quality ingredients without mods, lowest quality first".

### Task 7: Tab UI — always open, fire/refill, melt, output

**Files:**
- Modify: `Mod/Config/XUi_InGame/windows.xml` (tab content), `Mod/Config/Localization.csv`, `src/StashNet/UI/XUiC_StashNetAutomation.cs`, `src/StashNet/UI/AutomationTabPatches.cs`
- Create: `src/StashNet/UI/XUiC_StashNetFuelChoice.cs`

**Interfaces:**
- Consumes: `CraftingNetwork.SendFuelMode`, `SendOutput`, new `SendMeltTarget(MeltTarget)`, `SendRemoveMelt(string)` (add both, same pattern as `SendStockRule`).
- [ ] **Step 1: Always open.** At a Network station, on window open and whenever vanilla hides the recipe box (decompile `XUiC_RecipeList` around `InfoWindow.ViewComponent.IsVisible = false` and `XUiC_CraftingInfoWindow` `emptyInfoWindow`), keep `craftingInfoPanel` visible, hide the empty info window, and open our tab if no recipe is selected. Verify by decompiling that the info window's bindings tolerate a null recipe; guard what doesn't with a prefix/postfix limited to Network stations. Vanilla tab buttons with no recipe keep our tab open.
- [ ] **Step 2: Layout (447×303)**, top to bottom: keep row (existing); melt row (forge only: "Keep ◄ metal ► melted at", Counter, Keep/Update); header with list paging ◄ ►; list 6 rows × 20 px showing stock rules then melt targets ("Iron (melted)  tally / target", green when reached, ✕ remove); Fire row (Myself / Auto buttons, selected = highlighted; tooltips = existing fuel tip strings); Refill row (fuel stations, Fire = Auto: "none" + up to 7 `XUiC_StashNetFuelChoice` icon buttons with count, chosen fuel first then by stored count; chosen fuel shows even at 0); Output row ("Finished items:" To storage / Stay here). Remove ◄ ► fuel buttons and `FuelModes.Next/Previous` (and their tests) if unused.
- [ ] **Step 3:** Tally for melt rows: read from the open window's `TileEntityWorkstation.input` (client copy) — fine to be approximate.
- [ ] **Step 4:** Localization keys for all new text (melt label, "(melted)", Fire/Myself/Auto, Refill/none, Finished items/To storage/Stay here, `stashnetMeltsFull`). Build succeeds; no XUi warnings when loaded in `dist` (checked in-game by owner).
- [ ] **Step 5:** Commit "UI: always-open automation tab with fire, refill, melt and output rows".

### Task 8: Release, checklist, notes

**Files:**
- Modify: `docs/testing/automation-checklist.md`, `README.md`, `CLAUDE.md`
- [ ] **Step 1:** Checklist steps: tab without recipe; Fire Myself/Auto; refill icon pick; melt Iron 2000 from scrap iron stops at 2000; glass uses broken glass before sand; Stay here keeps output + backpack refund; vehicle craft pulls a stored engine/battery (lowest quality), modded one untouched; old station settings still load.
- [ ] **Step 2:** `powershell -File tools/release.ps1 -Version 0.22.0` → tests pass, zip `dist/StashNet-v0.22.0.zip`.
- [ ] **Step 3:** Whole-branch review (fresh reviewer), fix findings, commit "v0.22.0: station controls".
