# Station automation: auto-stock, auto-fuel, auto-extinguish (design)

Date: 2026-11-15. Branch: `m10-automation` off `master`. Target: v0.20.0.

## Intent

The owner wants the Network stations (Network Workbench, Forge, Chemistry Station, Campfire,
Cement Mixer) to run themselves:

1. **Auto-stock:** "keep N of this item in storage". When the network holds fewer, the station
   crafts more from stored ingredients.
2. **Auto-fuel:** stations that burn fuel top themselves up from storage, with the fuel type
   chosen per station (owner, 2026-11-15).
3. **Auto-extinguish:** a lit station with nothing to do puts its fire out (saves fuel, and a lit
   fire makes heat the whole time it burns).

The owner approved the overview and asked to build and test it; the calls below marked
"(Claude's call)" were stated to the owner and can be changed after playtesting.

## Facts from the game (V3.4 b10, decompiled)

- Fuel module is used by forge, campfire, chemistry station (`Modules` contains `fuel`).
  Workbench and cement mixer have no fuel: auto-fuel/extinguish don't apply to them.
- `TileEntityWorkstation`: `fuel[3]`, `tools[3]`, `input[3 + materials]`, `output[6]`,
  `queue[4]` (`queue[3]` is the item being crafted; new items fill from the end, like
  `XUiC_CraftingQueue.AddRecipeToCraft`), `isBurning` / `IsBurning` (setter marks modified).
- `HandleRecipeQueue` does nothing while a player has the station open (`bUserAccessing`), or
  for a fuel station that isn't burning. A burning station burns fuel and emits heat even with an
  empty queue.
- Forge smelting: raw ore sits in `input[0..2]`; melted metal tallies sit in the last
  `materialNames.Length` input slots and stay there. "Smelting pending" = any of the first
  `input.Length - materialNames.Length` slots non-empty, and only when `XUiM_Recipes.DisableSmelter`
  is off. (Vanilla `InputIsEmpty()` also counts tallies >= 10, so don't use it.)
- The Craft button (`ItemActionEntryCraft.OnActivated`) queues a copy of the recipe with
  `count = GetRecipeCraftOutputCount`, `craftingTime = GetRecipeCraftTime`, `craftingTier`, and
  per-craft ingredient amounts after the player's perks (`CraftingIngredientCount` effect, input
  modifier). All of these depend on the player.
- Tile entities only tick in loaded chunks, so automation only runs while a player is nearby.

## Design

### Station settings (server-held)

Per station, saved by the server in `<save>/StashNet/stations.txt`, keyed by position and block
name (a different block at that spot = no settings; settings are also dropped when the station
is removed, so a new station never inherits them). Game-free model + text format in
`StashNet.Core` (`StationSettings`), with tests.

- **Fuel mode:** `Manual` (default; vanilla behaviour, nothing automatic) / `Auto` (light and put
  out the fire automatically, use only fuel already in the station) / a fuel item name (Auto +
  refill with that item from storage). Only for fuel stations.
- **Stock rules:** up to 8 per station (Claude's call). Each rule: output item name, target amount,
  and the recipe exactly as the Craft button would queue it, captured on the setting player's game:
  recipe name, output count per craft, craft time per item, crafting tier (= quality for quality
  items), per-craft ingredient list (name + amount). Plus the setter's platform ID (for the lock).

The client computes the recipe data, as vanilla clients already compute crafts (the mod's trust
model: same as vanilla). The server checks only that the recipe exists and belongs to this
station's crafting area.

### Server tick (`StationAutomation`, from the existing `TileEntityWorkstation.UpdateTick` postfix)

Throttled to once every 5 s per station (Claude's call), server only, skipped while a player has
the station open or `LockManager` has it locked. Order:

1. **Auto-stock**, for each rule in order, while a queue slot is free and at least one queue slot
   stays free for manual crafts (Claude's call):
   - The rule's setter must still pass the Core lock (`NetworkLock.IsAllowed`), else skip.
   - have = network count of the output item (all variants) + amount already queued for it in this
     station + amount in this station's output slots.
   - If have < target: crafts = ceil((target - have) / output count), capped by what the stored
     ingredients allow and by the queue item's multiplier limit. Needs the recipe's tool
     (`craftingToolType`) in the station's tool slots, else skip.
   - Withdraw the ingredients from the network first (save drives), then put a `RecipeQueueItem`
     into the free slot (StartingEntityId = -1: **no crafting XP** for automatic crafts, Claude's
     call to avoid AFK XP farming). If the queue write fails, put the ingredients back.
2. **Fire** (fuel stations, mode != Manual):
   - work = recipe in queue, or smelting pending.
   - Mode is a fuel item: if there's work, top the fuel slots up to one full stack in total from
     storage (Claude's call), filling empty slots or slots of the same item; never replace other
     fuel already in the slots.
   - If work and fuel (burn time left > 0) and not beside water and not burning: light it.
   - If no work and burning: put it out.
   - Output still flows into the network via the existing `CraftingOutputServer`.

Never lose items: ingredients/fuel leave storage only after the destination is known to have room;
drives are saved before the station is marked modified.

### UI (client)

- **Fuel button** in the fuel window header, only at Network stations with fuel: shows the
  current mode; click = next mode. Order: Manual → Auto → each fuel item currently in storage
  (sorted by name) → Manual. Tooltip explains the mode.
- **Auto-stock panel** in the crafting queue area (all Network stations): "Keep in stock" with the
  selected recipe, an amount box (vanilla counter), Add/Update button, and the station's rule list
  (item icon or name, stored now / target, remove button).
- Settings travel with two new terminal ops (`SetFuelMode`, `SetStockRule` add/update/remove) and a
  station-settings field on every response to a station's request (like the removed output switch).

### Not in scope

- Rules shared between stations: two stations with the same rule may both craft (accepted).
- Running while nobody is nearby.
- The optional power/fuel running cost for the network (separate, parked).
- Any change to manual crafting.

## Approaches considered

- **Server-side automation on the station tile entity (chosen).** Works with the window closed,
  for every player, on dedicated servers. Needs recipe data captured on the client.
- Client-side automation while a player has the window open: simple, but vanilla pauses crafting
  while the window is open, so it would do nothing useful.
- A separate "auto-crafter" block: more art/config, and the owner asked for it on the stations.

## Testing

- `StashNet.Core` tests: settings parse/serialize round-trip, bad data refused, fuel-mode cycling
  order, the shortfall/craft-count math.
- In-game checklist `docs/testing/automation-checklist.md`: rule crafts when below target and stops
  at target; respects tools, the lock, and a full network; fuel button cycles; auto-refuel uses the
  chosen fuel; forge stays lit while smelting and goes out after; manual mode unchanged; settings
  survive save/reload; a removed and re-placed station starts with no settings; joined player.
