# StashNet look: "wrecked but working" military salvage

Date: 2026-11-13. Branch: `m9-release` (lands before 1.0). Status: design, waiting for the owner's approval.

## Why

The owner wants StashNet's blocks to look like their own thing, not like furniture players recognize from the game. In the owner's words, the cleaner mockups looked "too clean and not cobbled together". The look should fit 7 Days to Die: survivor gear salvaged from a military bunker that has been through hordes and still runs.

## Goals

- All **8 StashNet blocks** get one shared style: the 3 Cores, the 3 Drive Bays, the Storage Terminal and the Crafting Terminal.
- **Style (owner's pick: mockup "C3 Military salvage", damage level "Wrecked but working"):**
  - army-green paint (the Armored bay a darker green)
  - a stenciled "STASHNET" (plus a unit label such as "UNIT 01 · BAY"), mostly worn off
  - bare metal showing at worn edges and corners
  - bullet holes, deep claw gouges and scorch marks
  - heavy rust streaks and a rusted-through base
  - a small, cracked, taped-up green-text readout (for example "DRV 4/4 OK")
  - a chipped stripe in the tier color: tier 1 blue, tier 2 green, tier 3 purple, matching the drives' tape colors
  - on bays: a broken cable conduit with a (still) sparking wire, and loose cables
- **Per tier (owner confirmed):** each tier keeps its current vanilla shape and gets the treatment in its own tier color.
  - Drive Bay = tall file cabinet, blue. Readout "DRV 4/4".
  - Reinforced = server rack, green. One rack unit hangs half out, and cables spill from the bottom. Readout "DRV 8/8".
  - Armored = gun safe, darker green, purple. Bullet hits are dents only (no holes) and there are pry marks around the door: it took the hits and held. Readout "DRV 12/12".
  - Scrap Core = electrical box, blue. Storage Core = transformer, green. Advanced Core = control panel, purple.
- **Storage Terminal becomes the vanilla CRT TV with its screen on** (`@:Entities/Electrical/tvCRTPlayerOnPrefab.prefab`, the model of `tvCRT_PlayerOn`). Its screen shows a fixed old-computer **green text** picture (owner's pick): "STASHNET v1.0", status lines such as "> CORE ......... OK", "> DRIVES ....... 12", "> LINK ........ OK", and a cursor. Scan lines and a slight curve make it read as a tube screen. The TV body gets matching scuffs and a stenciled label.
- **Crafting Terminal** keeps its workbench shape (it inherits the workbench model) and gets the same paint, damage and a small green readout.
- **Icons:** all 8 blocks get new icons that match their new look. Drive icons stay as they are.
- Readout and screen text is a fixed picture, not live data. "DRV 4/4" is decoration; it doesn't change with the real drive count.

## Non-goals

- New 3D shapes, Unity, Blender or asset bundles. That was option B, set aside for after 1.0 if the owner still wants it.
- The drives' held and dropped model. It stays the vanilla sack, which the game also uses for car batteries, electrical parts and small engines.
- Real particle sparks, sounds or live screen contents. The sparking wire is part of a still picture. Flicker on the green screens is fine if cheap (see "Left to the builder").
- Any gameplay change. Sizes, slot counts, HP, recipes, heat and upgrades stay the same.

## Anti-goals (a failure even if it works)

- **Looks clean, sci-fi or neon.** The owner turned down the clean versions. When in doubt, dirtier and more worn.
- **Changes how vanilla blocks look.** These models share materials and textures with vanilla file cabinets, safes, TVs and so on. Anything we change must be our own copy, applied only to StashNet blocks.
- **Errors or extra work on a dedicated server.** It has no graphics, so all look code must do nothing there.
- **Noticeable FPS cost** with a normal base (say 30 StashNet blocks in view).
- **Breaking saves** or the owner's test world beyond the one known terminal issue below.

## Constraints

- **No extra software for the owner.** Everything is made by code and scripts in the repo, like the drive icons (`tools/icons/draw-drive-icons.ps1`). Texture-drawing scripts must be deterministic, so re-running them reproduces the same files byte for byte.
- Game V3.4 b10, Unity 2022.3.62f2. The mod DLL compiles against the game's own libraries (see CLAUDE.md "Build note").
- The look is client-side only. Block definitions still come from XML. Gameplay code is untouched.
- Upgrades swap the block in place (vanilla `UpgradeBlock`), so the new tier's model spawns fresh. The look must appear on the new model each time.
- Project conventions still apply: names start with `stashnet`, game DLLs aren't committed, decompiled code goes to scratch only, and deploying needs the owner's yes with the game closed.

## Approaches considered

| | Approach | Trade-off |
|---|---|---|
| **A (picked)** | Keep the vanilla models. Tint them army green and attach flat "skin" pieces (decals, stripes, screens) to their faces at runtime, with textures drawn by script. | No install for the owner, quick, low risk for 1.0. The outline stays vanilla, and curved models are harder to dress. |
| B | Fully new models built in Unity and loaded from an asset bundle. | Completely our own shapes. Needs the owner to install Unity (5–10 GB plus a sign-in), the most work, and the code-built models would look simpler than hand-made art. Possible after 1.0. |
| C | New shapes built by code at runtime, without Unity. | Our own shapes with no install, but an unusual hack for 7DTD and hard to make match the game's lighting. Dropped. |

The owner picked A: "lets go with A".

## How it works (decided parts)

1. **Paint.** Each block's XML gets an army-green `TintColor` (darker for the Armored bay). The gun safe already uses `TintColor`, so this works on these models. Exact colors are the builder's call; they should match the mockups.
2. **Skin pieces at runtime.** When a StashNet block's model appears, the mod attaches flat textured pieces to it as child objects. These include the stencil, damage decals, the tier stripe, the readout and the CRT screen. The hook is `Block.OnBlockEntityTransformAfterActivated(WorldBase, Vector3i, BlockValue, BlockEntityData)`, verified by decompiling. `BlockEntityData` carries the model's transform. It's a virtual method, so the code can override it in our own block classes:
   - Cores: `StashNet.Blocks.StashNetCore`, which exists.
   - Storage Terminal: `StashNet.Blocks.StashNetTerminal`, which exists.
   - Drive Bays: today XML `Class="CompositeTileEntity"`. They need a new `BlockCompositeTileEntity` subclass, wired up the same way the Core was in v0.9.1. Changing a block's class is save-safe, because saves store the tile entity, not the block class.
   - Crafting Terminal: inherits `Class="Workstation"` (`BlockWorkstation`) from the workbench. It needs a new `BlockWorkstation` subclass, set with an explicit `Class` property in our block.
   The hook does nothing on a dedicated server, and it never edits a shared material in place.
3. **Textures** are PNGs in the mod folder, drawn by a new deterministic script under `tools/`. One texture sheet (atlas) is fine. It needs these pieces:
   - the stencil and unit labels
   - bullet holes and dents
   - claw gouges, scorch marks and rust streaks
   - bare-metal wear patches and the rusted base band
   - tier stripes in 3 colors, chipped
   - the cracked green readout, with one picture per readout text
   - duct tape, conduit, cables and the sparking wire
   - the CRT green text screen
4. **Where each piece goes** on each of the 6 vanilla models (plus the CRT and the workbench) is a per-block layout with position, rotation, size and texture piece. It must be easy to tweak without rewriting code.
5. **Storage Terminal model change:** use the `tvCRTPlayerOnPrefab` model. The block becomes **1×1×1**: remove `DisplayType=blockMulti`, `MultiBlockDim=2,1,1` and `ModelOffset=.5,0,0`. Copy placement properties from vanilla `tvSmallStand1x1` where they apply (`GndAlign`, `Place`, `Collide`). Keep our class, heat property, HP, pickup, repair and recipe.
   - **Known save issue:** terminals already placed in the owner's test world are 2 wide. Before installing this version, the owner picks up every Storage Terminal in the test world and places them again afterwards. No one else has StashNet installed yet, so this only affects the owner's test world.
6. **Icons:** 8 new PNGs in `Mod/UIAtlases/ItemIconAtlas/<block name>.png`. Each is made by a script from that model's vanilla icon in the game install: recolored army green, with the same decals and tier stripe composited on. Then `CustomIcon` and `CustomIconTint` are removed from those blocks, so the game uses the PNG named after the block, the same way the drives work. This is my call: it keeps each icon matching its world model. The derived PNGs are committed like the drive icons.
7. **Order of work (owner agreed):**
   1. Build the **Drive Bay** alone first: tint, all its decals, readout, stripe and icon.
   2. The owner looks at it in-game and gives feedback.
   3. Only then do the other 7 blocks.
8. **After this:** version bump (builder's call on the number, for example 0.10.0), owner test, then M9 Part A (the multiplayer test) runs on the new look.

## Open technical risk (settled by the Drive Bay step)

- **Shader.** Which shader the skin pieces use so they light like the rest of the model. Holes and claw gouges need transparent edges (alpha cutout), and the screens should glow or stay readable in the dark. First try: clone the model's own material and swap its texture. If that can't do cutout or glow, find a suitable shader already loaded in the game. `Shader.Find("Standard")` may return nothing in a game build.
- **Curved or detailed models** (transformer, control panel, server rack) may need fewer or smaller pieces to avoid floating decals. Fewer, well-placed pieces beat many that float.
- **TintColor and decals:** tinting tints the model's material, not our pieces. Our pieces carry their own colors.

## Left to the builder

- Exact colors, sizes and placement of every piece, matched to the mockup drawings and the "dirtier, not cleaner" rule.
- Layout format: an XML file in `Mod/Config/`, or properties on each block. XML is preferred, so the owner's tweaks don't need a code change.
- Screen flicker: a cheap per-block random dim of the readout and CRT picture is welcome. Drop it if it costs more than a trivial update.
- Whether the sparking wire and spilled cables are flat pictures or simple thin 3D strips.
- Version number.

## Testing

- `dotnet test` stays green. Layout parsing and any game-free texture math get tests where it's natural.
- **Drive Bay step:** the owner places a Drive Bay in the test world and checks it from several angles, by day and night. Things to check:
  - nothing floats or flickers through the model
  - vanilla file cabinets elsewhere look unchanged
  - the icon matches
  - the log shows no StashNet errors
- **Full step:**
  - every block in all tiers looks as described
  - upgrading swaps the look in place
  - the CRT terminal sits flat on the floor and works as before
  - vanilla TVs, safes, racks, cabinets, electrical boxes, transformers, control panels and workbenches look unchanged
  - FPS is fine with a full base in view
  - no errors when the mod runs on a dedicated server (covered by M9 Part A)

## Notes to update when this lands

- CLAUDE.md "Conventions": v1 is no longer "vanilla models and tinted vanilla icons". It becomes vanilla shapes with StashNet skins, plus custom icons for all blocks and drives.
- CLAUDE.md "Tier looks": the terminal is the CRT.
- README (player-facing): it doesn't describe block looks today, so no change is needed unless screenshots are added.

## Mockups

The owner picked from rough drawings in the brainstorming browser. The chosen ones are copied next to this file in `2026-11-13-wrecked-salvage-look-mockups/`. Open them in a browser:
- `skin-c3-beatup.html`: the damage level; "Wrecked but working" is the second card.
- `bays-all-tiers.html`: all three bays.

They're flat front views. In-game, the pieces sit on the real vanilla shapes.
