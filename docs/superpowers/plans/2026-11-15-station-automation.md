# Station Automation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Network stations keep chosen items in stock, refuel themselves with a chosen fuel from storage, and light/put out their fire as work comes and goes.

**Architecture:** Station settings (fuel mode + stock rules) live on the server in a per-world file, modelled and serialized by a game-free `StashNet.Core` class. Clients change them with two new terminal ops and learn them from every station response. A throttled server tick (the existing `TileEntityWorkstation.UpdateTick` postfix) runs auto-stock and fire control; the client adds a fuel-mode button and an auto-stock panel.

**Tech Stack:** C# (netstandard2.0 core + game DLL), Harmony, 7DTD XUi XML, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-15-station-automation-design.md`

## Global Constraints

- Branch `m10-automation`; version bump only with `tools/release.ps1 -Version 0.20.0`; deploy only after the owner says yes, game closed.
- Server-side code never touches XUi or `EntityPlayerLocal`.
- Never lose items: storage loses items only after the destination accepted them; drives are saved (`net.SaveDirty()`) before the station is marked modified.
- Stock rules: max 8 per station. Tick throttle: 5 s per station. Keep 1 queue slot free for manual crafts. Fuel top-up target: one full stack in total. Automatic crafts give no XP (`StartingEntityId = -1`).
- Fuel modes: `Manual` (default) / `Auto` / a fuel item name. Button order: Manual → Auto → fuel items in storage sorted by name → Manual.
- Settings file `<save>/StashNet/stations.txt`, keyed by position + block name; dropped when the station is removed.
- All block/item names start with `stashnet`; game-free logic goes in `StashNet.Core` with tests.

## Review Focus

- A forge holding only melted metal (tallies, no raw ore) must go out; one with raw ore waiting must stay lit (Task 4 test via `StationWork.HasWork`).
- A rule whose target is already met by items queued or sitting in the output must not queue more (Task 1 `StockMath` test).
- Network full / ingredient missing / tool missing: nothing is withdrawn (Task 4: withdraw only after all checks; manual check in checklist).
- Garbage or hostile settings text from a client (huge numbers, separators in names, 9th rule) is refused, not stored (Task 1 parse tests, Task 3 server validation).
- A station broken and a new one placed on the same spot starts with no settings (Task 3 removal hook; checklist).

---

### Task 1: Core settings model, text format, and stock math

**Files:**
- Create: `src/StashNet.Core/Stations/StationSettings.cs`
- Create: `src/StashNet.Core/Stations/StockMath.cs`
- Test: `tests/StashNet.Core.Tests/StationSettingsTests.cs`, `tests/StashNet.Core.Tests/StockMathTests.cs`

**Interfaces:**
- Produces:
  - `sealed class StockRule { string Item; int Target; string Recipe; int OutputCount; float CraftTime; int Tier; string Owner; List<(string Name, int Amount)> Ingredients; string ToLine(); static bool TryParseLine(string line, out StockRule rule, out string error); }`
  - `sealed class StationSettings { const int MaxRules = 8; string FuelMode = "Manual"; List<StockRule> Rules; bool IsDefault; string Serialize(); static StationSettings Parse(string text); bool SetRule(StockRule r) /* replaces same Item, false when full */; bool RemoveRule(string item); }` — `Parse` throws `FormatException` on bad data.
  - `sealed class StationSettingsFile { StationSettings Get(int x,int y,int z,string block); void Set(int x,int y,int z,string block, StationSettings s) /* default = remove */; bool Remove(int x,int y,int z); string Serialize(); static StationSettingsFile Parse(string text); }` — Parse skips (does not throw on) a bad station block, keeps the rest.
  - `static class FuelModes { const string Manual="Manual", Auto="Auto"; static string Next(string current, IEnumerable<string> fuelsInStorage); static bool IsFuelItem(string mode); }`
  - `static class StockMath { static int CraftsNeeded(int target, int have, int outputCount); static int CraftsAffordable(IEnumerable<(int perCraft, int stored)> ingredients); }`
