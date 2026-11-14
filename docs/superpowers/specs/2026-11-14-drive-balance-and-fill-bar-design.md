# Drive balance + fill bar: design (short)

Date: 2026-11-14 · Branch: `m9-release` · Size: small change

## Why
The owner feels the High-Density Drive is overpowered **because of how much it holds**: an Armored bay full of them is 4,608 stacks. They also want each drive tier to cost 2 drives of the tier below. They want to tell at a glance whether a drive has items on it and how full it is.

## Goals
1. Drive capacities go from 24 / 96 / 384 to **24 / 72 / 216** stacks. The owner picked option A, with 3× steps. Crafting 2 drives into 1 drive of the next tier then gives 1.5× the stacks.
2. Standard Drive recipe = **2 Basic Drives** + the current extras. High-Density recipe = **2 Standard Drives** + the current extras.
3. **Only empty drives** count as crafting ingredients, so crafting can never destroy stored items.
4. A **fill bar** on every drive slot: the vanilla wear bar, which shows wherever an item slot is drawn (backpack, toolbelt, bays, loot bags).
5. The **fill percentage shown in the middle of the slot**, on top of the icon.
6. The drive's **item description** explains the bar and percentage and gives exact numbers.

## Non-goals
- No new or recolored drive icons (owner: too much art for little gain).
- No unlock gate (skill level or schematic) on the High-Density Drive, because capacity was the problem.
- Heat weights (1/2/4), bays, Cores and the other recipe ingredients don't change.

## Approaches considered
- **Capacity:** A, 24/72/216 (picked); B, cut HD only to 192 (an HD upgrade would barely be worth it); C, 2× steps, 24/48/96 (upgrades would only save bay slots).
- **Fill display:** wear bar (picked) + percentage in the middle (owner's addition); percentage in the corner; an LED on the icon (needs 9 new icons and shows only 3 states).

## Design
- **Capacity:** `StashNetMaxStacks` in `Mod/Config/items.xml` → 24 / 72 / 216.
- **Recipes:** `Mod/Config/recipes.xml`. Add `stashnetDriveBasic` ×2 to the Standard recipe and `stashnetDriveStandard` ×2 to the High-Density recipe. Keep the other ingredients and the workbench requirement.
- **Empty-only ingredients:** vanilla matches ingredients by item type only. Patch ingredient counting and removal so that a drive holding items, or one we can't read, is neither counted nor consumed. The rule is the same as `DriveScrapGuardPatch`'s. This covers the backpack, toolbelt and the Crafting Terminal's network stock. Drives can't be stored in the network, but the guard checks there too.
- **Fill bar:** override `XUiC_ItemStack`'s `hasdurability` / `durabilityfill` / `durabilitycolor` bindings for items tagged `stashnetDrive`:
  - shown only when the drive holds items;
  - fill = used / max stacks, capped at 1;
  - color green below 75%, yellow from 75%, red when full (builder's call on exact shades; use vanilla's palette).
  - Display only: drives keep no `MaxUseTimes`, so repair kits and wear never apply.
- **Percentage:** a new centered label in the item stack template, `Mod/Config/XUi_InGame/templates.xml` via XPath (vanilla's is in `Data/Config/XUi_InGame/templates.xml`), bound to a new binding such as `stashnetfilltext`. Text is like "66%". It's empty for empty drives and non-drives. Small outlined font so the icon still shows.
- **Description:** the static description (Localization) explains the bar and percentage. The item info panel adds a live line: "142 / 216 stacks used (66%) · 37 different items".
- **Over-full drives** (a High-Density Drive from before this change holding more than 216 stacks): it keeps everything and stays readable. It takes nothing new: `DriveContents` already clamps free stacks at 0. The bar shows full and red; the text shows the real percentage, e.g. "133%".
- **Unreadable drive data:** the bar is hidden and the text shows "?".
- **Performance:** cache the computed fill per drive data string. Slots redraw often, and parsing a drive takes work.
- **Multiplayer:** drive data travels with the item, so each client computes the bar itself (stack sizes from its own item classes). There's no new network traffic.

## Left to the builder
The exact Harmony targets for ingredient counting and removal, where the live info line hooks in, and the label's font size and position.

## Test notes
If the owner's test world has a High-Density Drive holding more than 216 stacks, it becomes over-full, which is a good test of the over-full rule. If not, fill one with `sn` dev commands before deploying.

## Revision 1 (2026-11-14, owner): five AE-style tiers
Replaces Goals 1-2. The fill bar, percentage, description line and empty-only ingredient guard stay as built (v0.11.0).

- **Tiers** (owner picked option "E" shape, ×4 then ×3, starting at 48): 1K 48 / 4K 192 / 16K 576 / 64K 1,728 / 256K 5,184 stacks. Each tier = **3 empty drives of the tier below** + parts. Gating comes from cost: a 256K is 81 1K drives' worth.
- **Recipes** ("cost what we were going to make it cost"): 1K = today's Basic recipe (backpack). 4K = 3×1K + 5 forged iron, 8 electrical, 2 mechanical. 16K = 3×4K + 5 forged steel, 15 electrical, 4 mechanical. 64K = 3×16K + 10 forged steel, 25 electrical, 8 mechanical. 256K = 3×64K + 20 forged steel, 40 electrical, 12 mechanical. All but 1K at a workbench. (64K/256K parts are Claude's proposal, tunable.)
- **Names** (owner): display names "1K Drive" … "256K Drive". Claude's call: internal item IDs stay (`stashnetDriveBasic`/`Standard`/`HighDensity` = 1K/4K/16K) so existing drives in saves keep working; new IDs `stashnetDrive64K`, `stashnetDrive256K`. Every tier grows, so no existing drive becomes over-full.
- **Heat weights** 1/2/4/6/8 (Claude's call). Scrap `Weight` 2/4/8/16/32, EconomicValue 60/240/600/1500/4000.
- **Icons** (owner): five new worn 3D drive icons from `tools/icons/draw-drive-icons.ps1`. The label shows the stack count (48 … 5184; owner, 2026-11-14: not the tier name), each tier has its own tape color, and the face gets heavier plating as tiers rise (64K and 256K get new treatments).
