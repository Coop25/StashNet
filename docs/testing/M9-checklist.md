# Milestone 9: multiplayer test checklist

**Goal:** everything StashNet does also works when you're a player **joined to a server**, not the host. Nothing duplicates, and nothing is lost. Up to now every test ran in single-player.

There are two parts:
- **Part A (solo, ~45 min):** run a dedicated server on your own PC and join it. You're a joined player, just like a friend would be.
- **Part B (needs a friend):** the things only two real people can test. Do them whenever someone can join.

---

## Setup (once)

1. **Install the server.** In Steam: Library → change the filter from "Games" to "Tools" → install **7 Days to Die Dedicated Server** (free). It installs next to the game, in `steamapps\common\7 Days to Die Dedicated Server`.
2. **Give it the same mods as your game.** Copy everything in your game's `Mods` folder into the server's `Mods` folder (including `StashNet`). Your game and the server must run the same mods, **and the exact same StashNet version**, or joining fails or acts strangely. Friends install StashNet from the release zip of that same version.
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
- [ ] The **server window** shows `[StashNet] Loaded v<version> (33 methods patched)` (the version you installed; 33 as of v0.17.0) and no red errors mentioning StashNet.
- [ ] Your **game** (F1) shows the same line.

### A1. Terminal as a joined player
Build a small network: Scrap Core, a Drive Bay with a couple of 48-Stack drives, a Storage Terminal.
- [ ] Open the terminal. The list shows (no "not supported" message). The window reaches the bottom of your backpack and shows 11 rows.
- [ ] Withdraw one item (click) and a stack (shift-click). Right-click also takes one.
- [ ] Shift-click a backpack stack to deposit it.
- [ ] **Drop onto the terminal:** pick up a backpack stack so it's on your cursor, click it onto the terminal's items: the whole stack goes in. Right-click instead puts in just one and keeps the rest on your cursor.
- [ ] **Backpack buttons:** "move all" (top right of the backpack) stores every unlocked stack; "fill and smart" stores only items storage already has. Locked slots and drives stay put.
- [ ] **Dragging** a stack around your backpack with the terminal open works (it follows the cursor).
- [ ] **Tabs:** only All plus categories you have stored show. Store something from a new category: its tab appears within ~2 s. Take the last of a category out: its tab goes away.
- [ ] **Mouse wheel** over the items turns pages (store enough kinds of items to get 2 pages, or use a small search).
- [ ] Fill your backpack, leaving 1 free slot, and withdraw something with several stacks. One slot fills, "Backpack full" shows, and the rest stays in storage. The numbers add up.
- [ ] Storage nearly full: drop or move-all more than fits. What doesn't fit comes back to your backpack; nothing disappears.
- [ ] (Optional) Keep the terminal open, walk more than ~8 blocks away, and click an item: "You're too far from the terminal", and nothing moves.

### A2. Code lock
- [ ] Set a code on the Core. Close and reopen the terminal: you still get in (you're the owner).
- [ ] Change the code. You still get in (owner).

### A3. Drive Bays
- [ ] Put drives in and take them out of a bay. Only drives go in. Try putting wood in: it's refused.
- [ ] The terminal shows the right items after moving drives around.
- [ ] **Fill bars:** drives in the bay and in your backpack show a fill bar and a percent in the middle (green, yellow from 75%, red when full); empty drives show neither. Selecting a drive shows "N / M stacks used (P%) · T different items" in its description.

### A4. Unload, scrap and sell
- [ ] Take out a drive with items on it. **Scrap** is refused with "Unload this drive first".
- [ ] At a **trader**, try to sell that drive: refused with "unload it ... before selling it".
- [ ] Open a terminal and shift-click that drive: its items move into the network, and the drive is now empty (its bar and percent disappear).
- [ ] Scrap the empty drive: it works. (Or sell it: it sells.)

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
- [ ] **Drive upgrade:** with 3 empty 48-Stack drives and the parts in your backpack, craft a 192-Stack Drive at a workbench (or the Crafting Terminal): it works and uses exactly 3 drives. Put it in a bay: the stack total grows by 192.
- [ ] With 1 full + 2 empty 48-Stack drives, the 192-Stack recipe isn't craftable and Craft is refused; the full drive keeps its items.
- [ ] **New looks:** a Drive Bay looks like a tall file cabinet, Reinforced like a server rack, Armored like a dark gun safe. A Scrap Core looks like an electrical box, a Storage Core like a control panel with a sloped top, an Advanced Core like a control panel. Each sits flat on the floor (not floating or sunk in), you can't walk through it, and upgrading swaps the look in place. Their icons match.
- [ ] **Drive names and icons:** five drives, 48-Stack to 5184-Stack Drive, each with its worn hard-drive icon (blue / green / purple / orange / red tape with 48 / 192 / 576 / 1728 / 5184) in your backpack, in a Drive Bay, and in the creative menu. None shows a blank or pink square, and nothing says "1K"/"4K".

### A7. Core spacing (passed in single-player; check it again as a joined player)
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

### B6. Depositing at the same time
- [ ] Storage nearly full (a few free stacks). You both press the backpack's "move all" (or drop stacks onto your terminals) at the same moment. Afterwards: storage is full, everything that didn't fit is back in your backpacks, and the totals add up (nothing duplicated, nothing gone).

### B7. Your friend sees the same drives
- [ ] Your friend opens your Drive Bay (with the code): the drives show the same fill bars and percents you see.
- [ ] Your friend withdraws from your network while you watch your tabs: when they take the last item of a category, the tab disappears on your screen within ~2 s.

---

When done, send me what passed or failed, plus the **server's log**: the newest `output_log` file in the server folder's `7DaysToDieServer_Data`. I'll also check your game's log.
