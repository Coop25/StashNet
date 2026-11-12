# Milestone 1: Blocks & Items Implementation Plan

> **For agentic workers:** Implement task by task (superpowers:executing-plans). Steps use checkbox (`- [ ]`) syntax.

**Goal:** The four starter pieces exist in-game: craftable, placeable, using vanilla looks. The Drive Bay is a lockable 4-slot container that accepts only drives.

**Architecture:** Mostly XML patches in `Mod/Config/`. One custom composite tile-entity feature marks Drive Bays. Two Harmony hooks enforce "drives only": `ItemStack.CanMoveTo` (drag-drop, drop-one, shift-click) and `XUiM_LootContainer.StashItems` (stash buttons).

**Tech Stack:** 7DTD V3.4 XML patching (XPath `append`), C# net48, HarmonyX (bundled with the game).

**Spec:** `docs/PLAN.md` sections 2.1–2.4, roadmap row 1.

## Global Constraints
- Every new block and item name starts with `stashnet` (avoids clashes with the owner's other mods).
- The item tag `stashnetDrive` marks every drive item. Drive Bay logic checks the tag, never item names, so later drive tiers work automatically.
- Drives do not stack (`Stacknumber` 1). Each drive carries its own contents.
- Vanilla models and tinted vanilla icons only. No custom art.
- Recipes use early-game materials only and are craftable in the backpack.
- Never deploy into the game folder without the owner's explicit yes, and only while the game is closed.

## Review Focus
1. **Shift-click from the backpack** while a Drive Bay is open: non-drives must stay in the backpack. (Covered by the CanMoveTo hook. Checklist step.)
2. **Stash buttons** ("stash all" / "fill stacks" / "smart stash") on a Drive Bay must move only drives. (StashItems hook. Checklist step.)
3. **Hooks must not leak into other containers:** a normal crate or your other storage mods still accept everything. (Hooks only act when the open container has the Drive Bay feature. Checklist step.)
4. **Destroying a Drive Bay** drops its drives in a loot bag and doesn't delete them. (Vanilla TEFeatureStorage behavior. Checklist step.)
5. **The lock:** the Drive Bay can be locked with a code like a vanilla crate. (TEFeatureLockable. Checklist step.)

---

### Task 1: Items, blocks, loot container, recipes, localization (XML)

**Files:**
- Create: `Mod/Config/items.xml`: `stashnetDriveBasic`
- Create: `Mod/Config/blocks.xml`: `stashnetScrapCore`, `stashnetDriveBay`, `stashnetTerminal`
- Create: `Mod/Config/loot.xml`: lootcontainer `stashnetDriveBaySlots`, size `4,1`
- Create: `Mod/Config/recipes.xml`
- Create: `Mod/Config/Localization.csv`

**Values:**

| Name | Model (borrowed) | Notes |
|---|---|---|
| `stashnetScrapCore` | electricalBox02 model, icon `electricalBox01` tinted | MaxDamage 1000, pick-up-able |
| `stashnetDriveBay` | `serverComputerPrefab` (1×2×1), icon `decoServerRackWhite` tinted | CompositeTileEntity: TEFeatureStorage(`stashnetDriveBaySlots`) + TEFeatureLockable + TEFeatureStashNetDriveBay; MaxDamage 1000 |
| `stashnetTerminal` | `monitorKeyboardMousePCPrefab` (2×1×1), icon `decoComputerMonitorKeyboardMousePC` tinted | MaxDamage 500, pick-up-able |
| `stashnetDriveBasic` | icon `resourceElectricParts` tinted | Stacknumber 1, Tags `stashnetDrive` |

| Recipe | Ingredients |
|---|---|
| Scrap Storage Core | scrap iron 20, electrical parts 5, duct tape 2 |
| Drive Bay | scrap iron 15, wood 10, electrical parts 2 |
| Storage Terminal | scrap iron 10, electrical parts 6, scrap polymers 4, duct tape 1 |
| Basic Drive | electrical parts 2, scrap polymers 2, duct tape 1 |

- [ ] Write the five files.
- [ ] Verify: build stages them into `dist/StashNet/Config/`. In-game the log has no `XML` / `[MODS]` errors mentioning StashNet.

### Task 2: Drive Bay marker feature + drives-only hooks (C#)

**Files:**
- Create: `src/StashNet/Blocks/TEFeatureStashNetDriveBay.cs`
- Create: `src/StashNet/Blocks/DriveRules.cs`
- Create: `src/StashNet/Patches/DriveBayOnlyDrivesPatches.cs`

**Interfaces:**
- `public class TEFeatureStashNetDriveBay : TEFeatureAbs`: parameterless constructor; `CopyFromInternal` no-op. The class name must match the XML `property class`.
- `public static class DriveRules`
  - `public const string DriveTag = "stashnetDrive";`
  - `public static bool IsDrive(ItemValue iv)`: true if the item class has the `stashnetDrive` tag.
  - `public static bool IsDriveBay(TEFeatureStorage storage)`: true if `storage?.Parent?.GetFeature<TEFeatureStashNetDriveBay>() != null`.
  - `public static bool OpenContainerIsDriveBay()`: the local player's `xui.LootContainer` is a Drive Bay.
- Patch `ItemStack.CanMoveTo(StackLocationTypes, int)` postfix: if the result is true, the location is `LootContainer`, the open container is a Drive Bay, and the item is not a drive → result false.
- Patch `XUiM_LootContainer.StashItems` prefix: if `_dstInventory` is a Drive Bay `TEFeatureStorage` → replace `_ignoredSlots` with a copy that also marks every non-drive source slot as ignored.

- [ ] Write the files.
- [ ] Verify: `dotnet build` succeeds with 0 warnings and the game log shows the Harmony patches applied without errors. Behavior is covered by the M1 in-game checklist (Review Focus 1–3).

### Task 3: Checklist + docs + commit
- [ ] `docs/testing/M1-checklist.md` covers crafting, placing, the drives-only rule (all 3 paths), other containers unaffected, lock, destroy → loot bag.
- [ ] Update `CLAUDE.md` current milestone. Commit and push.
