# Salvage look: in-game checklist

The look is "military salvage, wrecked but working". Design: `docs/superpowers/specs/2026-11-13-wrecked-salvage-look-design.md`.

## Part 1: the Drive Bay only (v0.10.0)

Only the plain **Drive Bay** has the new look in this version. Everything else looks the same as before.

**Before installing:** nothing to pick up. Use your StashNet test world.

### Looks
- [ ] Place a Drive Bay (`cm` creative menu, search "drive bay"). It's army green, with:
  - "STASHNET" stenciled near the top, mostly worn off
  - a small green screen reading "DRV 4/4 OK"
  - a chipped **blue** stripe low on the front
  - rust, scorch marks, claw gouges and bullet holes
  - a broken pipe with a sparking wire on its right side
- [ ] Look at it from the front, both sides and the top, by day and at night.
  - Nothing floats away from the cabinet, and nothing flickers or disappears into it.
  - Holes and gouges have no square background around them.
- [ ] The blue stripe stays **blue**, not green.
- [ ] The green screen is readable in the dark.
- [ ] A normal vanilla file cabinet elsewhere still looks normal (not green, no decals).
- [ ] Its icon (backpack and creative menu) is the green, beat-up cabinet.

### It keeps working
- [ ] Put drives in and take them out. A terminal still sees them.
- [ ] Upgrade it to a Reinforced bay. The old decals go away, and the Reinforced bay looks as it did before (it gets its look in the next version).
- [ ] Walk about 200 blocks away and come back. The look is still there, only once (not doubled).
- [ ] Pick the bay up (or break it). No decals are left floating in the air.

### Tuning (if something is in the wrong place)
Tell me what to move, for example "the stripe is too low" or "the screen floats off the front". I'll change the layout file and copy just that file into the game. Then open the console (F1), type `sn skin reload` and look again. No restart needed.

### Send me
- Your verdict on the look: keep it as is, tweak it, or go another direction.
- A screenshot if you can.
- I'll read the newest game log myself.

## Part 2: everything else (next version)
Written when Phase 2 is built.
