# Milestone 0: in-game test checklist

**Goal:** prove the mod loads, and that a drive-sized chunk of data survives being saved on an item.

**Takes about 10 minutes.** Use a **new, throwaway world**, not your real save.

## Before you start
- [ ] Claude has copied the mod into the game's Mods folder (Claude asks you first, and does it only while the game is closed).
- [ ] Launch the game **without EasyAntiCheat** (same as you already do for your other DLL mods).

## 1. Does the mod load?
- [ ] Start a **new world** (any map, Navezgane is fine).
- [ ] Press **F1** to open the console.
- [ ] Type `stashnet info` and press Enter.
- **Expected:** `[StashNet] StashNet v0.0.1 is loaded.`

## 2. Small drive (100 item types, like a High-Density drive)
- [ ] Hold any tool or weapon in your hand (e.g. the stone axe).
- [ ] In the console: `stashnet metatest 100`
- **Expected:** `Wrote fake drive: 100 item types, ... bytes`
- [ ] Right away: `stashnet metacheck`, which should say **PASS**.

## 3. Does it survive a save and reload?
- [ ] Press Esc, **Save and Exit** to the main menu.
- [ ] Load the same world again.
- [ ] Hold the **same** tool, open the console, run `stashnet metacheck`.
- **Expected:** **PASS**

## 4. Does it survive moving the item around?
- [ ] Put the tool in a storage box, take it back out, then hold it and run `stashnet metacheck`.
- [ ] Drop it on the ground, pick it back up, then hold it and run `stashnet metacheck`.
- **Expected:** **PASS** both times

## 5. Stress test (find the ceiling)
- [ ] Hold a **different** tool and run `stashnet metatest 1000`.
- [ ] Save and Exit, reload, hold it, run `stashnet metacheck`.
- **Expected:** PASS. If it fails, crashes, or the world won't load, tell Claude exactly what happened.

## Report back
Copy-paste the console lines, or just tell Claude PASS/FAIL for each step. If anything goes wrong, send the log file:
`%APPDATA%\7DaysToDie\logs\` (the newest file), or `7DaysToDie_Data\output_log*.txt` in the game folder.

Afterwards you can run `stashnet metaclear` on the test items, or just delete the throwaway world.
