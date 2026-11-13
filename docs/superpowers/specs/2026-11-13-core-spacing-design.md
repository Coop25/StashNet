# Core spacing (short design, M8 follow-up)

*2026-11-13. Agreed with the owner in conversation.*

## Intent
Two Cores close together make overlapping networks that are confusing to use: bays switch owners, and the terminal and lock follow whichever Core is nearer. The owner wants Cores kept apart so networks never overlap, even once both are fully upgraded.

## Decision
- **A Core can't be placed if any other Core is within 2 × the largest Core reach** (cube distance, every direction including up and down). With reach 24 for the Advanced Core, that's 48 blocks: placing at 48 or closer is refused, at 49 or more it's allowed. The number is derived from the Cores' `StashNetRadius` in XML, so it follows tuning.
- **Every Core counts** (owner, 2026-11-13): any tier and any owner. Accepted trade-off: on a server, someone could block a network near an unclaimed base by placing a Core first. Land claims already stop others building inside a base. Revisit in M9.
- It applies to every tier being placed, since any Core can later be upgraded. Upgrading a Core in place is unaffected (same spot, no placement).
- When refused, the game plays its normal "can't build" sound and a tooltip names the distance: *"Another Storage Core is only {0} blocks away. Cores must be more than {1} blocks apart so their networks never overlap."*
- Cores already too close keep working. Nothing is removed, and the nearest-Core rule (M8) still keeps their bays apart.
- The check is skipped in the prefab editor (edit mode), like vanilla's trader-area rule.

## Approaches
1. **Picked: a small Core block class** (`StashNet.Blocks.BlockStashNetCore : BlockCompositeTileEntity`, XML `Class="StashNet.Blocks.StashNetCore, StashNet"`, the same pattern as the terminal) that overrides `CanPlaceBlockAt`. The game calls it when the player clicks to place (`BlockToolSelection`). Placed Cores keep the same tile entity type, so saves are unaffected.
2. A Harmony patch on `Block.CanPlaceBlockAt`. Rejected: the method isn't declared on `BlockCompositeTileEntity`, so the patch would hit every block in the game (the project's Harmony rule).

## Notes
- The distance check is game-free (`StashNet.Core/Network/CoreSpacing.cs`) with unit tests. It reuses `CoreSite`.
- Placement is checked on the placing client, as vanilla does for its own placement rules. A modified client could skip it. That's the same trust level as vanilla. Note for M9.
