# Milestone 6: in-game test checklist (Crafting Terminal)

**Goal:** a Crafting Terminal works exactly like a workbench, but recipes can use ingredients stored in the network.

**About 15 minutes**, single-player in your throwaway world (creative mode is fine for getting materials).

## 0. Loads cleanly
- [ ] F1: `[StashNet] Loaded v0.7.0 (15 methods patched)` (9 new crafting hooks), and no red errors mentioning StashNet, blocks.xml, or recipes.xml.

## 1. The block
- [ ] Crafting menu: **Crafting Terminal** shows up (needs a Workbench + a Storage Terminal + 4 electrical parts). Or `giveself stashnetCraftingTerminal`.
- [ ] Place it **within 6 blocks of your Core**. It looks like a workbench, with a green-tinted icon.
- [ ] Open it: it's the **normal workbench screen** (recipe list, queue, output).

## 2. Crafting from the network
- [ ] Put some ingredients **only in the network**, not in your backpack (e.g. forged iron, nails, wood).
- [ ] In the Crafting Terminal, a recipe that uses them shows the **network amount** in its "have/need" numbers, and it's **craftable**.
- [ ] Craft it. **Expected:** it crafts. The ingredients disappear from the network (check the Storage Terminal), not from anywhere else.
- [ ] **Mixed:** have half the ingredients in your backpack and half in the network. Craft. Only the missing part is pulled from the network.
- [ ] **Craft count > 1:** set the count to 5 with enough in the network. All 5 craft.

## 3. Normal workbench rules still apply
- [ ] Recipes you haven't unlocked are still locked.
- [ ] Cancel a queued craft: the ingredients come back to your **backpack** (vanilla behavior).
- [ ] The output appears in the output slots as usual.

## 4. Doesn't leak into other places
- [ ] A **plain vanilla workbench** does NOT see network items (it counts only your backpack).
- [ ] A Crafting Terminal **out of range** of any Core works as a plain workbench.

## 5. Backpack full
- [ ] Fill your backpack, then craft something whose ingredients are all in the network.
- [ ] **Expected:** either it crafts (if the pulled items fit), or it's refused with "missing resources". Nothing disappears: the network totals stay correct.

## Report back
PASS/FAIL per section. If counts look wrong, a screenshot of the recipe's "have/need" panel helps.
