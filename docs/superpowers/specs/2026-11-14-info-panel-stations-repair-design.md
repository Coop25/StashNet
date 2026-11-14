# Info panel, exact amounts, stations and repair from storage (design)

Date: 2026-11-14. Target version: v0.19.0. Owner asked for all four after v0.18.0.

## Intent
Remove the remaining "walk to storage and back" chores:
1. See an item's full info (stats, mods, read/unread) without taking it out.
2. Take an exact amount (37 nails) in one go.
3. New StashNet versions of the forge, chemistry station, campfire and cement mixer use stored ingredients (owner, 2026-11-14: "New StashNet stations", like the Crafting Terminal).
4. Repair takes the repair kit from storage.

## Goals / non-goals / anti-goals
- Goals: the four above, for single-player and joined players.
- Non-goals: forge smelting input (ore into the forge's material slots stays vanilla); vanilla
  stations and normal workbenches stay vanilla (owner: "Only those four", then "New StashNet
  stations"); new 3D looks for the stations; toolbelt move-all.
- Anti-goals: anything that duplicates or loses items; slowing down quick grabs.

## Decisions (owner)
- Info panel: click a cell = select it and open the info panel; clicking the selected cell again
  takes one; shift-click still takes a stack at once; right-click still takes one at once.
- Stations: four NEW blocks, built like the Crafting Terminal: Network Forge, Network Chemistry
  Station, Network Campfire, Network Cement Mixer. Each `Extends` its vanilla station (same model,
  window, recipes, fuel, heat) and carries the existing marker property
  `StashNetCraftingTerminal`, so every Crafting Terminal hook and server check applies unchanged.
  Recipe (my call, same shape as the Crafting Terminal's): the vanilla station + 1 Storage
  Terminal + 4 electrical parts, crafted in the backpack. Not learnable (like the Crafting
  Terminal). Look (my call): vanilla model; icon tinted with the StashNet tint; name and
  description say it uses storage. They connect when inside a Core's reach; the code lock applies.
- Station output: a button on each StashNet station (the four new ones and the Crafting Terminal) switches
  "finished items go into storage" on/off. **On by default.**
- Repair: pulls the repair kit from storage while a network window is open (a Storage Terminal or
  a StashNet station or the Crafting Terminal; the backpack and its Repair button show there).

## Approaches
- Info panel: (chosen) feed the game's own item info window (`XUiC_ItemInfoWindow.SetInfo` with
  our cell as the controller and no vanilla action list) and add our own action buttons. Rejected:
  drawing our own info panel (would never match vanilla stats/mods display).
- Stations: (chosen, owner) new StashNet blocks that reuse the Crafting Terminal's marker and
  hooks. Rejected by the owner: connecting the vanilla stations themselves (free upgrade for
  every station near a Core, can't tell which are connected). Also rejected: taking ingredients
  straight from drives (bypasses vanilla crafting, dupe risk).
- Repair: (chosen) the same count + fetch-shortfall path as crafting, triggered by the vanilla
  Repair action. Rejected: repairing anywhere in Core reach (owner picked the window-open rule).

## How it works
- **Info panel.** Selecting a cell builds the display `ItemStack` (v0.18.0) and shows it in the
  info panel with buttons **Take** (takes the chosen amount) and **Take stack**. The selected cell
  stays highlighted. The panel clears when the item is gone from storage or the terminal closes.
- **Exact amount.** While a cell is selected, the terminal footer shows the item name and an
  amount box with - / + arrows (shift = +-10), typed input allowed, capped at what's stored and
  what fits in the backpack; Take uses it. Default 1. Withdraw requests carry the amount, split
  across the cell's members (v0.18.0 path).
- **Stations.** Nothing new in code: the marker property makes the window open as a network
  station and the server accept its requests (Core reach + player within 8 blocks), exactly as for
  the Crafting Terminal. While open, the existing crafting hooks count network stock, cap the craft
  count to what fits in the backpack, and fetch the shortfall on Craft ("click Craft again" for
  joined players).
- **Output toggle.** A button in the station window: "Finished items: into storage / stay here".
  The server holds the setting per station position (only the "off" ones are saved, in a small
  StashNet file in the world's save folder) and changes it through a new request type. When on,
  the existing output mover (window open: client; closed: server tick) applies to that station.
  Cancel refunds follow the same switch.
- **Repair.** While a network window is open, the item counts the Repair button checks include
  stored repair kits; on Repair, a missing kit is fetched into the backpack first, then vanilla
  repairs as usual (joined players may need to click Repair again, like Craft).

## Left to the builder
Exact layout of the footer amount box and the toggle button (screenshot to the owner); the
save-file format; the exact icon tint; how the info panel's action buttons
are injected; tests for game-free parts (amount capping, station-output setting store).

## Revision 1 (2026-11-14, v0.19.2)
The output switch is removed (owner: "the output button does not work lets get rid of it"). StashNet stations always send finished items and cancel refunds into storage, like the Crafting Terminal.
