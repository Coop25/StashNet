# Station controls: always-open Automation tab, fuel picker, melt targets, output switch, quality ingredients

Date: 2026-11-16. Branch `m10-automation` (builds on v0.21.0). Target v0.22.0.

## Why
Owner, after playing v0.21.0 ("definitely better"):
1. The ⚡ Automation tab can't be reached until a recipe is selected (the vanilla recipe box only
   shows then), so the fuel setting and the rule list are hidden behind an unrelated click.
2. The forge should keep its smelting stocked from storage, like keep-in-stock does for crafts.
3. Choosing the fuel with ◄ mode ► arrows isn't intuitive.
4. Bring back the output on/off switch. Owner's only use: "if I'm crafting something that I want to
   stay in my inventory". (The old switch was removed in v0.19.2 as "not working"; the build the owner
   had was v0.19.0, whose button vanished after a click. v0.19.1 fixed that 4 minutes before the
   removal and was most likely never tried.)
5. Bug: vehicle recipes at Network stations can't pull an engine or a battery from storage.

## Goals
- Everything a station can be told to do lives in one place: the ⚡ tab.
- The tab works with or without a selected recipe.
- The forge stays topped up with melted metal, only from raw meltables, and stops on its own.
- Quality ingredients (engine, battery) come out of storage for hand crafting.

## Non-goals
- Vanilla workstations and backpack crafting: unchanged.
- Auto-stock rules for recipes that need quality ingredients (vehicles): still refused
  (`StockRuleCapture` returns null), unchanged.
- No change to how heat, locks, the terminal or drives work.

## Anti-goals
- The forge must never melt something that isn't on the raw list (weapons, tools, parts, junk the
  player is keeping).
- Crafting must never use up a weapon/tool/item that has mods or cosmetics attached.
- Nothing may break the vanilla recipe box for other stations (v0.20.0 lesson: our UI inside vanilla
  windows catches its own failures).

## Approaches (owner picked A)
- **A. Everything in the ⚡ tab** (chosen): fuel, output, keep-in-stock and melt targets in one tab.
  Busy at a forge; can be split later if it feels crowded.
- B. Melt targets on the forge's own metal counters in the smelting panel: next to the number they
  control, but patches the vanilla smelting window (fragile) and splits settings across two places.
