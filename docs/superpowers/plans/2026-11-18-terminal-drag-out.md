# Terminal Drag-Out Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Press-and-drag on a Storage Terminal cell takes one stack (one tool) onto the cursor, like a backpack pick-up.

**Architecture:** Client only. The drag sends the normal Withdraw; `TerminalClient` remembers the request id as "to cursor" and, when the reply arrives, puts the first returned stack on the vanilla drag-and-drop cursor if it is still empty; everything else goes to the backpack as today. The cell detects the drag in its `Update` the way `XUiC_ItemStack.Update` does.

**Tech Stack:** C# netstandard2.0 game-free core + xUnit; 7DTD V3.4 b10 XUi glue.

**Spec:** `docs/superpowers/specs/2026-11-18-terminal-drag-out-design.md`

## Global Constraints

- No protocol, server or save changes. Never lose items: anything that can't go on the cursor goes to the backpack path (which already returns overflow to the server).
- Amount = `TakeAmount.Stack(count, stackSize)` (tools: 1; resources: one stack or all if fewer).
- Drag threshold = vanilla `XUiC_ItemStack.PickupSnapDistance` (4 px), left button only.
- Click, second click, shift-click, right-click and held-item deposit unchanged; the release that ends a drag does nothing else.
- Version stays 1.1.0 (unreleased); rebuild the zip with `tools/release.ps1 -Version 1.1.0`. Never deploy without the owner's OK.

## Review Focus

1. Reply arrives after the player already put something else on the cursor (server lag) → whole reply goes to the backpack. → Task 1 test `CursorTakenGoesAllToBag`.
2. Grouped cell (several members) → only the first member goes on the cursor, the rest to the backpack. → Task 1 test `OnlyFirstEntryGoesOnCursor`.
3. Drag release fires the cell's click → must not also select/take. → Task 2 manual check (checklist step D3).
4. Window closed before the reply arrives → reply still applied (to backpack, since the cursor rule needs the drag window and terminal open). → Task 2 manual check D5.
5. Dragging an empty cell or with something already on the cursor → no request. → Task 2 manual check D4.

---

### Task 1: Cursor split (game-free)

**Files:** Create `src/StashNet.Core/Protocol/CursorTake.cs`; Test `tests/StashNet.Core.Tests/CursorTakeTests.cs`

**Interfaces:** Produces `public static class CursorTake { static (ItemAmount? Cursor, List<ItemAmount> Bag) Split(IReadOnlyList<ItemAmount> give, bool cursorFree); }` (namespace `StashNet.Core.Protocol`; `ItemAmount` is the existing struct in TerminalMessages.cs). Cursor = the first entry with Count > 0 when `cursorFree`; Bag = all other entries in order.

- [ ] Step 1: tests `FirstEntryGoesOnCursor`, `OnlyFirstEntryGoesOnCursor` (3 entries → cursor = first, bag = other two), `CursorTakenGoesAllToBag` (cursorFree false → cursor null, bag = all), `EmptyGiveGivesNothing`, `ZeroCountEntriesSkipped`.
- [ ] Step 2: `dotnet test tests/StashNet.Core.Tests --filter CursorTakeTests` → compile failure.
- [ ] Step 3: implement.
- [ ] Step 4: filter passes; full suite green.
- [ ] Step 5: commit "Cursor split for drag-out takes".

### Task 2: Drag glue

**Files:** Modify `src/StashNet/Network/TerminalClient.cs`, `src/StashNet/UI/XUiC_StashNetTerminal.cs`, `src/StashNet/UI/XUiC_StashNetEntry.cs`

**Interfaces:**
- `TerminalClient.SendToCursor(TerminalRequest request)`: like `Send`, and records `request.RequestId` in a `HashSet<int> cursorRequests` (the id is assigned inside `Send`, so record after it, or assign inside). `Apply`: when the id is in the set (remove it), `CursorTake.Split(response.Give, cursorFree)` where `cursorFree` = local player UI's `xui.DragAndDropWindow` exists and `IsEmpty()` and `XUiC_StashNetTerminal.Current` is open; decode the cursor entry with `ItemCodec.TryDecode` → `new ItemStack(value, count)` → `drag.SetCurrentStack(stack)` and `drag.PickUpType = XUiC_ItemStack.StackLocationTypes.Backpack` (so vanilla's put-back targets the backpack); if decode fails, it goes to the bag list. Bag entries continue through the existing backpack/overflow path.
- `XUiC_StashNetTerminal.WithdrawToCursor(ItemGroup group)`: same amount and `pendingTaken` bookkeeping as `Withdraw(group, false)`, but sends via a cursor variant of `SendAction` (factor the shared part; no duplicated loop).
- `XUiC_StashNetEntry.Update(float dt)`: mirror `XUiC_ItemStack.Update`'s mouse branch: if the cell's button view is hovered, left button went down → remember `startMousePos` (cursor screen position via `xui.playerUI.CursorController.GetScreenPosition()`); while held, if the cursor is empty, the cell has a group, not shift, and distance > 4 → `Owner.WithdrawToCursor(group)` once and set `dragTaken = true`. On button up, reset. `OnPress` returns immediately (and clears the flag) when `dragTaken` was set by this press.

- [ ] Step 1: implement the three pieces; build `dotnet build src/StashNet/StashNet.csproj -c Release` → 0 errors, 0 new warnings; full tests green.
- [ ] Step 2: commit "Storage Terminal: drag a stack out onto the cursor".

### Task 3: Checklist, zip, notes

- [ ] Append to `docs/testing/live-screen-checklist.md` a section "Drag out (1.1.0)": D1 drag a tool from the terminal → one tool on the cursor, click a hotbar slot to place it; D2 drag a resource → one full stack (or all if fewer); D3 a plain click still only selects, and the release after a drag doesn't select or take; D4 dragging with something already on the cursor stores it instead; D5 on the server: drag, then close the window quickly → the item ends up in the backpack, nothing lost; D6 drop a dragged stack back on the grid → it goes back into storage.
- [ ] `tools/release.ps1 -Version 1.1.0` → tests pass, zip rebuilt. CLAUDE.md RESUME line mentions drag-out. Commit.
