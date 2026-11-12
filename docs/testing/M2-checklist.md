# Milestone 2: in-game test checklist

**Goal:** drives really hold items, within their capacity, with nothing lost, and keep them through saves and moves.

**About 15–20 minutes.** Use your StashNet throwaway world. Creative mode is fine.

There's no Terminal screen yet (that's Milestone 3), so you'll use console commands **while holding a drive**:
put a Basic Drive on your toolbelt, select it, press **F1**.

| Command | What it does |
|---|---|
| `stashnet drive` | show what's on the held drive |
| `stashnet drive deposit` | move your **backpack** contents onto the drive (as much as fits) |
| `stashnet drive withdraw` | move the drive's contents into your backpack (as much as fits) |
| `stashnet drive wipe` | erase the drive (testing only) |

Tip: `stashnet drive` with no extra word = show.

## 1. Basics
- [ ] Startup line in F1: `[StashNet] Loaded v0.2.0 (5 methods patched)`.
- [ ] Hold a new Basic Drive and run `stashnet drive`. Expected: `0/15 types, 0/1500 items`.
- [ ] Put a few stacks in your backpack (e.g. 300 wood, 200 stone, 50 nails). Run `stashnet drive deposit`.
  - Expected: they leave your backpack, and `stashnet drive` lists them with correct counts.
- [ ] Run `stashnet drive withdraw`. Everything comes back into your backpack, and the drive shows 0 again.

## 2. Capacity: 1,500 items
- [ ] Put **2,000 wood** in your backpack (`giveself resourceWood` a few times, or the creative menu). Deposit.
- **Expected:** exactly 1,500 go onto the drive and 500 stay in your backpack. `stashnet drive` shows `1500/1500 items`.

## 3. Capacity: 15 types
- [ ] Wipe the drive. Put **16+ different items** in your backpack, 1 of each (various resources, food, ammo, etc.). Deposit.
- **Expected:** 15 go onto the drive, and the rest stay in your backpack. `15/15 types`.
- [ ] Add more of a type that's **already on the drive** and deposit again. That should still work, up to the item limit.

## 4. Weapons and tools keep their stats
- [ ] Get a weapon or tool with **quality**, some **wear** (use it a bit), and ideally an **attached mod** (e.g. a scope on a gun).
- [ ] Note its quality, durability, and mods. Deposit it. `stashnet drive` should show `[quality X, durability Y/Z, N mod(s)]`.
- [ ] Withdraw it. **Expected:** identical quality, durability, and mods, and the mod is still attached.
- [ ] Bonus: a gun with ammo loaded keeps its loaded ammo.

## 5. Backpack full on withdraw (nothing vanishes)
- [ ] Deposit a lot (e.g. 1,000 wood + some other stuff). Then **fill your backpack** with junk so only 1–2 slots are free.
- [ ] Withdraw. **Expected:** it fills the free slots and says `Your backpack is full - the rest stayed on the drive`. `stashnet drive` shows the rest still there, and the counts add up.

## 6. Contents survive moving and saving
Put something recognizable on a drive first (e.g. 123 wood + a weapon).
- [ ] Put the drive into a **Drive Bay**, take it out, hold it, and run `stashnet drive`. Contents should be intact.
- [ ] Put the drive into a **normal storage box**, take it out, check again.
- [ ] Drop it on the ground, pick it up, check again.
- [ ] **Save and Exit**, reload the world, hold it, check again.

## 7. Drives don't go inside drives
- [ ] Have a second drive in your backpack and deposit. The second drive should **stay in your backpack**.

## Report back
PASS/FAIL per section. For anything off, a screenshot of the F1 output, or the newest log from
`G:\SteamLibrary\steamapps\common\7 Days To Die\7DaysToDie_Data\output_log_client__*.txt`.
