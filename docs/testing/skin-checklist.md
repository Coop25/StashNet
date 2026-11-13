# Salvage look: in-game checklist

The look is "military salvage, wrecked but working". Design: `docs/superpowers/specs/2026-11-13-wrecked-salvage-look-design.md` (see "Revision 1": the look is painted into the models' own textures).

## Part 1: save the textures, try the CRT terminal (v0.10.1)

This version changes nothing about how blocks look except two things:
- The Storage Terminal is now the **old CRT TV** (screen on). It still shows the TV's own picture; the green StashNet screen comes next.
- The Drive Bay still wears the test decals from v0.10.0.

Its main job is to save the real textures of each StashNet model, so I can repaint them.

**Before installing:** in your StashNet test world, **pick up every Storage Terminal**. They're 2 blocks wide now and become 1 wide. Leaving one placed could leave a broken half-block behind. Place them again afterwards.

### Save the textures (about 2 minutes)
- [ ] Load the test world. Using `cm` (creative menu), place **one of each** StashNet block close together:
  - Scrap Core, Storage Core, Advanced Core
  - Drive Bay, Reinforced Drive Bay, Armored Drive Bay
  - Storage Terminal, Crafting Terminal

  The Cores must be at least 49 blocks apart, so place them in three spots and do the next step at each one. Alternatively: place one Core, run the next step, upgrade the Core in place, and repeat.
- [ ] Stand next to them, open the console (F1) and type `sn skin dump`. It answers with something like "Saved N files for M block types". Run it again at each spot until all 8 are covered.
- [ ] Close the game and tell me. The files are in `Mods\StashNet\SkinDump`, and I'll read them myself.

### CRT terminal
- [ ] Place a Storage Terminal. It's the old CRT TV, it sits flat on the floor, and you can't walk through it.
- [ ] Press E: the terminal opens and works as before (withdraw, deposit).
- [ ] Pick it up again: that works too.

### Nothing broke
- [ ] Cores, bays and the Crafting Terminal still work: storing, crafting, upgrading, the code lock.
- [ ] I'll read the newest game log myself.
