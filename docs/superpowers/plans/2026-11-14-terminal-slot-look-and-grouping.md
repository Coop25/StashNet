# Terminal slot look + combined look-alikes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Terminal cells show the backpack's corner markings, and identical no-quality items differing only by seed show as one cell.

**Architecture:** Game-free folding/splitting in `StashNet.Core` (tested); the client terminal folds the server listing by a group key computed in game glue (`ItemCodec.GroupKey`); a withdraw from a cell sends one Withdraw request carrying each member's share; the server extracts every item in a Withdraw request. Cells bind vanilla `XUiBindingHelper` values on a display `ItemStack`.

**Tech Stack:** C# (netstandard2.0 core + game DLL), xUnit, 7DTD XUi XML.

**Spec:** `docs/superpowers/specs/2026-11-14-terminal-slot-look-and-grouping-design.md`

## Global Constraints
- Only items with `Quality == 0` combine; the key ignores the top-level `Seed` only.
- Storage format, capacity and stack-slot math do not change.
- Never give more than the server actually extracted (no dupes).
- Version bump only via `tools/release.ps1 -Version 0.18.0` (writes `dist/StashNet-v0.18.0.zip`).

## Review Focus
- A cell whose members were partly taken by another player: withdraw gives fewer, never errors.
- Shift-click on a combined one-per-slot cell takes exactly 1 (stack size 1).
- Undecodable variants (missing item): key = the exact variant, never merged or crashing.
- Plain items (variant null) still group by name exactly as today.
- Crafting Terminal's single-item Withdraw keeps working.

---

### Task 1: ListingGroups (game-free)

**Files:** Create `src/StashNet.Core/Protocol/ListingGroups.cs`; Test `tests/StashNet.Core.Tests/ListingGroupsTests.cs`

**Interfaces — Produces:**
- `sealed class ItemGroup { string Name; int Count; List<ItemAmount> Members; ItemAmount Display /* (Name, Members[0].Variant, Count) */ }`
- `static List<ItemGroup> ListingGroups.Fold(IEnumerable<ItemAmount> listing, Func<ItemAmount,string> keyOf)` — groups by (Name, keyOf(item)), keeps first-seen order.
- `static List<ItemAmount> ListingGroups.Split(IReadOnlyList<ItemAmount> members, int want)` — takes from members in order, each share ≤ member count, total ≤ want; skips zero shares.

- [ ] Tests: `Fold_MergesSameKey` (two engines, keys equal → one group, Count 3, 2 members); `Fold_KeepsDifferentKeysApart`; `Fold_SameKeyDifferentNamesApart`; `Split_TakesInOrder` (members 1,1,1 want 2 → [1,1]); `Split_CapsAtAvailable` (members 2 want 5 → [2]); `Split_ZeroWant_Empty`.
- [ ] Run `dotnet test tests/StashNet.Core.Tests` → new tests fail to compile; implement; rerun → all pass. Commit.

### Task 2: Server Withdraw takes several items

**Files:** Modify `src/StashNet/Network/TerminalServer.cs` (Withdraw), comment in `TerminalMessages.cs`.
- [ ] Withdraw loops over every `req.Items` entry (count > 0), extracting and adding what was removed to `Give`. Build passes. Commit with Task 3.

### Task 3: Group key + grouped terminal listing

**Files:** Modify `src/StashNet/Drives/ItemCodec.cs`, `src/StashNet/UI/XUiC_StashNetTerminal.cs`, `src/StashNet/UI/XUiC_StashNetEntry.cs`.
- `static string ItemCodec.GroupKey(string name, string variant)`: null variant → null; decode fails → variant; `Quality != 0` → variant; else set `seed = 0` and return `EncodeVariant` of it (cache by variant string).
- Terminal keeps `listing` (raw) and renders `List<ItemGroup>`; filter/sort/search use group Name/Count; `Withdraw(ItemGroup g, bool single)`: want = single ? 1 : min(g.Count, StackSize); request Items = `Split(g.Members, want)`.
- Entry holds an `ItemGroup`; hover text uses `group.Display`.
- [ ] Build `dotnet build src/StashNet/StashNet.csproj -c Release` → 0 errors.

### Task 4: Vanilla markings on cells

**Files:** Modify `XUiC_StashNetEntry.cs`, `Mod/Config/XUi_InGame/windows.xml`.
- Entry builds `ItemStack(display, count)` in SetItem; bindings `stashnet_typeicon` (`XUiBindingHelper.GetItemTypeIconValue`), `stashnet_hastypeicon`, `stashnet_typeicontint` (formatted `GetItemTypeIconTintColor`), `stashnet_permadur` (`EntityPlayerLocal.PermaDegrationOn && MaxDurabilityModifier < 1`).
- XML: corner sprite `ui_game_symbol_{stashnet_typeicon}` 20×20 at 2,-2; red background sprite under the durability bar; count labels get `effect="outline"`.
- [ ] Build → 0 errors. Commit.

### Task 5: Release
- [ ] `tools/release.ps1 -Version 0.18.0` → tests pass, zip written. Add checklist steps to `docs/testing/drive-fill-checklist.md` (or a new short section) and CLAUDE.md resume note. Commit.
