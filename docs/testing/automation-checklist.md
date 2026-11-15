# Station automation checklist (v0.20.0)

Use your StashNet test world. You need a Core with drives, and Network stations near it
(Network Workbench, Network Forge, Network Campfire at least). The new **AUTOMATION** panel is on
the right side of a station's screen, below OUTPUT.

**Please send a screenshot of the Network Forge screen**, so I can check the panel fits.

## 1. The panel
- [ ] The Network Workbench shows the AUTOMATION panel, but no fuel button.
- [ ] The Network Forge and Network Campfire show the panel with a "Fuel: Manual" button.
- [ ] A plain vanilla workbench, forge or campfire does **not** show the panel.

## 2. Fuel button and auto-fire
- [ ] With wood and coal in storage, clicking the fuel button goes Manual → Auto → Coal → Wood → Manual (fuels are listed by item name, so the order may differ).
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
- [ ] Storage full: nothing breaks. Finished items wait in the station's output.

## 4. Lock, save, removal
- [ ] Lock the Core with a code. A rule set by you still runs. (With a friend: a rule set by someone without the code stops.)
- [ ] Save and quit, then load again. The fuel mode and the rules are still there.
- [ ] Pick up or break a Network station and place a new one in the same spot: it starts with Manual and no rules.

## 5. Joined player (dedicated server or a friend's game)
- [ ] The panel works the same for a joined player, and the settings show up for everyone.

## Log
After testing, I'll check the newest `output_log_client__*.txt` for `[StashNet]` warnings, such as "automation at ...".
