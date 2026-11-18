# Live terminal screen checklist (v1.1.0)

Use your StashNet test world (or the supercraft server for step 9). You need a Core, a few Drive Bays
with drives, and Storage Terminals near the Core.

## What's new
The Storage Terminal's TV screen is now live. It shows stacks used / total with a fill bar, the number of
drives and how many are full, "SYSTEM HEAT" with a word (CALM, WARM or HOT: the heat the network itself gives off, not your whole area), and `[LOCKED]` or `[OPEN]`.
Players the Core's code doesn't let through see only `[LOCKED]`, no numbers. A terminal with no Core in
reach shows `NO SIGNAL`. Every so often the screen glitches for a moment (random, on purpose).

**Tuning the glitch:** in `Mods/StashNet/Skins/paint.xml`, on the `stashnetTerminal` block, change
`glitchMin`, `glitchMax` (seconds between glitches), `glitchLength` and `glitchStrength`. Then type
`sn skin reload` in the console to apply it.

## Steps
- [ ] **1. Load the world.** Every terminal screen switches from the old green picture to live numbers within a few seconds.
- [ ] **2. Numbers match.** Open a terminal. Stacks used / total and the drive count on the screen match the terminal window.
- [ ] **3. Big deposit.** Shift-click about 40 stacks into the terminal. The screen keeps up (it updates within about 1–2 seconds) and ends on the right number.
- [ ] **4. Drive out by hand.** Take a drive out of a bay. The screen updates within about 5 seconds.
- [ ] **5. Lock.** Set a code on the Core: the screen shows `[LOCKED]`. A second player who is not on the list sees only `[LOCKED]` (no numbers). After they enter the code they see numbers within about 2 seconds.
- [ ] **6. No Core.** Break the Core: the screen shows `NO SIGNAL` within about 10 seconds. Place it back: numbers return.
- [ ] **7. Seven terminals.** Put seven terminals on one network. They all show the same numbers, but glitch at different moments.
- [ ] **8. Vanilla TV.** Break a terminal and place a vanilla TV in the same spot: it shows the normal vanilla picture. Run `sn skin reload`: the terminal screens come back live.
- [ ] **9. Server.** On the supercraft server with a friend (it needs 1.1.0 too): both of you see live screens. `sn perf` shows no rebuilds while nobody touches anything.
- [ ] **10. Log.** Check the newest `output_log_client__*.txt` for StashNet errors. It should say `Loaded v1.1.0 (N methods patched)` with the same number as v1.0.2 (no new patches were added).

## Also in 1.1.0

11. Open a Storage Terminal: the category tab icons sit fully inside the window (the first one used to hang off the left edge).
12. A screen's bottom line reads `SYSTEM HEAT` with CALM / WARM / HOT on the right (the heat the network itself gives off).
13. With the game option "Disable Smelter" on, a Network Forge's automation tab shows no melt row. With it off, the melt row is there as before.
14. Open a Network station: for a moment, before its settings arrive, none of the Fire or Output buttons is lit; then the saved choice lights up.
