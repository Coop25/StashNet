# Milestone 8: Progression Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Standard/High-Density drives, Reinforced/Armored Drive Bays, Storage/Advanced Cores (hammer-upgraded with the next-tier block), size-weighted heat, a scrap guard for non-empty drives, one-Core-per-bay membership and tile-entity-based discovery (v0.9.0).

**Architecture:** Two new game-free pieces in `StashNet.Core` (a nearest-Core picker, a "drive holds items" check) and a weighted heat formula, all unit-tested. Game glue changes: `StashNetNetwork` discovers Cores/bays by walking chunk tile entities and uses the picker for membership; a Harmony prefix guards scrapping; heat sums per-drive weights. Tiers themselves are XML: vanilla `UpgradeBlock` carries storage, lock and owner across the upgrade (`GameManager.ChangeBlocks` → `UpgradeDowngradeFrom`).

**Tech Stack:** C# (netstandard2.0 Core lib + game-framework mod DLL), Harmony, xUnit, 7DTD XML/XPath config.

**Spec:** `docs/superpowers/specs/2026-11-13-m8-progression-design.md`

## Global Constraints

- Game V3.4 b10. The mod builds against the game's own libraries (existing csproj). Never commit game DLLs.
- Every new block/item name starts with `stashnet`. Code identifies drives by the `stashnetDrive` tag, Cores by the `StashNetRadius` property, and bays by `TEFeatureStashNetDriveBay`, never by name.
- All balance numbers live in XML. Use the spec's tables verbatim. They're placeholders, so don't "improve" them.
- Game-free logic goes in `src/StashNet.Core/` with xUnit tests in `tests/StashNet.Core.Tests/`. Game glue stays thin.
- Never lose items. Unreadable drive data counts as "holds items". Nothing in M8 may delete drive contents.
- Build to `dist/` only (`dotnet build src/StashNet/StashNet.csproj -c Release`). **Never** pass `-p:Deploy=true` without the owner's yes for that deploy, and only while the game is closed.
- Server-side code never touches XUi or `EntityPlayerLocal`.
- Harmony: the patched method must be declared on the patched type.
- Decompiled game code goes to the scratchpad only, never into the repo.
- Version **0.9.0**.
- Commits end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Work on branch `m8-progression`.

## Review Focus

1. **Two Cores exactly the same distance from a bay.** Both networks must agree on one owner whatever order the Cores are found in. (Task 1 test `Pick_Tie_SameOwnerWhateverTheOrder`.)
2. **A nearer Core whose reach doesn't cover the bay.** The bay belongs to the farther Core that does reach it, not to no one. (Task 1 test `Pick_NearerButOutOfReach_Ignored`.)
3. **A drive whose data can't be read.** It must not be scrappable. (Task 4 test `HoldsItems_Unreadable_True`.)
4. **A network at a chunk edge with a neighbor chunk not loaded.** The walk skips missing chunks, with no crash and no exception from a null chunk. (Task 2 code requirement plus an M8 checklist line.)
5. **The vanilla upgrade carry-over on our multi-block bay and lockable Core.** Drives, code, allowed players and owner survive. This can't be unit-tested, so it's M8 checklist step 1, written in Task 6.

---

### Task 1: Nearest-Core picker (game-free)

**Files:**
- Create: `src/StashNet.Core/Network/CorePicker.cs`
- Test: `tests/StashNet.Core.Tests/CorePickerTests.cs`

**Interfaces:**
- Produces (namespace `StashNet.Core.Network`):
  - `public readonly struct CoreSite { public CoreSite(int x, int y, int z, int reach); public int X { get; } public int Y { get; } public int Z { get; } public int Reach { get; } }`
  - `public static class CorePicker`
    - `public static int CubeDistance(int ax, int ay, int az, int bx, int by, int bz)`: the largest of the three axis differences.
    - `public static int Pick(IReadOnlyList<CoreSite> cores, int x, int y, int z)`: the index of the owning Core, or -1. The owner is the Core with the smallest cube distance among those with `distance <= Reach`. Ties go to the lowest Y, then X, then Z (independent of list order).

- [ ] **Step 0: Branch and commit the spec**

