# Salvage look: in-game checklist

The look is "military salvage, wrecked but working". Design: `docs/superpowers/specs/2026-11-13-wrecked-salvage-look-design.md` (Revisions 1 and 2: the look is painted into copies of the models' own textures while the game runs).

## Part 1 (v0.10.1): done 2026-11-13
The texture dump worked for all 8 blocks.

## Part 2 (v0.10.2): first repaint + find the fronts

**What's new:**
- All 8 StashNet blocks are repainted: army green, grime, rust, scorch marks, bullet holes (dents on the Armored bay), claw marks and worn bare metal.
- The CRT terminal shows the green "STASHNET v1.0" screen.
- No stencils or tier stripes yet. Those come once we know where each block's front is (the grid step below).

**Before installing:** nothing to pick up.

### Look
- [ ] Load your StashNet test world. Every StashNet block is army green and beat up. Vanilla file cabinets, safes, TVs, server racks, electrical boxes, transformers, control panels and workbenches elsewhere look **unchanged**.
- [ ] The CRT terminal's screen shows green text ("STASHNET v1.0", CORE / DRIVES / LINK lines) and flickers like an old TV.
- [ ] The first time each block type appears, the game may stutter for a moment while it repaints. It shouldn't happen again after that.
- [ ] Does it look the way you wanted? Too dirty, not dirty enough, the wrong green? Tell me in your own words.

### Find the fronts (about 3 minutes)
- [ ] Stand near one of each StashNet block (the same spots as for the dump). Open the console (F1) and type `sn skin grid`. The blocks now show a yellow grid with labels like `C4`.
- [ ] For each block, look at the **front** (the side facing you when you placed it) and note which labels you see there, for example: "Drive Bay front: B5, C5, B6, C6". If a label looks mirrored or upside down, say so. A screenshot of each works too.
- [ ] Type `sn skin grid` again to turn the grid off.

### Nothing broke
- [ ] Cores, bays and both terminals still work: storing, crafting, upgrading, the code lock.
- [ ] Close the game when done. I'll read the log.
