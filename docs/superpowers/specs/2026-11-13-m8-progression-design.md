# Milestone 8: Progression (design)

*2026-11-13. Agreed with the owner in conversation; numbers are placeholders for playtesting.*

## Intent
Grow the network from a crude first-week setup into a bigger, tougher, late-game system (`docs/PLAN.md` 2.2–2.4). The owner's guiding goal: **StashNet should help without being overpowered.** For M8, balance comes from **prices** (higher tiers cost real mid/late-game materials) and **heat** (bigger drives run hotter). Every number lives in the config XML so it can be tuned after playing.

## Goals
1. Two new drives: **Standard** (96 stacks) and **High-Density** (384 stacks), crafted new at a workbench.
2. Every drive's description says that shift-clicking it in a Storage Terminal empties it into the network.
3. A drive that still holds items **can't be scrapped**. Empty drives scrap normally.
4. Two new Drive Bay tiers, **Reinforced** and **Armored**. They hold more drives and are tougher. You upgrade a bay in place with a hammer, and its drives stay inside.
5. Two new Core tiers, **Storage Core** and **Advanced Storage Core**. They reach farther, run cooler per drive, and are tougher. You upgrade a Core in place with a hammer, and its code, allowed players and owner carry over.
6. Heat counts **drive size**: bigger drives add more heat.
7. **Each bay belongs to one Core only**, its nearest one. This fixes M7's double-counted heat.
8. Finding Cores and bays stays fast at reach 24.

## Non-goals
- **No drive upgrade kits** (owner, 2026-11-13). To move up a drive tier, craft the bigger drive, shift-click the old one in a terminal to unload it, then scrap the old one.
- **No downgrade on break** (owner). An Armored Bay is just tougher. When it breaks, it breaks completely and drops its drives in a loot bag, as bays do today.
- **No fuel or electricity running cost.** The owner raised fuel as a balance idea. It's parked until M8 has been played; both fuel and power go in the plan's "Later" list.
- No new art: same models, new tints.
- Selling a full drive to a trader still destroys its contents. That's a deliberate player action, and it's out of scope here. Noted for M9.

## Anti-goals (failure even if it "works")
- Any path in this milestone that deletes drive contents: upgrading a bay, upgrading a Core, scrapping, or a bay switching networks.
- Terminal clicks that lag at reach 24.
- Breaking the owner's existing test world (Scrap Cores, Drive Bays and Basic Drives already placed).

