# Station automation checklist (v0.21.0)

Use your StashNet test world. You need a Core with drives, and Network stations near it
(Network Workbench, Network Forge, Network Campfire at least). Automation is a tab in the recipe box:
select any recipe, then click the **lightning icon** next to the Ingredients and Description icons.

## 1. The tab and the category icons
- [ ] Every Network station shows the lightning tab icon. Clicking it shows "Keep ... in stock", the rule list and (forge, campfire, chemistry station) the fuel line with its explanation.
- [ ] Clicking Ingredients or Description goes back to those. Selecting another recipe while on Automation stays on Automation and changes the "Keep ... in stock" name.
- [ ] A plain vanilla workbench, forge or campfire, and backpack crafting, show **no** lightning icon.
- [ ] The category icons above the recipe list now show on every Network station (same as the vanilla station).
- [ ] Send a screenshot of the Automation tab at the Network Forge.

## 2. Fuel button and auto-fire
- [ ] With wood and coal in storage, the fuel arrows go Manual → Auto → Coal → Wood → Manual (◄ goes backwards). The sentence under it explains each mode.
- [ ] **Manual:** nothing changes compared to before. You light it yourself, and it keeps burning when idle.
- [ ] **Auto:** put fuel in the slots, queue a craft and close the screen. The fire lights by itself within about 5 seconds. When the craft finishes, the fire goes out within about 5 seconds.
- [ ] **Coal (a fuel item):** empty the fuel slots, queue a craft and close the screen. Coal from storage fills the fuel slots (one stack in total), the fire lights, and it goes out when idle.
- [ ] **Forge smelting:** in Auto, put ore in the forge and close the screen. It lights and keeps burning until all the ore has melted, then goes out, even though the melted metal stays inside.
- [ ] Other fuel already in the slots (wood while the mode is Coal) is left alone.

## 3. Keep in stock
- [ ] At the Network Workbench, select a simple recipe you can make from stored items (e.g. Wooden Club or Short Iron Pipe), set the amount to 5 and click **Keep**. A row appears: "<item>  N / 5".
- [ ] Close the screen. Within about 5 seconds the craft queues, and the ingredients leave storage. Finished items go into storage.
- [ ] It stops once storage holds 5 (counting what's still in the queue). It doesn't keep crafting past the target.
- [ ] Take some out of storage: it crafts again to get back to 5.
- [ ] Select the same recipe again: the button says **Update**. Change the amount, click: the row changes, no second row.
- [ ] The **x** on a row removes the rule.
- [ ] With 8 rules, a 9th is refused with a message.
- [ ] It always leaves one queue slot free: you can still start a craft by hand.
- [ ] Not enough ingredients in storage: nothing is queued, and nothing leaves storage.
- [ ] A campfire recipe that needs a Cooking Pot doesn't run without the pot in the tool slot, and runs with it.
- [ ] Network Forge recipes (e.g. forged iron) run from the forge's melted iron.
- [ ] Storage full: nothing breaks. Finished items wait in the station's output. Once the output slots are full too, the fire goes out (in Auto) instead of burning forever.
- [ ] A big target (e.g. 5000) queues about one stack per craft entry, not everything at once.
- [ ] No XP: automatic crafts give nobody crafting XP, even if you open the station while one is running. A craft you start by hand afterwards still gives you your normal XP.

## 4. Lock, save, removal
- [ ] Lock the Core with a code. A rule set by you still runs. (With a friend: a rule set by someone without the code stops.)
- [ ] Save and quit, then load again. The fuel mode and the rules are still there.
- [ ] Pick up or break a Network station and place a new one in the same spot: it starts with Manual and no rules.

## 5. Joined player (dedicated server or a friend's game)
- [ ] The panel works the same for a joined player, and the settings show up for everyone.

## Log
After testing, I'll check the newest `output_log_client__*.txt` for `[StashNet]` warnings, such as "automation at ...".
