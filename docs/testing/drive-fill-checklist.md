# Drive tiers + fill bar: in-game checklist (v0.12.0)

Use the StashNet test world. Creative menu or `sn` commands are fine for getting drives and materials.

1. **Names and icons.** The creative menu (search "Drive") shows five drives: 1K, 4K, 16K, 64K, 256K. Each has its own icon, with its stack count (48, 192, 576, 1728, 5184) written on the label. Drives you already had are now 1K / 4K / 16K and still hold their items.
2. **Fill bar and percent.** Put drives in your backpack:
   - an empty drive shows no bar and no number;
   - a partly filled drive shows a green bar and a number in the middle, e.g. "12%";
   - a drive at 75% or more shows yellow;
   - a full drive shows red and "100%".
3. **Description line.** Select a drive: the info panel starts with a line like "142 / 576 stacks used (24%) · 37 different items" ("Empty." for an empty drive), above the usual description.
4. **Only empty drives are used up.** With one full and two empty 1K Drives in your backpack, open a workbench: the 4K Drive recipe is not shown as craftable, and Craft is refused (it needs 3). Add a third empty 1K Drive and craft again: it works, and the full drive is still there with its items.
5. **The recipe shows the right count.** In the 4K Drive recipe, the "have" number for 1K Drives counts only empty ones, and the craft-count arrows don't go past what your empty drives allow.
6. **Higher tiers.** The 16K, 64K and 256K recipes each ask for 3 drives of the tier below (creative-spawn the drives to check quickly).
7. **Sizes.** Each drive's description line shows its max: 48 / 192 / 576 / 1,728 / 5,184.
8. **Other slots.** Drives in a Drive Bay, and in a loot bag (drop one), show the same bar and number.
9. **Log.** The newest log shows `Loaded v0.12.0 (27 methods patched)` and no StashNet errors.
