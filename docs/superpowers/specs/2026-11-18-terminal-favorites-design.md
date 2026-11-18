# Storage Terminal favorites (pins): design (short)

Date: 2026-11-18. Approved by the owner in conversation ("its per person", "do it").

## Intent
Players want a few items (ammo, food, fuel, their tools) always at the top of the Storage Terminal.

## What it does
- Select an item in the terminal: the vanilla item info panel shows **Pin** (or **Unpin**) next to Take / Take stack
  (`ui_game_symbol_star`).
- Pinned items come first in the grid, then the rest in the chosen sort order. Tabs and search still filter, so a pin
  only shows in the tabs it belongs to.
- Pinned cells show a small star in the bottom-left corner (top-left = item type symbol, right side = count).
- A pin is the **item name**: pinning 9mm ammo pins all 9mm ammo; pinning a pistol pins every pistol of that kind.

## Per player (owner)
Saved on the player's PC in `<game user data>/StashNet/favorites.txt` (one item name per line), shared by every
world and server. The server never sees it. Unknown names (an item from a removed mod) are kept but never match.

## Approaches considered
- **Per player on the PC (chosen, owner):** no server cost; each player pins their own things.
- Shared per network on the server: everyone sees the same pins; anyone could change them for others.

## Left to the builder
File read/write details (read once on first use, write on every change, never crash on a bad file), button label
keys, star sprite size/tint.
