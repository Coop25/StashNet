# Milestone 3a: Network + Storage Terminal (first half) Implementation Plan

> **For agentic workers:** Implement task by task (superpowers:executing-plans). Steps use checkbox (`- [ ]`) syntax.

**Goal:** Pressing E on a Storage Terminal opens a screen that shows everything on every drive in the network (bays within the Core's radius). Click to withdraw. Shift-click or "Deposit all" to deposit.

**Architecture:** Game-free `NetworkStorage` (in StashNet.Core, tested) spreads inserts and extracts across several drives. The game layer discovers the network by scanning blocks around the terminal and Core, loads every drive in every bay, applies one operation, and saves the changed drives back into their bay slots. Each operation re-discovers and reloads, so nothing goes stale. The UI is an XUi window group with a paged grid of entry cells, opened from a custom Block class.

**Tech Stack:** C# net48 / netstandard2.0, xUnit, XUi XML (Config/XUi_InGame), Harmony.

**Spec:** `docs/PLAN.md` §2.2, §2.5 (Storage Terminal part), roadmap row 3.

## Global Constraints
- **Single-player and the host only in 3a.** On a remote client, the terminal shows "Multiplayer clients aren't supported yet" and doesn't open. 3b adds server-authoritative networking.
- **Never lose items.** Withdraw: backpack first, then remove from drives only what arrived. Deposit: drives saved first, then the backpack reduced by what was accepted.
- **Network shape:** a terminal joins the **nearest** Core within the Core's radius (cube distance, max(|dx|,|dy|,|dz|) ≤ radius). The network's bays are the Drive Bays within that same radius of the Core. Scrap Core radius = **6**, read from the Core block's XML property `StashNetRadius`.
- **Deposit preference** (spec §2.5): drives that already hold that exact item first, then the drive with the most free item room.
- Skip a bay while a player has it open (`IsUserAccessing`), and skip unreadable drives (left untouched). The screen says how many were skipped.
- UI strings are in Localization.csv.

## Review Focus
1. **Several networks near each other:** a terminal must use the nearest Core and only that Core's bays. (Discovery code + checklist.)
2. **Withdraw when the backpack is full:** whatever doesn't fit stays in the network. (Same order rule as M2 + checklist.)
3. **A drive pulled from a bay while the terminal is open:** the next action reloads, so there's no stale write-back into the wrong drive. (Reload per op + checklist.)
4. **No Core in range / no bays / empty drives:** the screen opens with a clear message instead of an error. (Checklist.)
5. **Other mods' windows and containers:** the shift-click hook only acts while the StashNet terminal window is open. (Patch guard + checklist.)

---

### Task 1: NetworkStorage (Core, TDD)
**Files:** Create `src/StashNet.Core/Network/NetworkStorage.cs`, `tests/StashNet.Core.Tests/NetworkStorageTests.cs`

**Interfaces (Produces):**
- `sealed class NetworkDrive(DriveContents contents, DriveCapacity capacity, object tag)` with `Contents`, `Capacity`, `Tag`, `bool Dirty`
- `sealed class NetworkItem { string Name; string Variant; int Count }`
- `sealed class NetworkStorage(IReadOnlyList<NetworkDrive> drives)`
  - `int Insert(string name, string variant, int count)`: preference rule above. Sets `Dirty` on the drives it touches.
  - `int Extract(string name, string variant, int count)`: takes from drives in order. Sets `Dirty`.
  - `IReadOnlyList<NetworkItem> Aggregate()`: merged by (name, variant), in first-seen order
  - `int UsedTypes, UsedItems, MaxTypes, MaxItems` (sums)

**Tests:** prefers the drive already holding the item; spills over to the next drive; picks the drive with the most free room for a new type; returns 0 when all are full; extracts across drives; aggregates by summing; marks dirty only on touched drives; 0 drives behaves like empty.

### Task 2: Network discovery + operations (game glue)
**Files:** Create `src/StashNet/Network/StashNetNetwork.cs`, `src/StashNet/Network/TerminalOps.cs`. Modify `Mod/Config/blocks.xml` (Core `StashNetRadius` 6; Terminal `Class`).
- `StashNetNetwork.TryOpen(World world, Vector3i terminalPos, out StashNetNetwork net, out string problem)`: finds the Core and bays, loads drives into a `NetworkStorage`. Counts skipped bays and drives.
- `net.SaveDirty()`: for each dirty drive, `DriveItem.Save` into a clone of the slot's ItemValue → `storage.UpdateSlot(i, stack)` + `storage.SetModified()`.
- `TerminalOps.Withdraw(player, terminalPos, name, variant, count) -> int moved`
- `TerminalOps.DepositStack(player, terminalPos, ItemStack stack) -> int accepted` (stack count reduced by the caller)
- `TerminalOps.DepositBackpack(player, terminalPos) -> int moved` (skips locked slots, drives, unstorable items)

### Task 3: Terminal block + window + controllers
**Files:** Create `src/StashNet/Blocks/BlockStashNetTerminal.cs`, `src/StashNet/UI/XUiC_StashNetTerminal.cs`, `src/StashNet/UI/XUiC_StashNetEntry.cs`, `src/StashNet/Patches/TerminalShiftClickPatch.cs`, `Mod/Config/XUi_InGame/windows.xml`, `Mod/Config/XUi_InGame/xui.xml`. Modify `Mod/Config/Localization.csv`.
- Block: activation text "Press E to use Storage Terminal". E opens window group `stashnetTerminal` (backpack opens alongside). Remote client → tooltip message only.
- Window: header with title and a usage label (`x/y types, a/b items`), a status line (problems / skipped), a 8×6 grid of entries (icon, count, hovered name in the footer), Prev/Next paging, a "Deposit all" button.
- Entry click: left = one stack, right = one item. Refresh after each action.
- Shift-click in the backpack/toolbelt while the terminal is open → deposit that stack.

### Task 4: Version 0.3.0, checklist, docs, commit
