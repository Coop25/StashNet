# Terminal cells: vanilla markings and combined look-alike items (design)

Date: 2026-11-14. Target version: v0.18.0. Short design (small change).

## Intent
The owner wants the Storage Terminal's item cells to read like backpack slots and to stop
spreading identical one-per-slot items across many cells.

1. **Vanilla markings.** Show the same corner symbol a backpack slot shows: read/known for
   books, recipes, schematics and magazines; "modded", boosted-star and combined "+" on gear;
   block type symbols. Match the backpack's outlined count text and its red "lost max
   durability" bar.
2. **Combine look-alikes.** Engines, dyes, mods, chickens and similar items show as ONE cell
   with a count ("Engine x6") when they are identical except for the game's hidden random
   seed. Storage is unchanged: each stored item keeps its own data and still uses its own
   stack slot.

## Goals / non-goals / anti-goals
- Goal: a stack of identical things reads as one pile, like stone does today.
- Non-goal: changing what drives store, capacity math, or the stack-slot count.
- Anti-goal: handing a player an item different from what the cell showed (worse hidden
  stats). That is why gear with quality never combines (owner's pick: "No-quality items only").
- Anti-goal: anything that can duplicate or lose items.

## Approaches considered
- **A (chosen): keep our cells, add vanilla markings via the game's own helper**
  (`XUiBindingHelper.GetItemTypeIconValue / GetHasItemTypeIconValue / GetItemTypeIconTintColor`
  on a display `ItemStack`). The markings always match the backpack; our click handling stays.
- B: reuse the vanilla `item_stack` slot controller. Identical look, but it brings drag/swap/drop
  behavior that fights withdraw clicks and risks duplicates. Rejected.
- Grouping where: **client-side display grouping** (chosen) vs. changing storage to drop the
  seed. Changing storage would destroy data; rejected.

## How it works
- **Group key** (game glue, next to `ItemCodec`): decode the variant; if the item has no quality
  (`Quality == 0`), the key is the name + the variant re-encoded with `Seed = 0`. Otherwise the
  key is the exact variant (no combining). Durability (`UseTimes`), mods, metadata and stats all
  stay in the key, so a worn engine and a fresh one stay separate.
- **Listing:** the terminal folds the server's listing by group key. A cell = total count + the
  list of real (name, variant, count) members. Sorting, search, tabs and "N different items"
  work on cells as today (the server's distinct count is unchanged: it already counts by name).
- **Withdraw from a cell:** click = 1, shift-click = one stack (for one-per-slot items, that is 1),
  right-click = 1, exactly as now. The client splits the amount across the cell's members in
  order and sends ONE Withdraw request carrying several items; the server extracts each (it
  already only gives what it actually removed). If another player took some meanwhile, you get
  fewer, never extra. Server change: Withdraw accepts more than one item.
- **Markings:** each cell builds a display `ItemStack(decoded value, count)` and binds the
  vanilla corner icon (`ui_game_symbol_*`, its tint), the perma-durability red background, and an
  outlined count, laid out like the backpack slot.

## Left to the builder
Exact pixel positions inside the 58-px cell, caching of decoded values/keys per listing refresh,
and tests for the game-free parts (splitting a withdraw amount across members).
