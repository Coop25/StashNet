# Drive Balance + Fill Bar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Drive capacities 24/72/216, each drive tier crafted from 2 empty drives of the tier below, and a fill bar + centered percentage + live description line on every drive.

**Architecture:** XML changes for capacity, recipes and text. Fill math is game-free in `StashNet.Core` (tested). The game glue is Harmony postfixes: `Bag`/`Inventory` item counting and removal skip non-empty drives, and two `GetBindingValueInternal` postfixes feed new bindings. One is on `XUiC_ItemStack`, for a bar and a label added to the vanilla `item_stack` template by XPath. The other is on `XUiC_ItemInfoWindow`, for the description.

**Tech Stack:** C# (game's mscorlib), HarmonyLib, 7DTD XML/XPath, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-14-drive-balance-and-fill-bar-design.md`

## Global Constraints
- Capacities: Basic 24, Standard 72, High-Density 216 (`StashNetMaxStacks`). Heat weights stay 1/2/4.
- Standard recipe adds `stashnetDriveBasic` ×2; High-Density adds `stashnetDriveStandard` ×2. Other ingredients and `craft_area="workbench"` unchanged.
- A drive "holds items" exactly when `DriveItem.HoldsItems` says so (unreadable data counts as holding items).
- Bar colors: green below 75%, yellow from 75%, red when full (used ≥ max).
- Percentage text: `floor(used*100/max)`, but at least `1%` when used > 0. Over-full shows the real value (e.g. `133%`). Empty → no text and no bar. Unreadable → text `?`, no bar.
- Bar fill = min(1, used/max). It's display only: never set `MaxUseTimes`/`UseTimes` on drives.
- Game code must not touch XUi on a dedicated server. All new UI code is client-side binding code.
- Every new localization key starts with `stashnet`.

