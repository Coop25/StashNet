# Milestone 5: Drive Unload Implementation Plan

> **For agentic workers:** Implement task by task (superpowers:executing-plans). Steps use checkbox (`- [ ]`) syntax.

**Goal:** Shift-clicking a drive in the backpack while a Terminal is open moves that drive's contents into the network (as much as fits). Leftovers stay on the drive, and the drive stays where it is.

**Scope decision (owner, 2026-11-12):** with drive locks dropped, the Drive Port is **unload only**: no "fill drive from network", no wipe, no format.

**Architecture:** A new protocol op `DriveUnload` goes through the same server path as every terminal action. The client empties the drive in place first and tags it with a one-time token (`StashNetPending` metadata). The server inserts what fits and returns the rest in a new response list `ReturnToDrive`. The client finds the tagged drive (backpack, then toolbelt), writes the leftovers back onto it, and removes the tag. If the drive can't be found (moved or dropped mid-request), the leftovers go to the backpack as items instead. They're never lost.

**Spec:** `docs/PLAN.md` §2.5 (Drive Port), as revised.

## Global Constraints
- Same never-lose ordering as deposits: the drive is emptied client-side, then sent, and the server returns whatever it didn't take.
- Locked network / out of reach / no Core → everything returns to the drive.
- An empty or unreadable drive → a message, and no request is sent.
- Known limit (same as any deposit in flight): if the connection drops between send and reply, those items are lost. Single-player/host is synchronous, so this can't happen there.

## Review Focus
1. **Network fills up mid-unload:** the leftovers are back on the drive, with the right counts.
2. **Drive moved to another slot before the reply arrives (multiplayer):** found by its token.
3. **Unloading weapons with mods:** they arrive in the network intact (same codec as deposits).
4. **Drive in the toolbelt** (held) can be shift-clicked too.
5. **Deposit-all still never touches drives.**

### Task 1: Protocol (Core, TDD)
- `TerminalOp.DriveUnload = 5`. `TerminalResponse.ReturnToDrive: List<ItemAmount>` (serialized after `Give`).
- Tests: op 5 round-trips; ReturnToDrive round-trips; an op byte of 6 throws.

### Task 2: Server
- `TerminalServer`: DriveUnload = Deposit semantics, but leftovers go into `ReturnToDrive` (not `Give`). Failure paths return all items via `ReturnToDrive`.

### Task 3: Client + UI
- `XUiC_StashNetTerminal.DepositFromSlot`: a drive → `UnloadDrive(slot)`.
- `TerminalClient.SendDriveUnload(request, token)` + `Apply`: restore leftovers onto the tagged drive. Fallback: the backpack (overflow → Return).
- Localization: "Unloaded N items from the drive", "That drive is empty", "Some items stayed on the drive (storage full)".

### Task 4: v0.6.0, checklist, docs, commit
