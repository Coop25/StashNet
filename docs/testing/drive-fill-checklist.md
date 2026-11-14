# Drive balance + fill bar: in-game checklist (v0.11.0)

Use the StashNet test world. Creative menu or `sn` commands are fine for getting drives and materials.

1. **Fill bar and percent.** Put drives in your backpack:
   - an empty drive shows no bar and no number;
   - a partly filled drive shows a green bar and a number in the middle, e.g. "12%";
   - a drive at 75% or more shows yellow;
   - a full drive shows red and "100%".
2. **Description line.** Select a drive: the info panel starts with a line like "142 / 216 stacks used (66%) · 37 different items" ("Empty." for an empty drive), above the usual description.
3. **Only empty drives are used up.** With one full and one empty Basic Drive in your backpack, try to craft a Standard Drive at a workbench (it needs 2). It should be refused. Add a second empty Basic Drive and craft again: it works, and the full drive is still there with its items.
4. **The recipe shows the right count.** In the Standard Drive recipe, the "have" number for Basic Drives counts only empty ones.
5. **Other slots.** Drives in a Drive Bay, and in a loot bag (drop one), show the same bar and number.
6. **Sizes.** A new Standard Drive holds 72 stacks; a new High-Density Drive holds 216 (the description line shows the max).
7. **Over-full drive (only if you have one).** A High-Density Drive that held more than 216 stacks before this update shows red and its real percent (e.g. "133%"), keeps all its items, and new deposits go to other drives.
8. **Log.** The newest log shows `Loaded v0.11.0 (27 methods patched)` (was 21: +4 for the ingredient guard, +1 for the bar, +1 for the description) and no StashNet errors.