```bash
git checkout -b m8-progression
git add docs/superpowers/specs/2026-11-13-m8-progression-design.md docs/superpowers/plans/2026-11-13-m8-progression.md
git commit -m "M8 Progression: design and plan" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 1: Write the failing tests** in `CorePickerTests.cs` (`namespace StashNet.Core.Tests`, xUnit `[Fact]`s):

```csharp
CubeDistance_UsesLargestAxis:        Assert.Equal(5, CorePicker.CubeDistance(0,0,0, 3,-5,2));
Pick_NoCores_MinusOne:               Assert.Equal(-1, CorePicker.Pick(new CoreSite[0], 1,2,3));
Pick_OutOfReach_MinusOne:            Assert.Equal(-1, CorePicker.Pick(new[]{ new CoreSite(0,0,0,6) }, 7,0,0));
Pick_EdgeOfReach_Included:           Assert.Equal(0,  CorePicker.Pick(new[]{ new CoreSite(0,0,0,6) }, 6,6,-6));
Pick_NearestWins:                    Assert.Equal(1,  CorePicker.Pick(new[]{ new CoreSite(0,0,0,12), new CoreSite(5,0,0,6) }, 4,0,0));
Pick_NearerButOutOfReach_Ignored:    Assert.Equal(0,  CorePicker.Pick(new[]{ new CoreSite(0,0,0,24), new CoreSite(10,0,0,2) }, 6,0,0));
Pick_Tie_SameOwnerWhateverTheOrder:  var a = new CoreSite(0,0,0,6); var b = new CoreSite(4,0,0,6);
                                     Assert.Equal(0, CorePicker.Pick(new[]{ a, b }, 2,0,0));
                                     Assert.Equal(1, CorePicker.Pick(new[]{ b, a }, 2,0,0));   // both pick a (lower X)
Pick_Tie_LowerYBeatsLowerX:          Assert.Equal(1,  CorePicker.Pick(new[]{ new CoreSite(0,2,0,6), new CoreSite(4,0,0,6) }, 2,1,0));
Pick_AtCorePosition_PicksItself:     Assert.Equal(0,  CorePicker.Pick(new[]{ new CoreSite(3,3,3,6), new CoreSite(4,3,3,6) }, 3,3,3));
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --filter CorePickerTests`
Expected: build error, `CorePicker`/`CoreSite` not found.

- [ ] **Step 3: Implement `CoreSite` and `CorePicker` in `src/StashNet.Core/Network/CorePicker.cs`** as specified under Interfaces. Use a single pass that keeps the best index and compares (distance, Y, X, Z).

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet test`
Expected: all pass (59 existing + 9 new).

- [ ] **Step 5: Commit**

