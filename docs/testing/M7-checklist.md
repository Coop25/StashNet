# Milestone 7: in-game test checklist (Heat)

**Goal:** the Core gives off heat that grows with the number of drives (with a cap), using the game's own heat system (the one forges feed, which brings screamers).

**About 20 minutes**, single-player in your throwaway world. Creative mode is fine. Make sure the game's **heat setting is on** (world settings) and it's **not a blood moon** (heat pauses then).

The helper command: open the console (F1) and type `sn heat`. It prints two lines:
- **Core line:** how many drives the nearest network has and how much heat it reports (about every 3 minutes).
- **Area line:** the total heat where you stand, from every source (forges, campfires, us), out of 25. At 25 the game rolls a 1-in-5 screamer chance and resets the area to 0.

## 0. Loads cleanly
- [ ] F1: `[StashNet] Loaded v0.8.0 (19 methods patched)`, and no red errors mentioning StashNet or blocks.xml.
- [ ] Your existing Core still asks for its code / still lets you in (Cores placed before M7 load fine).

## 1. Heat scales with drives
Stand next to your Core for each step.
- [ ] Bays with **no drives**: `sn heat` says `0 drive(s) -> 0 heat per report`.
- [ ] **12 drives** (`giveself stashnetDriveBasic 12`, spread over 3 bays): says `12 drive(s) -> 3 heat per report`.
- [ ] **26 or more drives** (more bays): says `6.5 heat per report` (the cap). It never goes higher, however many you add.

## 2. Heat builds up over time
- [ ] With 12 drives and **no forge/campfire nearby**, run `sn heat` now, then again after ~3, ~6, and ~10 minutes. The area number rises in steps but **levels off around 13** and never reaches 25. **Expected:** no screamers from the network alone.
- [ ] (Optional, takes a while) Light a **forge** next to it and leave it running. The area number climbs past 25 within ~12 minutes, then drops back to 0 (that's the screamer roll). Screamers may or may not show up (1 in 5 per roll).

## 3. Terminals
- [ ] Take **all drives out of the bays** first (so the Core makes 0 heat), wait ~3 minutes, and note the area number. Then use the **Storage Terminal** for a bit (open, take/put items). The area number goes up by about 0.5 (at most once every ~3 minutes). Put the drives back afterwards.
- [ ] Open the **Crafting Terminal** but don't craft: no extra bump from it. While it's **crafting**, it gives off normal workbench heat (vanilla behavior).

## 4. Edge cases
- [ ] Pick up the Core (empty network first): `sn heat` says "No StashNet Core in range." Place it back: heat resumes.
- [ ] (Optional) `settime 1 8 0` to go back in time: the Core keeps reporting heat (check `sn heat` after ~3 minutes).
- [ ] Save, quit to the menu, reload: `sn heat` still shows the same drive count and heat.

When done, send me the results (and I'll check the log).
