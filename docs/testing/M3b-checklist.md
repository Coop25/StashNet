# Milestone 3b: in-game test checklist

**Goal:** the Terminal works in multiplayer without duplicating or losing items, and gains search, sort, and live refresh.

Part A is single-player (about 10 minutes). Part B needs multiplayer: a friend joining your game, **or** a dedicated server on your own PC that you join (Claude sets that up with you first).

---

## Part A: single-player (your throwaway world)

### A1. Loads cleanly
- [ ] F1: `[StashNet] Loaded v0.4.0 (6 methods patched)`.
- [ ] No red errors mentioning StashNet.

### A2. Everything from 3a still works
- [ ] Open the terminal. The list appears (it may say "Connecting to storage..." for a split second).
- [ ] Left-click = a stack, right-click = one, shift-click deposits, Deposit-all works, a full backpack keeps leftovers in storage.

### A3. Sort
- [ ] On open, the list is sorted **most first** (the biggest stack is top-left).
- [ ] Click the **Sort** button: the list switches to **Name** order. Click again: back to most first.
- [ ] Close and reopen the terminal. It remembers your sort choice.

### A4. Search
- [ ] Type part of an item name (e.g. `wood`) in the search box. Only matching items show.
- [ ] Clear the box. Everything shows again.
- [ ] While typing, your character doesn't move or open other menus.

### A5. Live refresh
- [ ] Keep the terminal open, and from **another** terminal or `stashnet drive` commands... (easier: in Part B with a second player). Skip in single-player if awkward.

### A6. Layout
- [ ] 📸 Screenshot the open terminal with some items in it. Does anything overlap, or is anything cut off?

---

## Part B: multiplayer

Use **one** of these setups:
- **Friend:** you host your throwaway world (with "allow joining" on) and a friend with the same StashNet version joins. **They** do the client tests.
- **Solo:** a dedicated server on your PC, and you join it from the game. (Ask Claude to set this up first. It needs a small server config file with anti-cheat off.)

### B1. Client can use the terminal
- [ ] The joined player opens a terminal. The list appears (no "not supported" message anymore).
- [ ] The joined player withdraws (stack + single), shift-click deposits, and uses Deposit-all. All work.

### B2. No duplication
- [ ] Put exactly **10 nails** (or any small amount) in storage.
- [ ] Both players open terminals on the same network and click the nails at the same moment.
- [ ] **Expected:** 10 nails in total across both backpacks, never more. Storage shows 0.

### B3. Nothing lost on a full backpack (client)
- [ ] The joined player fills their backpack, leaving 1 free slot, and withdraws something with several stacks.
- [ ] **Expected:** one slot fills, "Backpack full" shows, and the rest stays in storage (check that the count adds up).

### B4. Live refresh
- [ ] Player 1 keeps the terminal open. Player 2 deposits something on the same network.
- [ ] **Expected:** within ~2 seconds, player 1's list shows it without reopening.

### B5. Too far away
- [ ] (Optional) The terminal stays open while you walk more than ~8 blocks away and click an item. You get "You're too far from the terminal", and nothing moves.

### B6. Dedicated server only
- [ ] The server's log/console shows `[StashNet] Loaded v0.4.0` with no StashNet errors.

## Report back
PASS/FAIL per section + the screenshot. Client log: `%APPDATA%\7DaysToDie\logs\`.