- Line format: `rule|<item>|<target>|<recipe>|<outputCount>|<craftTime invariant>|<tier>|<owner>|<name>*<amt>;<name>*<amt>`; station block: `station <x>,<y>,<z> <block>` then `fuel <mode>` then `rule|...` lines, blank line between stations. Names must be non-empty and contain none of `| ; * , space newline`; target 1..1,000,000; outputCount 1..10,000; craftTime 0..86,400; tier 0..6; 1..16 ingredients with amount 1..100,000.

- [ ] **Step 1: Write failing tests**: `RoundTrip_KeepsFuelModeAndRules`, `Parse_RefusesNameWithSeparator`, `Parse_RefusesTargetOverMillion`, `SetRule_ReplacesSameItem`, `SetRule_RefusesNinthRule` (returns false, 8 rules kept), `File_BadStationBlockSkippedOthersKept`, `File_DifferentBlockNameAtSamePos_GetsDefault`, `File_SetDefault_RemovesEntry`, `FuelModes_Next_Cycles` (`Next("Manual",["wood","coal"])=="Auto"`, `Next("Auto",...)=="coal"`, `Next("coal",...)=="wood"`, `Next("wood",...)=="Manual"`, `Next("gas",["coal"])=="Manual"`), `CraftsNeeded` (`(500,312,10)==19`, `(500,500,10)==0`, `(500,600,1)==0`), `CraftsAffordable` (`[(2,9),(1,100)]==4`, `[(2,1)]==0`).
- [ ] **Step 2:** `dotnet test tests/StashNet.Core.Tests` → new tests fail to compile.
- [ ] **Step 3:** Implement the four classes (floats with `CultureInfo.InvariantCulture`).
- [ ] **Step 4:** `dotnet test tests/StashNet.Core.Tests` → all pass.
- [ ] **Step 5:** Commit "Core: station settings, fuel modes, stock math".

### Task 2: Protocol ops and response field

**Files:**
- Modify: `src/StashNet.Core/Protocol/TerminalMessages.cs`
- Test: `tests/StashNet.Core.Tests/TerminalMessagesTests.cs`

**Interfaces:**
- Produces: `TerminalOp.SetFuelMode = 6`, `SetStockRule = 7`, `RemoveStockRule = 8`; `TerminalRequest.Text` (string, "" default; fuel mode / rule line / item name); `TerminalResponse.Station` (string or null: `StationSettings.Serialize()` when the request came from a Network station).

- [ ] **Step 1: Failing tests** `Request_TextRoundTrips`, `Response_StationRoundTrips_NullAndText`, `Request_OpAboveRemoveStockRule_Refused` (op 9 → FormatException).
- [ ] **Step 2:** run → fail.
- [ ] **Step 3:** Write `Text` after Items; write `Station` as bool + string at the end of the response; raise the op upper bound to `RemoveStockRule`. Strings use the existing `ReadString` cap.
- [ ] **Step 4:** run → pass.
- [ ] **Step 5:** Commit.

### Task 3: Server settings store and request handling

**Files:**
- Create: `src/StashNet/Stations/StationSettingsStore.cs` (pattern: the removed `StationOutputStore`, `git show 3cb4bbf^:src/StashNet/Network/StationOutputStore.cs`; file `StashNet/stations.txt`)
- Create: `src/StashNet/Stations/StationRemovedPatch.cs` (Harmony postfix on `BlockWorkstation.OnBlockRemoved`: server, block has `StashNetCraftingTerminal` → `StationSettingsStore.Remove(pos)`)
- Modify: `src/StashNet/Network/TerminalServer.cs`

