# Milestone 1: in-game test checklist

**Goal:** the four starter pieces exist, can be crafted and placed, and the Drive Bay only takes drives.

**About 15 minutes.** Use a **new throwaway world** (creative mode is fine: F1 → `cm`).

## 1. Mod loads cleanly
- [ ] Start a new world. Press F1 and run `stashnet info`. Expected: **v0.1.0**.
- [ ] Scroll up in the F1 console (or check the log) for the line `[StashNet] Loaded v0.1.0 (5 methods patched)`.
  - Report the number if it isn't **5**.
- [ ] Any **red errors** in the console mentioning StashNet, blocks.xml, items.xml, loot.xml, or recipes.xml? (Screenshot them.)

## 2. Crafting
- [ ] Open the crafting menu (backpack) and search "drive", "storage", "terminal". All four should show up:
  - Scrap Storage Core, Drive Bay, Storage Terminal, Basic Drive
- [ ] Icons show (tinted versions of vanilla icons) and names/descriptions read correctly (not raw keys like `stashnetDriveBay`).
- [ ] Craft at least one of each (or grab them from the creative menu).

## 3. Placing
- [ ] Place the **Core** (looks like an electrical box). Pick it back up (hold **E**).
- [ ] Place the **Terminal** (computer + monitor, 2 blocks wide). Pick it back up.
- [ ] Place the **Drive Bay** (server rack, 2 blocks tall). Does it look right, not floating or sunk into the floor?

## 4. Drive Bay: drives only (the important part)
Have some normal items in your backpack (wood, stone, a weapon) **and** 2–5 Basic Drives.
- [ ] Open the Drive Bay. It has **4 slots**.
- [ ] **Drag** a Basic Drive into a slot. ✅ Should work.
- [ ] **Drag** wood into a slot. ❌ Should be refused.
- [ ] **Shift-click** wood from your backpack. ❌ Should stay in your backpack.
- [ ] **Right-click-drag** (drop one) wood onto an empty slot. ❌ Should be refused.
- [ ] Use the container's **stash buttons** (stash all / fill / smart). Only drives should move, never wood or weapons.
- [ ] Take drives back out (drag, shift-click). ✅ Should work.

## 5. Other containers are unaffected
- [ ] Open a normal storage box (or one from your other storage mods) and put wood in it. ✅ Should work as usual.
- [ ] If you have a vehicle handy: its storage still accepts anything.

## 6. Lock
- [ ] Look at the Drive Bay and hold **E**. There should be a **Lock / Set Code** option like a vanilla crate. Set a code, lock it.

## 7. Destroying a Drive Bay
- [ ] Put 2 drives in a Drive Bay, then destroy it (`dm` debug mode makes this quick, or just whack it).
- [ ] A **loot bag** should drop containing the drives. Pick them up.

## Report back
PASS/FAIL per section is enough. For anything odd, a screenshot or the newest log from `%APPDATA%\7DaysToDie\logs\`.
