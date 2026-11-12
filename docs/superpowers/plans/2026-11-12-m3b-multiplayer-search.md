# Milestone 3b: Multiplayer + Search/Sort Implementation Plan

> **For agentic workers:** Implement task by task (superpowers:executing-plans). Steps use checkbox (`- [ ]`) syntax.

**Goal:** Storage Terminals work for players connected to a server (not just single-player/host), with no item duplication or loss. The terminal screen gets a search box, a sort toggle (default: highest count first), and live refresh.

**Architecture:** One request/response protocol for every terminal action. The **server** owns all drive changes: it receives a request, runs it through the existing `StashNetNetwork`/`NetworkStorage` code, and replies. In single-player and on the host, the same handler is called directly (no packet). On a remote client, it travels in a `NetPackage`. The client's backpack is only ever changed **on the client**, by the client, from what the server's reply says.

**Tech Stack:** C# net48 / netstandard2.0, xUnit, 7DTD `NetPackage` / `ConnectionManager`, XUi.

**Spec:** `docs/PLAN.md` §2.5, §2.8, roadmap row 3.

## Global Constraints
- **Server is the only writer of drive data.** Clients never modify drives or bays.
- **Never lose, never duplicate:**
  - *Deposit:* the client removes the stack from its backpack, then sends it. The server inserts what fits and **returns the rest**, and the client puts it back.
  - *Withdraw:* the server removes from drives, then sends the items. If the client can't fit them all, it sends the overflow back as a **Return**. The server inserts it, and if storage is full too, **drops it at the player's feet**. A Return never bounces again.
- **The server validates every request:** the sender's player exists, the block at the position is a StashNet terminal, and the player is within **8 blocks** of it. Invalid requests are ignored and logged.
- Single-player/host calls the same server handler directly, so there's one code path.
- The dedicated server has no UI. Server-side code never touches XUi or `EntityPlayerLocal`.
- Live refresh: while a terminal is open, the client asks for the listing **every 2 seconds** and after every action.

## Review Focus
1. **Two players withdraw the last 10 nails at once:** one gets 10, the other gets 0. Never 20. (The server processes requests sequentially + checklist with 2 players / server + client.)
2. **The client's backpack is full when withdrawn items arrive:** the overflow returns to storage, or drops on the ground. Never vanishes. (Return path + checklist.)
3. **Deposit when storage is full:** everything not accepted comes back to the backpack. (Leftover-return + checklist.)
4. **The player walks away / closes the terminal mid-request:** the reply still lands in the backpack (give/return don't depend on the window being open). (Client handler independent of UI.)
5. **The dedicated server must not crash** loading the mod (no UI types touched server-side). (Checklist: start a dedicated server, check its log.)

---

### Task 1: Terminal protocol (Core, TDD)
**Files:** Create `src/StashNet.Core/Protocol/TerminalMessages.cs`, `tests/StashNet.Core.Tests/TerminalMessagesTests.cs`

**Interfaces (Produces):**
- `enum TerminalOp : byte { List = 1, Withdraw = 2, Deposit = 3, Return = 4 }`
- `struct ItemAmount { string Name; string Variant; int Count }`
- `sealed class TerminalRequest { TerminalOp Op; int X, Y, Z; ItemAmount Item /* Withdraw */; List<ItemAmount> Items /* Deposit/Return */; int RequestId }`
- `sealed class TerminalResponse { int RequestId; List<ItemAmount> Listing; int DistinctItems, UsedStacks, MaxStacks, Drives; List<string> MessageKeys; List<ItemAmount> Give; bool HasListing }`
- `static class TerminalCodec`: `void Write(BinaryWriter, TerminalRequest)`, `TerminalRequest ReadRequest(BinaryReader)`, the same for the response. Count/length sanity limits (max 10,000 entries, counts > 0), else `FormatException`.

**Tests:** round-trip each op; null variants survive; empty lists; bad op byte → throws; negative count → throws; an oversized list → throws.

### Task 2: Server handler (game glue)
**Files:** Create `src/StashNet/Network/TerminalServer.cs`. Modify `TerminalOps.cs` (becomes server-only helpers, with no `Bag` access).
- `TerminalResponse TerminalServer.Handle(World world, EntityPlayer player, TerminalRequest req)`: validate → open the network →
  - List: listing + usage + problem keys.
  - Withdraw: `Extract(min(count, available))` → `Give`.
  - Deposit: `Insert` each item → `Give` the leftovers + "storage full" key.
  - Return: `Insert` → whatever doesn't fit is dropped with `GameManager.ItemDropServer` at the player's position. Never in `Give`.
  - Always includes a fresh listing.

### Task 3: Transport (NetPackages + client dispatcher)
**Files:** Create `src/StashNet/Network/NetPackageStashNetRequest.cs`, `NetPackageStashNetResponse.cs`, `src/StashNet/Network/TerminalClient.cs`.
- `TerminalClient.Send(TerminalRequest)`: if `IsServer` → `Handle` directly → `Apply(response)`. Else `SendToServer(package)`.
- Request package: ToServer. `ProcessPackage` → `Handle(world, sender's EntityPlayer, req)` → `SendPackage(response, attachedToEntityId: sender)`.
- Response package: ToClient. `ProcessPackage` → `TerminalClient.Apply(response)`.
- `TerminalClient.Apply`: (1) put `Give` items into the local backpack with `BagTransfer`; any overflow → `Send(Return)`. (2) If a terminal window is open, hand it the listing and messages.

### Task 4: Terminal UI on the new protocol + search/sort + live refresh
**Files:** Modify `src/StashNet/UI/XUiC_StashNetTerminal.cs`, `src/StashNet/Blocks/BlockStashNetTerminal.cs` (remove the client block), `Mod/Config/XUi_InGame/windows.xml`, `Localization.csv`.
- The window renders the last listing it received. Actions: Withdraw (stack/one), Deposit (shift-click: remove from slot, then send), Deposit all (remove all eligible unlocked stacks, then send one Deposit).
- Search: a text box filters by localized name (case-insensitive, contains).
- Sort: **default = Count ↓ (highest first)**, ties broken by name ↑ so the order is stable (owner's request, 2026-11-12). The button toggles to **Name ↑** and back. The choice is remembered while the game runs.
- Refresh: a `List` request every 2 s while open.

### Task 5: Version 0.4.0, checklist (single-player regression + local dedicated server + client), docs, commit