## Review Focus
1. **A full drive sitting in the backpack next to an empty one when you craft an upgrade.** Only the empty one is used. Test: `Bag`/`Inventory` removal skips a non-empty drive (in-game checklist line 3; the patches can't be unit-tested without the game).
2. **A recipe that looks craftable because of full drives.** The ingredient count must show only empty drives (checklist line 4).
3. **An over-full drive from before the change** (> 216 stacks). Red full bar, text like `133%`, and it takes no new items. Test `Fill_OverFull_ShowsRealPercent` (Task 2), plus checklist line 6.
4. **A tiny fill rounding to 0%.** It must show `1%`. Test `Fill_OneStack_ShowsAtLeastOnePercent` (Task 2).
5. **Patched slot bindings on a dedicated server / in non-player slots** (bays, loot bags). The bindings only read the slot's own `itemStack` and never `EntityPlayerLocal` (code review, plus checklist line 5).

---

### Task 1: Capacities, recipes, descriptions

**Files:**
- Modify: `Mod/Config/items.xml` (`StashNetMaxStacks` 96 → 72, 384 → 216; the header comment if it lists numbers)
- Modify: `Mod/Config/recipes.xml` (Standard and HD recipes)
- Modify: `Mod/Config/Localization.csv` (Standard/HD descriptions)
- Modify: `CLAUDE.md`, `docs/PLAN.md`, `README.md` wherever 96/384 appear for drives

- [ ] **Step 1:** Edit the XML values per Global Constraints.
- [ ] **Step 2:** Rewrite the three drive descriptions. Exact new size wording: Basic "Holds 24 stacks, like a small chest." (unchanged), Standard "Holds 72 stacks. Crafted from two empty Basic Drives.", HD "Holds 216 stacks. Crafted from two empty Standard Drives." Append the same sentence to all three: "The bar under the drive shows how full it is, and the number in the middle is the percent used."
- [ ] **Step 3:** `grep -rn "384\|96 stacks" Mod README.md docs/PLAN.md CLAUDE.md` shows no stale drive numbers.
- [ ] **Step 4:** Commit: `Drives: capacity 24/72/216, upgrades cost 2 drives of the tier below`.

### Task 2: Game-free fill math

**Files:**
- Create: `src/StashNet.Core/Drives/DriveFill.cs`
- Test: `tests/StashNet.Core.Tests/DriveFillTests.cs`

**Interfaces:**
- Produces:
  - `public enum FillBand { Empty, Low, High, Full, Unreadable }` (Low < 75%, High ≥ 75%, Full = used ≥ max)
  - `public readonly struct DriveFill { int Used; int Max; int Types; FillBand Band; float Fraction /* 0..1 */; string PercentText /* "", "?", "66%" */ }`
  - `public static DriveFill DriveFill.FromText(string driveText, int maxStacks, Func<string,int> stackSizeOf)` uses `DriveSerializer.Deserialize`. A `DriveFormatException` gives `Unreadable`. Null/empty text gives `Empty`.

- [ ] **Step 1: Write failing tests:** `Fill_NullText_IsEmpty` (Band Empty, PercentText "", Fraction 0); `Fill_BadText_IsUnreadable` (PercentText "?"); `Fill_OneStack_ShowsAtLeastOnePercent` (1 of 216 → "1%"); `Fill_TwoThirds_Floors` (144/216 → "66%", Band Low); `Fill_SeventyFivePercent_IsHigh` (54/72 → "75%", High); `Fill_Full_IsFull` (72/72 → "100%", Full, Fraction 1); `Fill_OverFull_ShowsRealPercent` (288/216 → "133%", Full, Fraction 1); `Fill_CountsTypes` (3 entries → Types 3). Build contents with `DriveContents.AddLoaded` + `DriveSerializer.Serialize`, stack size 1 via `_ => 1`.
- [ ] **Step 2:** `dotnet test tests/StashNet.Core.Tests --filter DriveFillTests` → fails (type missing).
- [ ] **Step 3:** Implement `DriveFill`.
- [ ] **Step 4:** Same command → all pass; then a full `dotnet test` → all pass.
- [ ] **Step 5:** Commit: `Core: DriveFill (used/max, band, percent text)`.

### Task 3: Non-empty drives are never crafting ingredients

**Files:**
- Create: `src/StashNet/Patches/DriveIngredientGuardPatches.cs`

**Interfaces:**
- Consumes: `DriveRules.IsDrive(ItemValue)`, `DriveItem.HoldsItems(ItemValue)`.

- [ ] **Step 1:** Postfixes on `Bag.GetItemCount(ItemValue,int,int,bool)` and `Inventory.GetItemCount(ItemValue,bool,int,int,bool)`. When `_itemValue` is a drive type, subtract the count of matching slots whose drive `HoldsItems`. Drives don't stack, so each one counts as 1.
- [ ] **Step 2:** Prefixes on `Bag.DecItem` and `Inventory.DecItem` (signature `(ItemValue, int, bool, IList<ItemStack>)`). For drive types only, run the vanilla loop but skip slots whose drive `HoldsItems`. Set `__result` and return `false`. Every other type returns `true` (vanilla runs). For `Inventory`, clear slots the vanilla way (`SetStackAt(i, ItemStack.Empty)`). The toolbelt fires its own changed events through `SetStackAt`.
- [ ] **Step 3:** `dotnet build src/StashNet/StashNet.csproj -c Release` → 0 errors. The patch count printed at load rises by 4 (check the log line at deploy).
- [ ] **Step 4:** Commit: `Crafting never consumes a drive that holds items`.

### Task 4: Fill bar + centered percentage in every item slot

**Files:**
- Create: `src/StashNet/UI/DriveFillCache.cs`
- Create: `src/StashNet/Patches/DriveSlotFillPatch.cs`
- Create: `Mod/Config/XUi_InGame/templates.xml` (XPath into vanilla `item_stack`)

**Interfaces:**
- Consumes: `DriveFill.FromText` (Task 2), `DriveItem.TryGetCapacity`, `DriveItem.MetadataKey`, `ItemCodec.StackSizeOf`.
- Produces: `public static DriveFill DriveFillCache.Get(ItemValue drive)`. Keyed by the metadata string (null key → Empty), the cache is cleared when it holds more than 256 entries. Task 5 uses it too.

- [ ] **Step 1:** Implement `DriveFillCache.Get`.
- [ ] **Step 2:** Postfix on `XUiC_ItemStack.GetBindingValueInternal(ref string, string)` handling these binding names (returning `__result = true`) for any slot:
  - `stashnethasfill`: "true" only for a drive with Band Low/High/Full.
  - `stashnetfill`: Fraction, invariant culture.
  - `stashnetfillcolor`: Low `"0,170,0,255"`, High `"230,190,0,255"`, Full `"200,30,30,255"`.
  - `stashnetfilltext`: PercentText, "" for non-drives.
- [ ] **Step 3:** XPath in `templates.xml`: insert after the vanilla durability `filledsprite` in `item_stack`:
  - a background `sprite` (`[darkGrey]`) and a `filledsprite` with `fill="{stashnetfill}" color="{stashnetfillcolor}" visible="{stashnethasfill}"`, using the vanilla bar's size and position;
  - a `label` `text="{stashnetfilltext}"` centered on the icon (same `pos` and `pivot` as the vanilla `timer` label), `font_size="20"`, `effect="outline"`, `color="[white]"`, depth 8.

  Check the mod's XPath file convention by reading an existing file in `Mod/Config/XUi_InGame/`.
- [ ] **Step 4:** Build → 0 errors. Confirm `dist/StashNet/Config/XUi_InGame/templates.xml` is staged.
- [ ] **Step 5:** Commit: `Drive slots show a fill bar and percent`.

### Task 5: Live line in the item description

**Files:**
- Create: `src/StashNet/Patches/DriveInfoDescriptionPatch.cs`
- Modify: `Mod/Config/Localization.csv` (key `stashnetDriveFillLine`, english `{0} / {1} stacks used ({2}) · {3} different items`; key `stashnetDriveFillEmpty`, english `Empty.`; key `stashnetDriveFillUnreadable`, english `This drive's data can't be read.`)

- [ ] **Step 1:** Postfix on `XUiC_ItemInfoWindow.GetBindingValueInternal`. For `bindingName == "itemdescription"` and `__instance.itemStack` a drive, prepend the formatted line + `"\n\n"`, using `DriveFillCache.Get`.
- [ ] **Step 2:** Build → 0 errors; `dotnet test` → all pass.
- [ ] **Step 3:** Bump the version (csproj / `ModInfo.xml` / version constant, wherever the previous bump changed it; see commit `dcc3e7d`) to **0.11.0**. Build with `-p:Package=false`.
- [ ] **Step 4:** Commit: `v0.11.0: live drive fill line in the description`.

### Task 6: In-game checklist (owner)
Add `docs/testing/drive-fill-checklist.md`:
1. Drives in the backpack: an empty one shows no bar or number; a partly filled one shows a green bar and e.g. "12%"; a full one shows red "100%".
2. Selecting a drive shows the "N / M stacks used" line above its description.
3. With one full and one empty Basic Drive in the backpack, crafting a Standard Drive (needs 2) is refused. With two empty ones it works, and the full one is untouched.
4. The Standard recipe's ingredient count shows only empty drives.
5. Drives in a Drive Bay and in a dropped loot bag show the same bar and number.
6. An HD drive holding over 216 stacks (fill it with dev commands if none exists) shows red and the real percent, and deposits skip it.
7. The log shows `Loaded v0.11.0 (N methods patched)` with no StashNet errors.