## Constraints and findings (V3.4 b10, decompiled)
- **A hammer upgrade replaces the block, and vanilla carries the tile entity over.** `GameManager.ChangeBlocks` calls `newTE.UpgradeDowngradeFrom(oldTE)`. For composite blocks this copies the **owner**, `TEFeatureStorage` (the item grid is copied, then resized to the new block's container size) and `TEFeatureLockable` (locked flag, allowed users, password hash). Our own features (`TEFeatureStashNetDriveBay`, `TEFeatureStashNetHeat`) hold no state. If the new block had no storage, vanilla would drop the contents (`TEFeatureStorage.ReplacedBy`); every tier keeps storage. **This has never run in-game for our blocks: it's checklist step 1.**
- **A hammer upgrade costs exactly one item type** (`UpgradeBlock` → `ToBlock`, `Item`, `ItemCount`, `UpgradeHitCount`). The upgrade item may be a block; vanilla iron doors upgrade with `vaultDoor01` this way.
- **Repair tools have an allowlist** (`Allowed_upgrade_items` on the stone axe, claw hammer and nail gun, matched as a case-insensitive substring of the comma list). Our upgrade blocks must be appended to it.
- Today's discovery checks every block in a cube: (2r+1)³ = 2,197 blocks at reach 6 but 117,649 at reach 24, done twice per request (find the Core, then find its bays).
- Existing conventions apply: names start with `stashnet`; code checks the `stashnetDrive` tag and the Core's `StashNetRadius` property, never names; game-free logic goes in `StashNet.Core` with tests.

## Approaches
**Bay and Core tiers.**
1. **Picked: the vanilla hammer upgrade, using the next-tier block as the upgrade item** (owner: "the upgrade just takes the next tier of machine"). You craft a Storage Core and hammer it onto a Scrap Core. The data carries over through vanilla code, and higher tiers can also be placed directly.
2. Crafted upgrade parts with mixed ingredients, hammered on. Owner preferred 1.
3. One raw material per step (for example 10 forged steel). Owner: too cheap for what it gives.
4. Break the old block and place the new one. Rejected: it loses the code and allowed list, and every drive has to be moved by hand.

**Drive tiers.**
1. **Picked (owner): craft the bigger drive, unload the old one, scrap it.**
2. Upgrade kits (drag onto a drive, or apply in a terminal). Dropped by the owner.
3. A recipe of drive + kit. Rejected: crafting doesn't carry item data, and cancelling a queued craft refunds a blank drive.

**Balance.**
1. **Picked: prices plus size-weighted heat.**
2. Fuel burned per use. 3. Electricity. Both are parked until after playtesting.

## Design

### Drives (`items.xml`)
| Item | Stacks | Heat weight | Recipe | Scrap weight |
|---|---|---|---|---|
| Basic Drive `stashnetDriveBasic` | 24 | 1 | unchanged (backpack) | 2 (unchanged) |
| Standard Drive `stashnetDriveStandard` | 96 | 2 | workbench: 5 forged iron, 8 electrical parts, 2 mechanical parts | 4 |
| High-Density Drive `stashnetDriveHighDensity` | 384 | 4 | workbench: 5 forged steel, 15 electrical parts, 4 mechanical parts | 8 |

- New tiers copy the Basic Drive's properties (tag `stashnetDrive`, `Stacknumber 1`, `Material MelectricParts`, sack model) with their own tint and description. There's no perk or book lock; the workbench and the materials are the gate.
- **New item property `StashNetHeatWeight`** (missing means 1).
- **Descriptions:** each states its stack count and ends with *"Shift-click it while a Storage Terminal is open to empty its contents into the network."*
- **Scrap guard:** the Scrap action (`ItemActionEntryScrap`, used by both the backpack and the workbench windows) refuses a drive that holds items. It shows *"Unload this drive first: shift-click it in a Storage Terminal."* A drive "holds items" if it has `StashNetDrive` data that doesn't read as empty. **Unreadable data counts as holding items.**

### Drive Bays (`blocks.xml`, `loot.xml`)
| Block | Slots (loot grid) | MaxDamage | Recipe | Hammer upgrade |
|---|---|---|---|---|
| Drive Bay `stashnetDriveBay` | 4 (4×1) | 1,000 | unchanged | + 1 Reinforced Drive Bay → Reinforced |
| Reinforced Drive Bay `stashnetDriveBayReinforced` | 8 (4×2) | 5,000 (≈ concrete) | workbench: 30 forged iron, 8 mechanical parts, 4 electrical parts | + 1 Armored Drive Bay → Armored |
| Armored Drive Bay `stashnetDriveBayArmored` | 12 (4×3) | 10,000 (≈ steel, **hard cap**) | workbench: 25 forged steel, 10 mechanical parts, 8 electrical parts | none |

- Same model, size (1×2×1) and feature list as the Drive Bay (`TEFeatureStashNetDriveBay` first, then storage, then the legacy lock), each with its own loot container and tint. No `DowngradeBlock`.
- The drives-only hooks and the lock checks already key on `TEFeatureStashNetDriveBay`, so they cover every tier.

### Cores (`blocks.xml`)
| Block | Reach | Heat per weight | Cap | MaxDamage | Recipe | Hammer upgrade |
|---|---|---|---|---|---|---|
| Scrap Storage Core `stashnetScrapCore` | 6 | 0.25 | 6.5 | 1,000 | unchanged | + 1 Storage Core → Storage Core |
| Storage Core `stashnetStorageCore` | 12 | 0.18 | 6.5 | 5,000 | workbench: 40 forged iron, 15 electrical parts, 10 mechanical parts, 1 radiator | + 1 Advanced Storage Core → Advanced |
| Advanced Storage Core `stashnetAdvancedCore` | 24 | 0.12 | 6.5 | 10,000 | workbench: 30 forged steel, 25 electrical parts, 10 mechanical parts, 2 radiators | none |

- Same model and features as the Scrap Core (Lockable, Heat, Pickup), each with its own tint. Picking a Core up returns its own tier.
- **Upgrade allowlist:** append `stashnetDriveBayReinforced`, `stashnetDriveBayArmored`, `stashnetStorageCore` and `stashnetAdvancedCore` to every `Allowed_upgrade_items` value (an XPath append on the attribute), so other mods' tools work too.

### Heat
- `heat per report = min(total heat weight of the network's drives × Core's StashNetHeatPerDrive, Core's StashNetHeatCap)`. Unreadable drives still count, since their weight comes from the item class.
- Game-free formula in `StashNet.Core` (`NetworkHeat`), with unit tests. `sn heat` shows the drive count, the total weight and the result.
- Examples: 12 Basic drives on a Scrap Core = 3 (the M7 target "B" is unchanged). 16 Standard drives on a Storage Core = 5.76. An Advanced Core hits the cap at a total weight of 54 (about 14 High-Density drives).

### Which Core a bay belongs to
- **Rule:** a bay or terminal belongs to the **nearest Core (cube distance) whose reach covers it**. Ties go to a fixed position order, so every lookup agrees. This is the rule `TryFindCore` already applies to terminals and to the lock checks (`CoreLookupCache`), so a bay is now protected by the same Core whose network uses it.
- `TryOpen` includes a bay only if that bay's Core is this network's Core. The nearest-Core choice is a game-free function in `StashNet.Core` with tests (distances, reach, ties).
- A bay can switch networks when a nearer Core is placed or a Core is upgraded. Its drives stay in the bay; only which terminal shows them changes.

### Faster discovery
Replace the block-by-block cube scans with a walk over the **tile entities** in the chunks that overlap the cube. Cores and bays are both tile entities. Collect the candidate Cores once per `TryOpen`, then assign each bay in memory. `CoreLookupCache` stays.

### Text
- New names and descriptions for the six new blocks and items.
- Replace the hard-coded "6 blocks" in the Scrap Core description, `stashnetNoCore` and the Crafting Terminal description with wording that doesn't assume a reach. Each Core's description states its own reach.

### Compatibility
- Existing Scrap Cores, Drive Bays and Basic Drives only gain properties (`UpgradeBlock`). No feature changes on existing blocks, and no change to drive data. Old worlds load as before.
- Version **0.9.0**.

## Testing
- **Unit tests** (`dotnet test`): the weighted heat formula (weights, cap, bad inputs) and the nearest-Core picker (in range or not, nearest wins, ties, several Cores).
- **In-game checklist** `docs/testing/M8-checklist.md`, in this order:
  1. **Upgrade carry-over first:** a Drive Bay with 4 drives, upgraded to Reinforced, keeps the drives and shows 8 slots. A Core with a code, upgraded, keeps the code and the allowed players, and its reach grows.
  2. Craft a Standard and a High-Density drive and check their capacity in the terminal.
  3. Unload a full Basic drive, then scrap it. Scrapping a non-empty drive is refused.
  4. An Armored Bay soaks damage. Breaking it drops all 12 drives in a loot bag with contents intact.
  5. Two overlapping Cores: each bay is counted once (`sn heat` on each).
  6. Terminal and Crafting Terminal feel responsive on an Advanced Core.
  7. The existing test world loads with no StashNet errors.

## Risks
- **The vanilla carry-over is unproven for our blocks** (multi-block bay, composite features). Fallback if it fails: a Harmony hook that copies the old tile entity into the new one.
- In multiplayer, upgrading a bay someone has open: vanilla force-unlocks it. Goes on the M9 list.
- Mid-game networks reach the heat cap fairly quickly with Standard drives. That's intended ("a big hoard draws screamers"), but tune it after playing.

## Left to the builder
Tints, icons, sort order, upgrade hit counts and sounds, the tie-break order, the material choice behind each MaxDamage, and exact wording. All recipe and balance numbers are placeholders.

## Plan updates (`docs/PLAN.md`)
Remove the Drive Upgrade Kits. Bay tiers gain slots and are upgraded with the next-tier block. Add the Core tier recipes. Note fuel and electricity as parked balance levers. Change the M8 test line to "unload and scrap a drive; upgrade a bay and a Core without losing anything; armored bay soaks damage."