**Interfaces:**
- Consumes: Task 1 classes, Task 2 fields.
- Produces: `static class StationSettingsStore { StationSettings Get(Vector3i pos, Block block); void Set(Vector3i pos, Block block, StationSettings s); void Remove(Vector3i pos); }`; `static bool StationRules.HasFuel(Block block)` (block's `Workstation.Modules` contains `fuel`), in `src/StashNet/Stations/StationRules.cs`.

- [ ] **Step 1:** In `TerminalServer.Handle` (after the lock check), for a Network station (`CraftingNetwork.IsCraftingTerminal`): `SetFuelMode` → accept `Manual`/`Auto`, or an item whose `ItemClass.GetFuelValue > 0`, only if `HasFuel(block)`; `SetStockRule` → `StockRule.TryParseLine`, owner overwritten with `NetworkLock.PlayerId(player)` as string, recipe must exist (`CraftingManager.GetRecipe(rule.Recipe)`) with its `craftingArea` in the block's `CraftingAreaRecipes` (or equal to the block name when that property is absent), output item must exist; refused → message `stashnetRuleRefused` (8 rules → `stashnetRulesFull`); `RemoveStockRule` → remove by item. Attach `resp.Station = Get(...).Serialize()` to every reply from a Network station.
- [ ] **Step 2:** `dotnet build src/StashNet/StashNet.csproj -c Release` → 0 errors.
- [ ] **Step 3:** Commit.

### Task 4: Server automation tick

**Files:**
- Create: `src/StashNet/Stations/StationAutomation.cs`
- Create: `src/StashNet.Core/Stations/StationWork.cs` + test `tests/StashNet.Core.Tests/StationWorkTests.cs`
- Modify: `src/StashNet/Crafting/CraftingPatches.cs` (`WorkstationOutputToNetworkPatch.Postfix` also calls `StationAutomation.Tick(world, __instance)`)

**Interfaces:**
- Consumes: Tasks 1–3; `StashNetNetwork.TryOpen`, `net.Storage.Extract/Insert/Aggregate`, `NetworkLock`.
- Produces: `static class StationAutomation { static void Tick(World world, TileEntityWorkstation te); }`; game-free `static class StationWork { static bool HasWork(bool recipeQueued, bool smelterDisabled, int[] rawOreCounts); static FireAction Decide(string fuelMode, bool hasFuelModule, bool hasWork, bool burning, float burnLeft, bool besideWater); }` with `enum FireAction { None, Light, PutOut }`.

- [ ] **Step 1: Failing tests** `HasWork_OnlyMetalTallies_False` (`HasWork(false,false,[0,0,0])==false`), `HasWork_RawOre_True`, `HasWork_RawOreButSmelterDisabled_False`, `Decide_Manual_None`, `Decide_IdleBurning_PutOut`, `Decide_WorkWithFuel_Light`, `Decide_WorkNoFuel_None`, `Decide_BesideWater_None`, `Decide_NoFuelModule_None`.
- [ ] **Step 2:** run → fail; implement `StationWork`; run → pass.
- [ ] **Step 3:** Implement `Tick`: server only; skip if `te.IsUserAccessing()` or `LockManager.Instance.IsLockedServer(te)`; 5 s throttle per position; settings default → return. **Auto-stock** per rule while a free queue slot exists beyond the one kept free: owner must pass `NetworkLock.IsAllowed` (compare against the stored owner string the same way `NetworkLock.PlayerId` produces it); tool check: `recipe.craftingToolType == 0` or some `te.tools[i].itemValue.type == craftingToolType`; have = sum of `Aggregate()` counts with that name + queued (`Multiplier × Recipe.count` for queue items whose output type matches) + output slots of that type; crafts = `min(CraftsNeeded, CraftsAffordable, short.MaxValue)`; if > 0: `Extract` each ingredient × crafts — if any comes short, `Insert` everything back and stop; `net.SaveDirty()`; then place the `RecipeQueueItem` (Recipe built from the rule: `itemValueType`, `count`, `craftingTime`, `craftingArea`, `craftingToolType`, `craftingTier = Tier`, `tags`, ingredients; `Multiplier = crafts`, `CraftingTimeLeft = OneItemCraftTime = CraftTime`, `Quality = (byte)Tier`, `StartingEntityId = -1`, `IsCrafting = index == queue.Length-1`) in the highest free index, like `XUiC_CraftingQueue.AddRecipeToCraft`; `te.SetModified()`. **Fire** (fuel module only): HasWork from the queue and `te.input[0 .. input.Length - materialNames.Length)` counts; when mode is a fuel item and there's work, top up to one stack in total (`Extract` first, then fill empty/same-item fuel slots, `Insert` back what didn't fit); then apply `Decide` via `te.IsBurning = ...`. Wrap the whole tick in try/catch → `Log.Warning("[StashNet] automation: ...")`.
- [ ] **Step 4:** build → 0 errors; `dotnet test` → pass.
- [ ] **Step 5:** Commit.

