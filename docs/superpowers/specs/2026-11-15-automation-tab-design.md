# Automation tab + Network station category tabs (short design)

Date: 2026-11-15. Branch `m10-automation`. Target v0.21.0. Approved by the owner ("do it").

## Why
Owner, after v0.20.2: the AUTOMATION panel under Output "seems kind of tacked on", it runs off the
bottom of the screen at the forge, and the fuel button doesn't explain itself. Also the Network
stations' recipe category bar is empty ("bare without them").

## What
1. **Automation is a third tab in the recipe box** (vanilla `craftingInfoPanel`), next to the
   Ingredients and Description icons, shown only at Network stations. It replaces the ingredient
   area (447 × 303) with: "Keep <selected recipe> in stock: ◄ amount ► [Keep/Update]", the list of
   this station's rules ("item stored / target ✕", up to 8, green when stocked), and for fuel
   stations a fuel row "Fuel: ◄ mode ►" plus a sentence explaining the current mode:
   - Manual: "You light it yourself, like a normal <station>."
   - Auto: "Lights itself when there's a craft or ore to melt, goes out when idle. Uses the fuel you put in."
   - Fuel item: "... and refills <fuel> from storage (one stack)."
   The panel window under Output (`stashnetAutomation`) is removed.
2. **Category tabs:** the game picks a station's category list by its block name
   (`XUiC_CategoryList.SetupCategoriesByWorkstation(blockName)` → ui_display.xml
   `crafting_category_list display_type`). Each Network station gets a `StashNetCategories`
   property naming its vanilla station (workbench, forge, campfire, chemistryStation, cementMixer);
   a Harmony prefix swaps the name, so vanilla list changes carry over.

Behaviour, protocol, saving and the server tick are unchanged.

## Approaches
- Third tab in the recipe box (chosen, owner: "do the one you think"): built-in look, room for
  explanations, works on the selected recipe; you must click the tab to find it.
- A title-bar button swapping the recipe list for a full page: most room, but hides the recipe list
  you pick from.
- Keep the panel under Output: doesn't fit at the forge.

## How (builder notes)
- Tab state: the vanilla window has its own `TabType` (Ingredients/Description/UnlockedBy) driving
  `showingredients/showdescription/showunlockedby` bindings. A postfix on
  `XUiC_CraftingInfoWindow.GetBindingValueInternal` answers "false" for those while our tab is open;
  pressing a vanilla tab button closes ours; `SetRecipe` (it re-selects Ingredients) keeps ours open
  and deselects the vanilla buttons. Closed when the window closes.
- Fuel ◄ ► goes both ways: `FuelModes.Previous` next to `Next` (tested).
- The recipe box only shows once a recipe is selected, so the tab (and the fuel row) needs a
  selected recipe. Accepted (Claude's call): stations always open with a recipe list to pick from.