```bash
git add src/StashNet.Core/Network/CorePicker.cs tests/StashNet.Core.Tests/CorePickerTests.cs
git commit -m "Core picker: a bay or terminal belongs to its nearest in-reach Core (M8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Tile-entity discovery and one Core per bay

**Files:**
- Create: `src/StashNet/Network/TileEntityScan.cs`
- Modify: `src/StashNet/Network/StashNetNetwork.cs` (`TryOpen`, `TryFindCore`; remove the block-by-block `Cube` scans)
- Modify: `src/StashNet/Network/CoreLookupCache.cs` (the comment saying "scans ~2,000 blocks" now says it walks nearby tile entities)

**Interfaces:**
- Consumes: `CoreSite`, `CorePicker.Pick` (Task 1).
- Produces:
  - `internal static class TileEntityScan { public static IEnumerable<TileEntityComposite> InCube(World world, Vector3i center, int r); }`: every `TileEntityComposite` in a loaded chunk whose `ToWorldPos()` is within cube distance `r` of `center`. Chunk range is `World.toChunkXZ(center.x ± r)` × `World.toChunkXZ(center.z ± r)`. Use `world.GetChunkSync(cx, cz) as Chunk` and **skip null chunks**. Iterate `chunk.GetTileEntities().list`.
  - `StashNetNetwork.TryFindCore(World, Vector3i from, out Vector3i corePos, out int coreRadius)`: **signature unchanged**. Builds a `List<CoreSite>` from Core tile entities in `InCube(world, from, MaxCoreRadius())` (a Core is a tile entity whose block passes `IsCore`, with reach `GetInt(PropRadius)`) and returns `CorePicker.Pick`'s choice.
  - `StashNetNetwork.TryOpen(...)`: **signature unchanged**. After finding the Core, it collects the Core list once from `InCube(world, corePos, radius + MaxCoreRadius())`. Bays come from `InCube(world, corePos, radius)` (tile entities with a `TEFeatureStashNetDriveBay` feature). **A bay is included, and counted in `BayCount`, only if `CorePicker.Pick` for the bay's position returns this Core.** Everything after that (skipping open bays, reading drives, `SaveDirty`) is unchanged.

- [ ] **Step 1: Implement `TileEntityScan.InCube`** as specified.

- [ ] **Step 2: Rewrite `TryFindCore` and `TryOpen`** to use `TileEntityScan` and `CorePicker`. Delete the `Cube(...)` iterator and the private `CubeDistance` (use `CorePicker.CubeDistance` if one is needed).

- [ ] **Step 3: Build**

Run: `dotnet build src/StashNet/StashNet.csproj -c Release`
Expected: `Build succeeded`, 0 errors.

- [ ] **Step 4: Run the tests**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/StashNet/Network/TileEntityScan.cs src/StashNet/Network/StashNetNetwork.cs src/StashNet/Network/CoreLookupCache.cs
git commit -m "Find Cores and bays by tile entity; each bay joins only its nearest Core (M8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Size-weighted heat

**Files:**
- Modify: `src/StashNet.Core/Heat/NetworkHeat.cs`
- Test: `tests/StashNet.Core.Tests/HeatTests.cs`
- Modify: `src/StashNet/Drives/DriveItem.cs`, `src/StashNet/Network/StashNetNetwork.cs`, `src/StashNet/Heat/HeatEmitter.cs`, `src/StashNet/Heat/HeatCommand.cs`, `src/StashNet/Heat/TEFeatureStashNetHeat.cs`

**Interfaces:**
- Consumes: `StashNetNetwork` after Task 2.
- Produces:
  - `NetworkHeat.PerReport(float totalWeight, float heatPerDrive, float cap)`: `min(totalWeight × heatPerDrive, cap)`; 0 if any input ≤ 0. The first parameter changes from `int drives` to `float totalWeight`; existing int call sites still compile.
  - `DriveItem.PropHeatWeight = "StashNetHeatWeight"`; `public static float HeatWeight(ItemValue drive)`: the item class's `StashNetHeatWeight` if present, else `1f`, never below 0.
  - `StashNetNetwork.HeatWeight` (`float`, public get): the sum of `DriveItem.HeatWeight` over every drive (readable **and** unreadable) in the network's member bays that aren't being accessed.
  - `HeatEmitter.CoreHeat(World world, Vector3i corePos, out int drives, out float weight)`: uses `NetworkHeat.PerReport(net.HeatWeight, perDrive, cap)`.
  - `sn heat` prints: `Core at {pos}: {drives} drive(s), heat weight {weight:0.#} -> {heat:0.##} heat per report ({perDrive:0.##} per weight, cap {cap:0.##}), about every 3 minutes.`

- [ ] **Step 1: Write the failing tests** in `HeatTests.cs`, keeping the existing ones:

```csharp
PerReport_WeightedTotal:      Assert.Equal(5.76f, NetworkHeat.PerReport(32f, 0.18f, 6.5f), 3);  // 16 Standard on a Storage Core
PerReport_JustUnderCap:       Assert.Equal(6.48f, NetworkHeat.PerReport(54f, 0.12f, 6.5f), 3);  // Advanced Core
PerReport_OverCap_Capped:     Assert.Equal(6.5f,  NetworkHeat.PerReport(56f, 0.12f, 6.5f), 3);
PerReport_FractionalWeight:   Assert.Equal(0.125f, NetworkHeat.PerReport(0.5f, 0.25f, 6.5f), 4);
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --filter HeatTests`
Expected: build error in `PerReport_FractionalWeight` (cannot convert `float` to `int`).

- [ ] **Step 3: Change `NetworkHeat.PerReport`'s first parameter to `float totalWeight`** and update its doc comment.

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Wire up the game side.** Add `DriveItem.HeatWeight`, sum `StashNetNetwork.HeatWeight` in `TryOpen`, add the `out float weight` to `HeatEmitter.CoreHeat` (update `TEFeatureStashNetHeat` to `out _, out _`), and switch the `sn heat` line to the format above.

- [ ] **Step 6: Build**

Run: `dotnet build src/StashNet/StashNet.csproj -c Release`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add src/StashNet.Core/Heat/NetworkHeat.cs tests/StashNet.Core.Tests/HeatTests.cs src/StashNet/Drives/DriveItem.cs src/StashNet/Network/StashNetNetwork.cs src/StashNet/Heat
git commit -m "Heat counts drive size: StashNetHeatWeight per drive (M8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Scrap guard for drives that hold items

**Files:**
- Modify: `src/StashNet.Core/Drives/DriveSerializer.cs`
- Test: `tests/StashNet.Core.Tests/DriveSerializerTests.cs`
- Modify: `src/StashNet/Drives/DriveItem.cs`
- Create: `src/StashNet/Patches/DriveScrapGuardPatch.cs`
- Modify: `Mod/Config/Localization.csv`

**Interfaces:**
- Produces:
  - `DriveSerializer.HoldsItems(string text)`: `false` for null or empty; `true` if `Deserialize` throws `DriveFormatException`; otherwise `TypeCount > 0`.
  - `DriveItem.HoldsItems(ItemValue drive)`: reads the `StashNetDrive` string metadata (missing means null) and returns `DriveSerializer.HoldsItems(text)`.
  - Harmony prefix `DriveScrapGuardPatch` on `ItemActionEntryScrap.OnActivated`. If `((IItemStackController)__instance.ItemController).ItemStack` is a drive (`DriveRules.IsDrive`) and `DriveItem.HoldsItems` is true, it calls `GameManager.ShowTooltip(player, Localization.Get("stashnetUnloadBeforeScrap"), string.Empty, "ui_denied")` with `player = __instance.ItemController.xui.playerUI.entityPlayer` and returns `false`. Otherwise it returns `true`.
  - Localization row: `stashnetUnloadBeforeScrap,UI,XUi,,,Unload this drive first: shift-click it in a Storage Terminal.`

- [ ] **Step 1: Write the failing tests** in `DriveSerializerTests.cs`:

```csharp
HoldsItems_Null_False:        Assert.False(DriveSerializer.HoldsItems(null));
HoldsItems_Empty_False:       Assert.False(DriveSerializer.HoldsItems(""));
HoldsItems_HeaderOnly_False:  Assert.False(DriveSerializer.HoldsItems(DriveSerializer.Serialize(new DriveContents())));
HoldsItems_WithItem_True:     var d = new DriveContents(); d.TryInsert(Big, One, "resourceWood", null, 5);
                              Assert.True(DriveSerializer.HoldsItems(DriveSerializer.Serialize(d)));
HoldsItems_Unreadable_True:   Assert.True(DriveSerializer.HoldsItems("not a drive"));
```

- [ ] **Step 2: Run them and see them fail**

Run: `dotnet test --filter DriveSerializerTests`
Expected: build error, `HoldsItems` not found.

- [ ] **Step 3: Implement `DriveSerializer.HoldsItems`.**

- [ ] **Step 4: Run the tests and see them pass**

Run: `dotnet test`
Expected: all pass.

- [ ] **Step 5: Add `DriveItem.HoldsItems`, the patch and the localization row.**

- [ ] **Step 6: Build**

Run: `dotnet build src/StashNet/StashNet.csproj -c Release`
Expected: `Build succeeded`.

- [ ] **Step 7: Commit**

```bash
git add src/StashNet.Core/Drives/DriveSerializer.cs tests/StashNet.Core.Tests/DriveSerializerTests.cs src/StashNet/Drives/DriveItem.cs src/StashNet/Patches/DriveScrapGuardPatch.cs Mod/Config/Localization.csv
git commit -m "Refuse to scrap a drive that still holds items (M8)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Tier content (XML)

**Files:**
- Modify: `Mod/Config/items.xml`, `Mod/Config/blocks.xml`, `Mod/Config/loot.xml`, `Mod/Config/recipes.xml`, `Mod/Config/Localization.csv`

**Interfaces:**
- Consumes: the `StashNetHeatWeight` item property (Task 3) and `TEFeatureStashNetDriveBay` / `TEFeatureStashNetHeat` (existing).
- Produces the names used in the checklist: `stashnetDriveStandard`, `stashnetDriveHighDensity`, `stashnetDriveBayReinforced`, `stashnetDriveBayArmored`, `stashnetStorageCore`, `stashnetAdvancedCore`; loot containers `stashnetDriveBaySlotsReinforced` (`size="4,2"`) and `stashnetDriveBaySlotsArmored` (`size="4,3"`).

Write **every new block as a full definition (no `Extends`)**. Inheriting would copy the lower tier's `UpgradeBlock` (a block upgrading to itself) and its nested `CompositeFeatures`/`LootList`.

- [ ] **Step 1: items.xml.**
  - Add `StashNetHeatWeight` = 1 to the Basic Drive.
  - Add the Standard drive (`StashNetMaxStacks` 96, `StashNetHeatWeight` 2, `Weight` 4) and the High-Density drive (384, 4, `Weight` 8). Copy the Basic Drive's other properties, with their own `CustomIconTint` and `DescriptionKey`.
  - Append the four upgrade block names to every repair tool's allowlist:

```xml
<append xpath="//property[@name='Allowed_upgrade_items']/@value">,stashnetDriveBayReinforced,stashnetDriveBayArmored,stashnetStorageCore,stashnetAdvancedCore</append>
```

- [ ] **Step 2: loot.xml.** Add the two containers, the same as `stashnetDriveBaySlots` except for name and size.

- [ ] **Step 3: blocks.xml.**
  - **Drive Bay:** add `<property class="UpgradeBlock">` with `ToBlock` = `stashnetDriveBayReinforced`, `Item` = `stashnetDriveBayReinforced`, `ItemCount` 1, `UpgradeHitCount` 4.
  - **Reinforced Bay:** a copy of the Drive Bay with `LootList` `stashnetDriveBaySlotsReinforced`, `MaxDamage` 5000, and an upgrade to `stashnetDriveBayArmored` (Item `stashnetDriveBayArmored`).
  - **Armored Bay:** `LootList` `stashnetDriveBaySlotsArmored`, `MaxDamage` 10000, no `UpgradeBlock`, no `DowngradeBlock`.
  - **Scrap Core:** add an upgrade to `stashnetStorageCore`.
  - **Storage Core:** a copy of the Scrap Core with `StashNetRadius` 12, `StashNetHeatPerDrive` 0.18, `StashNetHeatCap` 6.5, `MaxDamage` 5000, and an upgrade to `stashnetAdvancedCore`.
  - **Advanced Core:** `StashNetRadius` 24, `StashNetHeatPerDrive` 0.12, `StashNetHeatCap` 6.5, `MaxDamage` 10000, no `UpgradeBlock`.
  - Every tier keeps its predecessor's model, size, features and feature order. Each tier gets its own tint, `DescriptionKey` and `SortOrder2`.
  - Update the comment above `StashNetHeatPerDrive` to say "per heat weight (Basic drive = 1)".

- [ ] **Step 4: recipes.xml.** Add six recipes, each with `craft_area="workbench" tags="workbenchCrafting"`, ingredients exactly as the spec's tables:
  - `stashnetDriveStandard`: forged iron 5, electrical parts 8, mechanical parts 2.
  - `stashnetDriveHighDensity`: forged steel 5, electrical parts 15, mechanical parts 4.
  - `stashnetDriveBayReinforced`: forged iron 30, mechanical parts 8, electrical parts 4.
  - `stashnetDriveBayArmored`: forged steel 25, mechanical parts 10, electrical parts 8.
  - `stashnetStorageCore`: forged iron 40, electrical parts 15, mechanical parts 10, `resourceRadiator` 1.
  - `stashnetAdvancedCore`: forged steel 30, electrical parts 25, mechanical parts 10, `resourceRadiator` 2.

- [ ] **Step 5: Localization.csv.**
  - Add names and descriptions for the six new entries. Each drive's description states its stack count and ends with: `Shift-click it while a Storage Terminal is open to empty its contents into the network.` Add that sentence to the Basic Drive's description too.
  - Each Core's description states its own reach.
  - Rewrite `stashnetScrapCoreDesc` (reach 6, stated as its own reach), `stashnetNoCore` and `stashnetCraftingTerminalDesc` so they don't say "within 6 blocks".
  - Names: Standard Drive, High-Density Drive, Reinforced Drive Bay, Armored Drive Bay, Storage Core, Advanced Storage Core.

- [ ] **Step 6: Check the XML parses and the build stages it**

Run (PowerShell): `foreach ($f in 'items','blocks','loot','recipes') { [xml](Get-Content "Mod/Config/$f.xml" -Raw) | Out-Null }; 'XML OK'`
Expected: `XML OK`.
Run: `dotnet build src/StashNet/StashNet.csproj -c Release`, then `grep -c "stashnetAdvancedCore" dist/StashNet/Config/blocks.xml`
Expected: `Build succeeded`, count ≥ 1.

- [ ] **Step 7: Commit**

```bash
git add Mod/Config
git commit -m "M8 tiers: Standard/High-Density drives, Reinforced/Armored bays, Storage/Advanced Cores" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Version, checklist, docs, final build

**Files:**
- Modify: `src/StashNet/StashNetMod.cs` (`Version = "0.9.0"`), `Mod/ModInfo.xml` (`0.9.0`)
- Create: `docs/testing/M8-checklist.md`
- Modify: `docs/PLAN.md`, `CLAUDE.md`

- [ ] **Step 1: Bump the version** in both files.

- [ ] **Step 2: Write `docs/testing/M8-checklist.md`** in the style of `M7-checklist.md`: plain language, checkboxes, `dm`/creative-menu tips. Steps in this order:
  1. **Upgrade carry-over (first).** Put 4 drives with items in a Drive Bay. Hammer a Reinforced Drive Bay onto it: the 4 drives are still there and there are 8 slots. Then go to Armored: 12 slots. Set a code on a Scrap Core and add a second player or alt, if available. Upgrade to Storage Core: the code is still asked for and the terminal still works. The reach grows (a terminal 10 blocks away now connects).
  2. Craft Standard and High-Density drives at a workbench. The terminal's stack total grows by 96 / 384.
  3. Fill a Basic drive, take it out, and try to scrap it: refused with the message. Shift-click it in a terminal to unload it, then scrap it: it works, and you get electrical parts.
  4. Armored Bay: hit it as a zombie would (or use `dm` damage). It takes far longer than a plain bay. When it breaks, a loot bag drops with all its drives, contents intact.
  5. Two Cores whose reaches overlap, with bays between them: `sn heat` at each Core. Every bay's drives count toward exactly one Core.
  6. Network at a chunk edge (a Core near a 16-block chunk border, bays on both sides): everything shows up. Nothing errors when walking away and back.
  7. On an Advanced Core with a few bays: terminal clicks and the Crafting Terminal feel instant.
  8. `sn heat` shows the heat weight (Basic 1, Standard 2, High-Density 4).
  9. The log shows `Loaded v0.9.0 (20 methods patched)` and no StashNet errors. Old Scrap Cores and bays from the test world still work.

- [ ] **Step 3: Update `docs/PLAN.md`.**
  - 2.1 table: remove the Drive Upgrade Kit row and add the Core and bay tiers.
  - 2.3: say you move up a tier by crafting the new drive, unloading the old one and scrapping it.
  - 2.4: give the slots 4/8/12 and the upgrade-with-the-next-tier-block rule.
  - 2.2: Core tier names.
  - "Later": add "Running cost (fuel or electricity), decide after playtesting M8."
  - Roadmap row 8: new test line from the spec.
  - Status line: "M8 built (v0.9.0), awaiting in-game test."

- [ ] **Step 4: Update `CLAUDE.md`.**
  - RESUME HERE: v0.9.0 is built on `m8-progression` and not deployed; next is the owner's M8 test.
  - Add a short "Progression (M8)" section: the tier names and numbers live in XML; vanilla upgrade carry-over via `ChangeBlocks` → `UpgradeDowngradeFrom`; the upgrade-tool allowlist append; discovery is by tile entity plus `CorePicker`; heat weight; the scrap guard; no Extends for tier blocks.

- [ ] **Step 5: Final verification**

Run: `dotnet test`
Expected: all pass (59 + 9 + 4 + 5 = 77).
Run: `dotnet build src/StashNet/StashNet.csproj -c Release`
Expected: `Build succeeded`, and `dist/StashNet/ModInfo.xml` says 0.9.0.

- [ ] **Step 6: Commit**

```bash
git add src/StashNet/StashNetMod.cs Mod/ModInfo.xml docs/testing/M8-checklist.md docs/PLAN.md CLAUDE.md
git commit -m "M8 Progression: v0.9.0, test checklist, plan and notes" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

Then stop. Ask the owner: "Can I deploy? (game closed)". Deploy only on a yes.
