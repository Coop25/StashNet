# Drive tiers + fill bar: in-game checklist (v0.15.0)

Use the StashNet test world. Creative menu or `sn` commands are fine for getting drives and materials.

1. **Names and icons.** The creative menu (search "Drive") shows five drives: 48-Stack, 192-Stack, 576-Stack, 1728-Stack and 5184-Stack Drive. Nothing anywhere says "1K", "4K" etc. Each has its own icon, with its stack count (48, 192, 576, 1728, 5184) written on the label. Drives you already had are now 48-stack / 192-stack / 576-stack and still hold their items.
2. **Fill bar and percent.** Put drives in your backpack:
   - an empty drive shows no bar and no number;
   - a partly filled drive shows a green bar and a number in the middle, e.g. "12%";
   - a drive at 75% or more shows yellow;
   - a full drive shows red and "100%".
3. **Description line.** Select a drive: the info panel starts with a line like "142 / 576 stacks used (24%) · 37 different items" ("Empty." for an empty drive), above the usual description.
4. **Only empty drives are used up.** With one full and two empty 48-Stack Drives in your backpack, open a workbench: the 192-Stack Drive recipe is not shown as craftable, and Craft is refused (it needs 3). Add a third empty 48-Stack Drive and craft again: it works, and the full drive is still there with its items.
5. **The recipe shows the right count.** In the 192-Stack Drive recipe, the "have" number for 48-Stack Drives counts only empty ones, and the craft-count arrows don't go past what your empty drives allow.
6. **Higher tiers.** The 576-stack, 1728-stack and 5184-stack recipes each ask for 3 drives of the tier below (creative-spawn the drives to check quickly).
7. **Sizes.** Each drive's description line shows its max: 48 / 192 / 576 / 1,728 / 5,184.
8. **Other slots.** Drives in a Drive Bay, and in a loot bag (drop one), show the same bar and number.
9. **Log.** The newest log shows `Loaded v0.15.0 (32 methods patched)` and no StashNet errors.
10. **Taller terminal.** The Storage Terminal window now reaches the bottom of the backpack window and shows 11 rows of items (88 per page). Send a screenshot so we can line it up exactly.
11. **Mouse wheel.** Scrolling over the item slots turns the pages (wheel down = next page, up = previous). Also check every slot still highlights on hover and withdraws on click.
12. **Backpack move buttons.** With a terminal open, the backpack's two move buttons (top right of the backpack, next to sort) are enabled:
    - "move all" stores every unlocked backpack stack in the network;
    - "fill and smart" stores only items the network already holds;
    - locked backpack slots stay put; drives stay in the backpack (shift-click still unloads them).
    With a normal loot container open instead, both buttons work like vanilla.
13. **Category tabs.** Under the search box there's a row of icons: All, then Reading, Resources, Ammo/Weapons, Tools/Traps, Science, Mods, Food/Cooking, Decor/Misc, Medical, Armor (hover shows the name).
    - Clicking one shows only that kind of item and goes back to page 1; All shows everything.
    - It works together with the search box and the sort button.
    - Close and reopen the terminal: the same tab is still selected.
    - Every item slot still highlights and withdraws normally.
14. **Dragging with a terminal open.** Drag a stack around your backpack and toolbelt: it follows the cursor and drops where you let go (fixed in v0.14.1).
15. **No header button.** The terminal's top-right "deposit all" icon is gone; the backpack's move-all button does that job.
16. **Tabs only when used.** Only All plus categories that hold at least one stored item show a tab. Store something from a new category: its tab appears within ~2 seconds. Take the last of a category out: its tab disappears (and if it was selected, you're back on All).
17. **Click = one, shift-click = stack.** Clicking a stored item takes 1. Shift-clicking takes a full stack (or everything, if less than a stack is stored). Right-click also takes 1.
