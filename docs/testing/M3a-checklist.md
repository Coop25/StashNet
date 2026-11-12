# Milestone 3a: in-game test checklist

**Goal:** the Storage Terminal opens a screen showing everything on all drives in the network, and you can withdraw and deposit through it without losing anything.

**About 20 minutes.** Use your StashNet throwaway world, **single-player** (multiplayer clients come in 3b).

> This is the first custom screen, so **how it looks** matters too. Screenshot the terminal window even if everything works, so the layout can be tuned.

## Setup: build a small network
1. Place a **Scrap Storage Core**.
2. Within **6 blocks** of it, place **2 Drive Bays** and a **Storage Terminal**.
3. Put **2 Basic Drives** in the first bay and **1** in the second.
4. Put some items on one drive first (hold it and `stashnet drive deposit`), **or** leave them empty and use the terminal's deposit.

## 1. Loads cleanly
- [ ] F1: `[StashNet] Loaded v0.3.0 (6 methods patched)` (one more hook than before: shift-click deposit).
- [ ] No red errors mentioning StashNet, windows.xml, or xui.xml.

## 2. Opening the terminal
- [ ] Look at the terminal. The prompt says **"Storage Terminal / Press E to Use"**.
- [ ] Press **E**. The terminal window opens on the left, with your backpack on the right.
- [ ] The top line shows usage like `X/45 types  Y/4500 items  (3 drives)`.
- [ ] Any items already on drives show as icons with counts.
- [ ] **Hold E** on the terminal: the radial menu has **pick up**, and it works (then place it back).
- [ ] 📸 Screenshot of the open terminal.

## 3. Deposit
- [ ] **Shift-click** a stack in your backpack. It moves into the terminal list.
- [ ] Click the **Deposit all** icon (top-right of the terminal). Everything storable moves in. **Locked** backpack slots stay put, and drives in your backpack stay put.
- [ ] Deposit more than fits (3 Basic Drives = 45 types / 4,500 items). The leftovers stay in your backpack, with a "Storage full" message.

## 4. Withdraw
- [ ] **Left-click** an item: one full stack comes into your backpack.
- [ ] **Right-click** an item: exactly **one** comes out.
- [ ] Hovering over an item shows its **name** at the bottom.
- [ ] Withdraw a **weapon**: it keeps its quality, durability, and mods.
- [ ] Fill your backpack, then click an item: you get "Backpack full", and the counts in the terminal stay correct (nothing vanishes).

## 5. It's really one network
- [ ] Items deposited through the terminal end up on drives in **both** bays (check by taking a drive out, holding it, and running `stashnet drive`).
- [ ] Take a drive **out** of a bay, then reopen the terminal. Its items disappear from the list. Put it back, and they reappear.

## 6. Range and problems (clear messages, no errors)
- [ ] A terminal **more than 6 blocks** from any Core shows "No Storage Core in range".
- [ ] A Core with **no bays nearby** shows "No Drive Bays in range".
- [ ] Bays with **no drives** show "No drives in the Drive Bays".
- [ ] **Two networks:** place a second Core + bay + terminal ~10 blocks away. Each terminal only shows **its own** network's items.

## 7. Paging (if you have more than 48 types)
- [ ] With 49+ different item types, the arrows at the bottom flip pages, and the page number updates.

## 8. Other containers still normal
- [ ] Shift-click in your backpack while a **normal box** is open still moves items into the box (not into StashNet).

## Report back
PASS/FAIL per section + the screenshot. Logs: `%APPDATA%\7DaysToDie\logs\` (newest file).
