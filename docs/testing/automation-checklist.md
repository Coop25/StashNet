# Station automation checklist (v0.22.1)

Use your StashNet test world. You need a Core with drives, and Network stations near it
(Network Workbench, Network Forge, Network Campfire at least). Automation is the **lightning tab** in
the recipe box. At Network stations it opens by itself when no recipe is selected.

## 0. New in v0.22.0 / v0.22.1 (two tabs: lightning = Stock, fire = Station)
- [ ] **Tab without a recipe:** open the Network Forge without clicking a recipe. The recipe box shows with the Stock tab open (or the Station tab, if that was the last one you used at a forge this game). Stock: "Select a recipe to keep in stock", Keep melted, the list. Station (fire icon): Finished items, Fire, Refill, each with a line saying what it does. Click a backpack item: its info shows instead (like vanilla); click a recipe: the box comes back.
- [ ] **Melt names** match the forge window: Iron, Brass, Lead, Sand (= glass), Stone, Clay.
- [ ] **Both ore slots:** with an Iron target and both ore slots empty, scrap iron goes into BOTH slots (about half each) and both melt at once. With Iron and Clay targets and both slots empty, each gets one slot.
- [ ] **Vehicle parts from storage (v0.23.4):** with engines and a few car batteries of different quality ONLY in storage (none in the backpack), open a Network Workbench: a vehicle that needs them shows as craftable. Craft it: an engine and the LOWEST-quality battery (two if the recipe needs two) are taken; the better batteries stay in storage.
- [ ] **One slot per metal (v0.23.3):** with Clay alone, clay fills both slots. Now add an Iron target (scrap iron in storage): within about 5 seconds the smaller clay stack goes back to storage and iron takes that slot. With Clay, Iron and Brass targets, the slots never hold the same metal twice.
- [ ] Click a recipe: the box switches to Ingredients by itself (the tab only opened because nothing was selected). Click the lightning icon yourself, then pick other recipes: it stays on Automation.
- [ ] Ingredients / Description buttons do nothing until a recipe is picked; with a recipe they work as before.
- [ ] With no recipe selected, type an amount into the melt box: the box keeps your typing (doesn't lose focus every second or two).
- [ ] **Fire:** Myself / Auto buttons, the chosen one highlighted. Hover shows what each does.
- [ ] **Refill** (only with Fire = Auto): "none" plus the fuels in storage with their icons and counts. Click Coal: it's highlighted and refills like v0.21.0's Coal mode. Click "none": back to plain Auto. Run the coal out of storage: Coal still shows (count 0) while it's chosen.
- [ ] **Melt targets (Network Forge):** pick Iron with ◄ ►, set 2000, click Keep. "Iron (melted)  N / 2000" appears. Put scrap iron in storage, close the screen: within about 5 seconds scrap iron moves into the ore slots, only as much as needed, and (Fire = Auto) the forge lights. It stops at 2000 and the fire goes out.
- [ ] Several metals at once (Iron and Clay): both fill. A 7th metal can't exist (only 6 metals).
- [ ] **Glass:** with both broken glass and crushed sand stored, a Glass target uses broken glass first, then sand.
- [ ] Nothing else is ever melted: weapons, tools, nails, cans in storage stay put.
- [ ] Ore you put in yourself counts: with 1000 scrap iron already in the slots and a 1000 target, nothing more is pulled.
- [ ] **Finished items: Stay here.** Click it, queue a craft: the result stays in Output; cancelling a craft refunds to your backpack. Click To storage: Output empties into storage again. Each station remembers its own setting after save and reload.
- [ ] **Vehicle parts:** put a modless Small Engine and a Car Battery in storage (none in your backpack), with the rest of a vehicle's ingredients. At the Network Workbench the recipe shows as craftable; Craft pulls the engine and battery (the lowest-quality ones if you have several). An engine with a mod attached is never used.
- [ ] Send a screenshot of the Automation tab at the Network Forge.

## 1. The tab and the category icons
- [ ] Every Network station shows the lightning tab icon.
- [ ] Selecting another recipe while on Automation stays on Automation and changes the "Keep ... in stock" name.
- [ ] A plain vanilla workbench, forge or campfire, and backpack crafting, show **no** lightning icon, and vanilla stations still show their empty panel until a recipe is picked.
- [ ] The category icons above the recipe list show on every Network station.

## 2. Auto-fire
- [ ] **Fire: Myself:** nothing changes compared to before. You light it yourself, and it keeps burning when idle.
- [ ] **Auto:** put fuel in the slots, queue a craft and close the screen. The fire lights by itself within about 5 seconds. When the craft finishes, the fire goes out within about 5 seconds.
- [ ] **Auto + Refill Coal:** empty the fuel slots, queue a craft and close the screen. Coal from storage fills the fuel slots up to 100 (not a whole stack), the fire lights, and it goes out when idle.
- [ ] **Forge smelting:** in Auto, put ore in the forge and close the screen. It lights and keeps burning until all the ore has melted, then goes out, even though the melted metal stays inside.
- [ ] Other fuel already in the slots (wood while Refill is Coal) is left alone.

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
- [ ] Save and quit, then load again. The fire, refill, melt targets, output switch and rules are still there.
- [ ] Pick up or break a Network station and place a new one in the same spot: it starts with Fire Myself, To storage, no rules and no melt targets.

## 5. Joined player (dedicated server or a friend's game)
- [ ] The panel works the same for a joined player, and the settings show up for everyone.

## Log
After testing, I'll check the newest `output_log_client__*.txt` for `[StashNet]` warnings, such as "automation at ...".

## 6. Shared network summary (v0.23.0)
- [ ] Everything above still works the same (keep in stock, melting, refill, finished items to storage).
- [ ] With 2+ forges at their melt targets and nothing to do, `sn perf reset`, wait a minute, `sn perf`: "Real network reads" is low (about 1 per minute per network from the backstop, plus any terminal use), and "Answered from the shared summary" counts up.
- [ ] Put scrap iron into storage through a Storage Terminal while a forge's Iron target is short: it feeds within ~5 s.
- [ ] Take a drive holding scrap iron out of a bay by hand, then put it back: the forge notices within a few seconds both ways ("Summary rebuilds (drives changed)" in `sn perf`).
- [ ] Storage completely full but some arrows already stored: finished arrows at a Network station still move into storage.