### Task 5: Client state and rule capture

**Files:**
- Modify: `src/StashNet/Crafting/CraftingNetwork.cs`
- Create: `src/StashNet/Stations/StockRuleCapture.cs`

**Interfaces:**
- Produces: `CraftingNetwork.Settings` (`StationSettings`, null until the first reply), `CraftingNetwork.Listing` (last `List<ItemAmount>`), `CraftingNetwork.HasFuel`, `CraftingNetwork.SendFuelMode(string)`, `SendStockRule(StockRule)`, `SendRemoveRule(string item)`; `static StockRule StockRuleCapture.FromRecipe(XUi xui, Recipe recipe, int target)` — count `XUiM_Recipes.GetRecipeCraftOutputCount`, time `XUiM_Recipes.GetRecipeCraftTime`, tier `recipe.GetCraftingTier(player)`, ingredients as `ItemActionEntryCraft.hasItems` computes them (reuse `PerCraftAmount`, made internal); null for recipes with quality ingredients.

- [ ] **Step 1:** `OnResponse` stores `response.Station` (parsed) for our station's replies and the listing; settings cleared on Open/Close.
- [ ] **Step 2:** build → 0 errors. Commit.

### Task 6: UI — fuel button and auto-stock panel

**Files:**
- Modify: `Mod/Config/XUi_InGame/windows.xml`, `Mod/Config/Localization.csv`
- Create: `src/StashNet/UI/XUiC_StashNetFuelMode.cs`, `src/StashNet/UI/XUiC_StashNetAutoStock.cs`

**Interfaces:**
- Consumes: Task 5.

- [ ] **Step 1:** Fuel button appended to `windowFuel`'s header rect (pattern: the removed output button, `git show 3cb4bbf^:Mod/Config/XUi_InGame/windows.xml`, incl. `RefreshBindingsSelfAndChildren`): visible only when `CraftingNetwork.Active && HasFuel`; label shows the mode (`Manual`, `Auto`, or the fuel's localized name); click → `FuelModes.Next(current, fuel items in Listing with fuel value > 0)` → `SendFuelMode`.
- [ ] **Step 2:** Auto-stock panel appended under `windowCraftingQueue` content: label "Keep in stock: <selected recipe>", a vanilla counter (amount, default 100, step 10), Add/Update button (`StockRuleCapture.FromRecipe` with the window group's `XUiC_RecipeList` selected entry's recipe; null → tooltip `stashnetRuleCantAuto`), and a list of up to 8 rows "name  stored/target  [x]". Visible only at Network stations.
- [ ] **Step 3:** Localization keys: `stashnetFuelManual`, `stashnetFuelAuto`, `stashnetFuelTip`, `stashnetKeepInStock`, `stashnetAddRule`, `stashnetRuleRefused`, `stashnetRulesFull`, `stashnetRuleCantAuto`.
- [ ] **Step 4:** build → 0 errors, `dist/` staged. Commit.

### Task 7: Release build, checklist, notes

**Files:**
- Create: `docs/testing/automation-checklist.md` (the spec's Testing list as checkboxes)
- Modify: `CLAUDE.md` (resume section), `README.md` (feature list)

- [ ] **Step 1:** `powershell -ExecutionPolicy Bypass -File tools/release.ps1 -Version 0.20.0` → tests pass, `dist/StashNet-v0.20.0.zip`.
- [ ] **Step 2:** Commit; ask the owner "can I deploy? (game closed)".
