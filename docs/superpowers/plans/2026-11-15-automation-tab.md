# Automation Tab Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the automation controls into a third tab of the recipe box with explanatory fuel text, and give Network stations their vanilla category tabs.

**Architecture:** XUi append into vanilla `craftingInfoPanel` with our controller; a postfix on `XUiC_CraftingInfoWindow.GetBindingValueInternal` hides the vanilla tab contents while ours is open. Category tabs: a prefix on `XUiC_CategoryList.SetupCategoriesByWorkstation` swaps our block name for the vanilla station named by the block's `StashNetCategories` property.

**Tech Stack:** C#, Harmony, 7DTD XUi XML, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-15-automation-tab-design.md`

## Global Constraints

- Server behaviour, protocol and saving unchanged. Version via `tools/release.ps1 -Version 0.21.0`. Deploy only with the owner's OK, game closed.
- Every XUi controller used in a vanilla window must survive its own Init failure without breaking the vanilla window (v0.20.0 lesson); `Counter` needs `countUp`, `countDown`, `countMax` and a textfield.
- Fuel text (exact): Manual "You light it yourself, like a normal {0}." / Auto "Lights itself when there's a craft or ore to melt, goes out when idle. Uses the fuel you put in." / fuel item "Lights itself when there's a craft or ore to melt, goes out when idle, and refills {0} from storage (one stack)."

## Review Focus

- The automation tab must never appear in backpack crafting or at vanilla stations (owning code: Task 2 `AtNetworkStation`).
- Selecting another recipe while on the Automation tab keeps the tab open and updates "Keep <recipe>".
- Pressing Ingredients/Description/Unlocks leaves the Automation tab and shows that content.
- Closing and reopening the station starts on Ingredients.
- Category tabs on the Network Workbench and Forge match the vanilla workbench and forge.

---

### Task 1: FuelModes.Previous

**Files:** Modify `src/StashNet.Core/Stations/StationSettings.cs`; Test `tests/StashNet.Core.Tests/StationSettingsTests.cs`

**Interfaces:** Produces `static string FuelModes.Previous(string current, IEnumerable<string> fuelsInStorage)` (same order as `Next`, reversed; unknown current → Manual).

- [ ] Test `FuelModes_Previous_Cycles`: fuels [wood, coal] → `Previous("Manual")=="wood"`, `Previous("wood")=="coal"`, `Previous("coal")=="Auto"`, `Previous("Auto")=="Manual"`, `Previous("gas",["coal"])=="Manual"`. Run `dotnet test tests/StashNet.Core.Tests` → compile failure. Implement. Run → pass. Commit.

### Task 2: Automation tab UI (replaces the panel window)

**Files:** Modify `Mod/Config/XUi_InGame/windows.xml` (remove window `stashnetAutomation`, append tab into `craftingInfoPanel/rect[@name='contentCraftingInfo']`), `Mod/Config/XUi_InGame/xui.xml` (remove the window-group append), `Mod/Config/Localization.csv`, `src/StashNet/UI/XUiC_StashNetAutomation.cs`; Create `src/StashNet/UI/AutomationTabPatches.cs`.

**Interfaces:** Consumes Task 1. Produces `static bool XUiC_StashNetAutomation.TabOpen` (true only while our tab shows at a Network station).

- [ ] XML: rect with our controller holding the tab button (`btnAutomationTab`, sprite `ui_game_symbol_electric_power`, at searchControls x 151, visible `{stashnet_atstation}`) and the content rect (pos 153,-43, 447×303, visible `{stashnet_tabopen}`): keep row (label, Counter with hidden countMax, Keep/Update button), "Kept in stock at this station:" header, 8 rule rows of 21 px, fuel row (`◄ mode ►`, visible `{stashnet_hasfuel}`), fuel explanation label.
- [ ] Controller: `AtNetworkStation()` from the window group's TileEntity block; tab button sets `TabOpen`, deselects vanilla tab buttons, marks the info window dirty; vanilla tab buttons' OnPress close it; OnClose resets it; Init guarded with try/catch.
- [ ] Patches: postfix `XUiC_CraftingInfoWindow.GetBindingValueInternal` → "false" for `showingredients/showdescription/showunlockedby` while `TabOpen`; postfix `XUiC_CraftingInfoWindow.SetRecipe` → while `TabOpen`, deselect the vanilla tab buttons again.
- [ ] Localization: `stashnetAutomationTab`, `stashnetKeptHere`, `stashnetFuelLabelShort`, `stashnetFuelManualTip`, `stashnetFuelAutoTip`, `stashnetFuelItemTip` (exact text above).
- [ ] `dotnet build src/StashNet/StashNet.csproj -c Release` → 0 errors; XML parses. Commit.

### Task 3: Category tabs for Network stations

**Files:** Modify `Mod/Config/blocks.xml` (`StashNetCategories` on the 5 stations: workbench, forge, chemistryStation, campfire, cementMixer); Create `src/StashNet/Crafting/StationCategoryPatch.cs`.

- [ ] Prefix `XUiC_CategoryList.SetupCategoriesByWorkstation(ref string _workstation)`: if `Block.GetBlockByName(_workstation)` has `StashNetCategories`, replace `_workstation` with it. Build → 0 errors. Commit.

### Task 4: Release

- [ ] Update `docs/testing/automation-checklist.md` section 1 (tab, categories), `CLAUDE.md` resume line; `tools/release.ps1 -Version 0.21.0` → tests pass, zip. Commit. Ask the owner to deploy.