- For the tab-without-recipe part (owner picked B of that question): always show the recipe box at
  Network stations, rather than auto-selecting the first recipe (which would select something the
  player didn't pick).

## What the player sees

The ⚡ tab at a forge:

```
 [Ingredients] [Description] [⚡]
 Keep [Iron Pipe] in stock      ◄ 100 ►  [Keep]
 Keep  ◄ Iron ►  melted at      ◄ 2000 ► [Keep]   ← forge only
 ───────────────────────────────────────────
 Iron Pipe          87 / 100      ✕
 Iron (melted)   2000 / 2000      ✕
 ───────────────────────────────────────────
 Fire:    (Myself) [Auto]                        ← fuel stations only
 Refill:  (none) [🪵 Wood 312] (⚫ Coal 40)       ← only when Fire = Auto
 Finished items: [To storage] (Stay here)
```

### 1. Always-open tab
- At a Network station the recipe box is visible from the moment the window opens, even with no
  recipe selected, and the ⚡ tab can be opened. Vanilla stations keep vanilla behaviour (empty info
  window until a recipe is picked).
- With no recipe: the keep line reads "Pick a recipe to keep it in stock" and its Keep button is
  inactive; melt, list, fire, refill and output all work.
- The vanilla tabs (Ingredients/Description) still need a recipe; with none selected, opening the
  box at a Network station shows the ⚡ tab.

### 2. Fuel picker
- **Fire: Myself / Auto** (two buttons, selected one highlighted). Myself = today's Manual. Auto =
  today's Auto.
- **Refill from storage** (only with Fire = Auto): "none" plus one button per fuel item currently in
  storage (plain items with a fuel value), each with its item icon and stored count, sorted by name.
  Clicking one = today's fuel-item mode. If the chosen fuel isn't in storage right now it still shows
  (count 0) so the setting stays visible.
- The explanation line for the current mode stays (existing localization).
- Saved format unchanged: `FuelModes` Manual / Auto / fuel item name. ◄ ► buttons removed.
  `FuelModes.Next/Previous` can go if nothing else uses them.

### 3. Melt targets (forge only, i.e. stations with the material-input module)
- One target per metal: Iron, Brass, Lead, Glass, Stone, Clay (the forge's `InputMaterials`). Up to
  six, separate from the 8 keep-in-stock rules. "Keep ◄ metal ► melted at ◄ amount ► [Keep/Update]";
  each target shows in the list as "<Metal> (melted)  <tally> / <target>  ✕", green when reached.
- The target is the melted amount (the forge's own counter, e.g. `unit_iron`), capped at that unit's
  max (30,000 in vanilla).
- **Raw list** (balance XML, `Mod/Config/` like the other balance numbers), in priority order per
  metal:
  - Iron: `resourceScrapIron`
  - Brass: `resourceScrapBrass`
  - Lead: `resourceScrapLead`
  - Glass: `resourceBrokenGlass` first, then `resourceCrushedSand` (owner: broken glass first; its
    only vanilla use is melting)
  - Stone: `resourceRockSmall`
  - Clay: `resourceClayLump`
  Only these items are ever moved into the forge by automation. Only plain (non-variant) stacks.
- **Feeding (server tick, with the other automation):** for each target, `pending` = units the raw
  items already in the ore slots will add (item weight × count, for items of that metal).
  If `tally + pending < target`, pull `ceil((target − tally − pending) / weight)` of the first raw
  item on the list that's in storage (then the next), limited by free room in the ore slots (stack
  size per slot) and so the tally can't pass the unit's max. Nothing pulled when the target is
  reached → it stops on its own. Items leave storage first, the rest is put back if it doesn't fit
  (same "never lose items" rule as auto-stock).
- Auto fire already lights while there's meltable ore and goes out when idle; with Fire = Myself the
  forge is fed but the player lights it.
- Respect the existing guards: skipped while someone has the station open, locked, or Disable
  Smelter is on (no melting then, so no feeding); network lock/owner check like stock rules.

### 4. Output switch
- "Finished items: To storage / Stay here", every Network station, default To storage, remembered
  per station on the server.
- Stay here = vanilla: finished crafts stay in the Output slots (both movers skip: the client's
  window-open mover and the server's `CraftingOutputServer`); cancelled crafts refund to the backpack.
- Auto-stock already counts items in Output, so it won't over-craft; with Output full, vanilla stalls
  and auto-fire treats that as no work (existing `OutputFits`).
- Auto-stock crafts also stay in Output while the switch is off (owner can say if they'd rather
  those still go to storage).

### 5. Quality ingredients (bug fix)
- Hand crafting at a Network station (and the craft-count max, ingredient counts, Craft click
  shortfall fetch) also counts stored items that have quality, when nothing is attached (no mods, no
  cosmetics). The shortfall fetch withdraws specific stored variants, **lowest quality first**.
- Applies to every recipe ingredient with quality (vehicle engine `smallEngine`, `carBattery`, etc.).
- Items with mods or cosmetics are never counted or pulled.

## Saving and protocol (builder notes)
- All in the existing per-station settings (`StationSettings` text, `<save>/StashNet/stations.txt`,
  sent to clients on every station reply). New lines: `output off` (only written when off) and
  `melt <metal> <target> <owner>`. `IsDefault` must account for both. Old files load unchanged.
- Melt targets store who set them, exactly like stock rules (Claude's call, for consistency): the
  owner's platform ID is saved, checked each tick against the Core's lock (`OwnerAllowed`), and
  stripped from what clients receive. If that player loses access, the forge stops pulling from
  storage.
- New ops alongside `SetFuelMode`/`SetStockRule`/`RemoveStockRule`: set output, set melt target,
  remove melt target (or one op with a flag; builder's call). Server validates metal against the
  station's `materialNames` and clamps the target.
- Removing a station already drops its settings (`StationRemovedPatch`), so the switch and targets
  go with it (fixes v0.19.0's "new station inherits off" gap).
- Quality withdraw: reuse the existing Withdraw with the variant string from the station listing.

## Constraints
- Game V3.4 b10, vanilla models/windows; our UI elements inside vanilla windows must fail safe.
- Server is the only writer of drives; server code never touches XUi.
- Game-free logic (melt-feed math, settings parsing, variant pick order) goes in `StashNet.Core` with
  tests.
- The 447 × 303 tab area must fit the forge layout above; if it doesn't, the builder may shrink the
  rule list (e.g. scroll or fewer visible rows) but not move settings out of the tab.

## Left to the builder
- How to force the recipe box visible with no recipe (vanilla `XUiC_RecipeList.SelectedEntry` setter
  / `XUiC_CraftingInfoWindow` + `emptyInfoWindow`), and making vanilla bindings safe with a null recipe.
- Exact XUi layout, icons, colors, localization text.
- Op shapes, test list, checklist steps for the owner.

## Revision 1 (2026-11-16, after the owner's first look at v0.22.0)
Owner: "the UI is getting a bit crowded", the Fire / Refill / Finished items buttons didn't respond, and
the melt row should use the game's own names.
- **Two tabs** (owner OK'd the split): ⚡ **Stock** = keep in stock, keep melted, the list (10 rows per
  page); 🔥 **Station** = Finished items (+ a line saying what it does), Fire (+ explanation line, back
  from tooltips), Refill (2 rows, up to 14 fuels). Finished items is first, so the Workbench and Cement
  Mixer (no fire) show it at the top. With no recipe, the tab last used at that kind of station (block
  name, this game run) opens, else Stock (`AutomationTabState`).
- **Buttons fixed:** the click buttons over the highlight sprites were fully transparent; NGUI skips
  widgets with alpha < 0.001 when deciding what was clicked (`UICamera`), so they never got clicks. They
  now use alpha 3/255.
- **Metal names** = the forge window's own labels (`lbl<metal>`: Iron, Brass, Lead, Sand, Stone, Clay),
  like `XUiC_WorkstationMaterialInputWindow`.

## Revision 2 (2026-11-16, owner)
- **Use both ore slots:** melt feeding spreads each metal's raw items evenly over the slots already holding
  that item plus the empty ones (`MeltFeed.Spread`), so both slots melt at once. When several targets
  need feeding, each later target with no slot yet keeps one empty slot (two metals = one slot each).
- **Refill keeps 100, not a stack:** block property `StashNetFuelRefill` (100 on the three fuel
  stations; default 100), at most one stack (`FuelRefill.Want`). Owner: "maybe ten... maybe a hundred";
  Claude picked 100.
