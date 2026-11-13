# Milestone 9: multiplayer test checklist

**Goal:** everything StashNet does also works when you're a player **joined to a server**, not the host. Nothing duplicates, and nothing is lost. Up to now every test ran in single-player.

There are two parts:
- **Part A (solo, ~45 min):** run a dedicated server on your own PC and join it. You're a joined player, just like a friend would be.
- **Part B (needs a friend):** the things only two real people can test. Do them whenever someone can join.

---

## Setup (once)

1. **Install the server.** In Steam: Library → change the filter from "Games" to "Tools" → install **7 Days to Die Dedicated Server** (free). It installs next to the game, in `steamapps\common\7 Days to Die Dedicated Server`.
2. **Give it the same mods as your game.** Copy everything in your game's `Mods` folder into the server's `Mods` folder (including `StashNet`). Your game and the server must run the same mods, or joining fails or acts strangely.
3. **Edit the server's `serverconfig.xml`** (in the server folder) with Notepad:
   - `EACEnabled` → `false`
   - `ServerVisibility` → `0` (not listed publicly)
   - `GameName` → something new, like `StashNetTest` (a fresh test world)
   - Leave the rest alone.
4. **Start it:** double-click `startdedicated.bat` in the server folder. Wait until the window stops scrolling (a minute or two the first time, while it generates the world).
5. **Join it:** start the game with EasyAntiCheat off → Join a Game → Connect to IP → `127.0.0.1`, port `26900`.
6. Once you're in, give yourself admin so `giveself`, `cm` and `dm` work. In the server window, type `admin add <your name> 0` (or use the server's web panel). Ask me if this step gives you trouble.

---

## Part A: solo on your own server

### A0. Loads cleanly
- [ ] The **server window** shows `[StashNet] Loaded v0.9.1 (20 methods patched)` and no red errors mentioning StashNet.
- [ ] Your **game** (F1) shows the same line.

### A1. Terminal as a joined player
Build a small network: Scrap Core, a Drive Bay with a couple of Basic drives, a Storage Terminal.
- [ ] Open the terminal. The list shows (no "not supported" message).
- [ ] Withdraw a stack (left-click) and a single item (right-click). Shift-click to deposit. Use Deposit-all. All of it works.
- [ ] Fill your backpack, leaving 1 free slot, and withdraw something with several stacks. One slot fills, "Backpack full" shows, and the rest stays in storage. The numbers add up.
- [ ] (Optional) Keep the terminal open, walk more than ~8 blocks away, and click an item: "You're too far from the terminal", and nothing moves.

### A2. Code lock
- [ ] Set a code on the Core. Close and reopen the terminal: you still get in (you're the owner).
- [ ] Change the code. You still get in (owner).

### A3. Drive Bays
- [ ] Put drives in and take them out of a bay. Only drives go in. Try putting wood in: it's refused.
- [ ] The terminal shows the right items after moving drives around.

### A4. Unload and scrap
- [ ] Take out a drive with items on it. **Scrap** is refused with "Unload this drive first".
- [ ] Open a terminal and shift-click that drive: its items move into the network, and the drive is now empty.
- [ ] Scrap the empty drive: it works.

### A5. Crafting Terminal as a joined player
Place a Crafting Terminal near the Core and put some ingredients in storage (not in your backpack).
- [ ] Open it. Recipes that need stored ingredients show as craftable.
- [ ] Click **Craft**. As a joined player it may say "Fetching ingredients from storage... click Craft again". Click again, and the craft starts. (This was expected in the design. Tell me if it happens every time and feels bad.)
- [ ] The craft-count arrows never let you queue more than fits in your backpack.
- [ ] Cancel a queued craft: the ingredients go back into **storage**.
- [ ] Let a craft finish **while the window is open**: the result goes into storage.
- [ ] Queue a craft, **close the window**, and wait: the result goes into storage within a few seconds.

### A6. Upgrades and tiers on the server
- [ ] Hammer a Reinforced Drive Bay onto a bay with drives in it: the drives stay, and there are 8 slots.
- [ ] With a code set, hammer a Storage Core onto the Scrap Core: the code stays, and the reach grows (a terminal 10 blocks away connects).
- [ ] Craft a Standard drive at a workbench and put it in a bay: the stack total grows by 96.

### A7. Core spacing (new in v0.9.1, never tested yet)
- [ ] Try to place another Core **30 blocks** from yours: refused, with the "can't build" sound and "Another Storage Core is only 30 blocks away...".
- [ ] Try **20 blocks straight up**: refused.
- [ ] **50 blocks** away: allowed.
- [ ] Upgrading a Core in place still works.

### A8. Restart
- [ ] Note what's in storage. Stop the server (type `shutdown` in the server window), start it again, and rejoin. Everything is still there, upgraded blocks are still upgraded, and the code still works.

### Known gap
- `sn heat` doesn't work for a joined player on a dedicated server yet. It only works for the host or in single-player. Heat itself still runs on the server. Skip the heat checks here.

---

## Part B: needs a friend

Your friend needs the same mods, StashNet included, with EasyAntiCheat off. They join your dedicated server (your PC's address) or your hosted game.

### B1. No duplication (most important)
- [ ] Put exactly **10 nails** in storage.
- [ ] Both of you open terminals on the same network and click the nails **at the same moment**.
- [ ] **Expected:** 10 nails in total across both backpacks, never more. Storage shows 0.

### B2. Live refresh
- [ ] You keep the terminal open while your friend deposits something on the same network. Within ~2 seconds, your list shows it without reopening.

### B3. Locked out
- [ ] With a code on your Core, your friend looks at your bay or terminal and sees "Network locked". Pressing E opens the keypad.
- [ ] A wrong code keeps them out. The right code lets them in.
- [ ] You change the code: they're locked out again until they enter the new one.

### B4. Two players crafting
- [ ] You both use Crafting Terminals on the same network at the same time with the same ingredients. Nobody ends up with free ingredients, and storage never goes negative.

### B5. Spacing between two players
- [ ] Your friend can't place a Core within 48 blocks of yours, and vice versa.

---

When done, send me what passed or failed, plus the **server's log**: the newest `output_log` file in the server folder's `7DaysToDieServer_Data`. I'll also check your game's log.
