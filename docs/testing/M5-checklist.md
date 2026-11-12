# Milestone 5: in-game test checklist (drive unload)

**Goal:** shift-clicking a drive in your backpack while a Terminal is open empties it into the network. Whatever doesn't fit stays on the drive.

**About 10 minutes**, single-player in your throwaway world.

## 0. Loads cleanly
- [ ] F1: `[StashNet] Loaded v0.6.0 (6 methods patched)`, and no red errors mentioning StashNet.
- [ ] (Also confirms v0.5.1's lock changes load fine, since that version was never launched.)

## 1. Unload a drive
- [ ] Put items on a spare Basic Drive (e.g. with `stashnet drive deposit` while holding it), then put the drive in your **backpack**.
- [ ] Open a Terminal on a network with free space. **Shift-click the drive.**
- [ ] **Expected:** "Unloaded N items from the drive." The items show in the Terminal list, and the drive stays in your backpack.
- [ ] Hold the drive and run `stashnet drive`. It's empty.

## 2. Network fills up mid-unload
- [ ] Fill the network until only a stack or two is free.
- [ ] Shift-click a drive holding more than that.
- [ ] **Expected:** part of it moves in, with "Storage full - the rest stayed on the drive." `stashnet drive` on that drive shows the leftovers with the correct counts.

## 3. Weapons survive
- [ ] Put a weapon with mods/wear on a drive, then unload the drive. Withdraw the weapon from the Terminal. Quality, durability, and mods are intact.

## 4. Edge cases
- [ ] Shift-click an **empty** drive: "That drive is empty." Nothing happens.
- [ ] Shift-click a drive on your **toolbelt**: it unloads too.
- [ ] **Deposit all** (top-right button) still never moves drives.

## Report back
PASS/FAIL per section. Logs: `%APPDATA%\7DaysToDie\logs\`.
