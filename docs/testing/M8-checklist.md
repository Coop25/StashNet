# Milestone 8: in-game test checklist (Progression)

**Goal:** bigger drives, tougher bays that hold more drives, better Cores that reach farther, and a hammer upgrade that never loses anything.

**About 30–40 minutes**, single-player in your throwaway world. Creative mode or `giveself` is fine for materials. `dm` (debug mode) makes damage tests quick.

Handy commands (F1 console):
- `giveself stashnetDriveStandard`, `giveself stashnetDriveHighDensity`
- `giveself stashnetDriveBayReinforced`, `giveself stashnetDriveBayArmored`
- `giveself stashnetStorageCore`, `giveself stashnetAdvancedCore`
- `giveself meleeToolRepairT1ClawHammer` (any repair tool works: stone axe, claw hammer, nail gun)
- `sn heat` (stand next to a Core)

**How upgrading works:** hold a hammer, have the **next tier's block** in your backpack or toolbelt, and keep hitting the block, the same as upgrading wood to cobblestone. After a few hits it turns into the next tier and the block from your inventory is used up.

## 0. Loads cleanly
- [ ] F1: `[StashNet] Loaded v0.9.0 (20 methods patched)`, and no red errors mentioning StashNet, blocks.xml, items.xml or recipes.xml.
- [ ] Your existing network (Scrap Core, bays, Basic drives) still works: the terminal shows your items and asks for the code if it's locked.

## 1. Upgrades keep everything (most important, do this first)
- [ ] Put **4 drives with items on them** in a Drive Bay. Note what the terminal shows.
- [ ] Hammer a **Reinforced Drive Bay** onto it. It turns into a Reinforced Drive Bay. Open it: all 4 drives are still there, and there are **8 slots**. The terminal shows the same items.
- [ ] Hammer an **Armored Drive Bay** onto it: still the same 4 drives, now **12 slots**.
- [ ] Set a code on a **Scrap Core** (if it doesn't have one). Hammer a **Storage Core** onto it. It turns into a Storage Core. Looking at a terminal or bay still says the network is locked if you weren't allowed. If you were allowed before, you still are. The terminal still works.
- [ ] Place a terminal **10 blocks** from the Core. Before the upgrade it says "No Storage Core in range"; after the upgrade to Storage Core it connects.
- [ ] Hammer an **Advanced Storage Core** onto the Storage Core: the code is still there, and a terminal **20 blocks** away connects.
- [ ] Hitting an **Armored** bay or an **Advanced** Core with a hammer does **not** upgrade it (top tier).

## 2. New drives
- [ ] At a **workbench**: the Standard Drive and High-Density Drive recipes show up (forged iron / forged steel, electrical parts, mechanical parts). Craft one of each (or `giveself`).
- [ ] Hover each drive: the description says how many stacks it holds and ends with "Shift-click it while a Storage Terminal is open...". The Basic Drive's description now says that too.
- [ ] Put both in a bay. The terminal's stack total goes up by **96** and **384**.

## 3. Moving up a drive tier (unload + scrap)
- [ ] Take a **Basic drive that has items on it** out of a bay. In your backpack, click it → **Scrap**. It's **refused** with "Unload this drive first: shift-click it in a Storage Terminal." Nothing is lost (check with the terminal after putting it back, or step 3).
- [ ] Open a Storage Terminal and **shift-click** that drive: its items move into the network.
- [ ] Now **Scrap** the empty drive: it works, and you get electrical parts back.
- [ ] (Optional) Same for a Standard drive: refused while full, scraps once empty.

## 4. Armored Bay toughness
- [ ] Put drives in an **Armored** bay (12 if you can). Hit it the way a zombie would, or shoot it. It takes far longer than a plain Drive Bay (10,000 health vs 1,000).
- [ ] Finish destroying it: a **loot bag** drops with **all its drives**. Put them in another bay: their items are all there.

## 5. Two Cores, each bay counted once
- [ ] Place two Cores about **8 blocks apart**, with reaches that overlap (Scrap Cores reach 6, so put bays in the overlap). Put drives in bays on both sides and in the middle.
- [ ] Stand at each Core and run `sn heat`. Every bay's drives count toward **exactly one** Core (the nearer one). The two drive counts add up to the total number of drives.
- [ ] Open a terminal near each Core: each one shows only its own bays' items.
- [ ] Lock **one** of the two Cores with a code. Only the bays that Core owns ask for its code; the other Core's bays open freely.
- [ ] Place a terminal next to a Core whose nearby bays all belong to the other Core: it says "N Drive Bay(s) in range belong to a nearer Storage Core" (not "No Drive Bays").

## 6. Chunk edges
- [ ] Build a small network across a **chunk border** (chunks are 16×16; F3 shows your chunk). Put the Core on one side and bays on both sides. Everything shows up in the terminal.
- [ ] Walk far away (a few hundred blocks), come back, and use the terminal: no errors.

## 7. Big network feels fast
- [ ] On an **Advanced** Core with several bays, click through the terminal (search, take, put) and use the Crafting Terminal. It feels as instant as before.

## 8. Heat counts drive size
- [ ] Stand at a Core and run `sn heat`. The line shows a **heat weight**: each Basic drive adds 1, each Standard 2, each High-Density 4.
- [ ] Example: 12 Basic drives on a Scrap Core gives `heat weight 12 -> 3 heat per report` (same as M7). Swap one Basic for a High-Density: the weight goes to 15.

## 9. Save and reload
- [ ] Save, quit to the menu, reload. Upgraded bays and Cores are still their new tier, with the same drives and code. `sn heat` shows the same numbers.

When done, send me the results (and I'll check the log).
