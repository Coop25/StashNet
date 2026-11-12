# Milestone 6: Crafting Terminal Implementation Plan

> **For agentic workers:** Implement task by task (superpowers:executing-plans). Steps use checkbox (`- [ ]`) syntax.

**Goal:** A Crafting Terminal block that **is a vanilla workbench** (same screen, recipes, perk gates, queue, output), except that its recipes can use ingredients stored in the network.

**Architecture:**
- **Block:** `Extends="workbench"` with `WorkstationName=workbench` (same recipes, same "correct workstation" checks) and `WorkstationWindow=workstation_workbench` (the vanilla screen), plus marker `StashNetCraftingTerminal`. The recipe needs a vanilla workbench + a Storage Terminal, so it can't skip progression.
- **Network stock (client):** while a Crafting Terminal's workstation window is open, `CraftingNetwork` keeps a listing of the network's **plain** items (variants such as tools are excluded: recipe ingredients are plain), refreshed every 2 s and after crafts, through the existing `List` request (the server validates the crafting terminal position too).
- **"What you have" includes the network** (only while that window is open), via Harmony postfixes on `XUiM_PlayerInventory.GetItemCount(ItemValue|int)`, `GetAllItemStacks` (returns a fresh list, safe to append), `HasItems`, and a prefix on `XUiC_RecipeList.BuildRecipeInfosList` (craftable sorting).
- **Taking ingredients:** vanilla only ever takes from the backpack/toolbelt. When you click Craft (`ItemActionEntryCraft.OnActivated` sets a flag), the `HasItems` postfix computes the **shortfall** against raw backpack+toolbelt counts and **withdraws it from the network into the backpack** (normal Withdraw protocol). Then vanilla takes the ingredients as usual.
  - Single-player/host: the withdraw is synchronous, so the craft proceeds in the same click.
  - Remote client: the withdraw is async, so this click is refused with "Fetching ingredients from storage…" and the next click crafts from the backpack. (Multiplayer is untested.)

**Spec:** `docs/PLAN.md` §2.5 (Crafting Terminal), roadmap row 6.

## Global Constraints
- No changes to vanilla crafting rules: recipes, unlocks, tiers, crafting time, queue, and output all stay vanilla. Only "what counts as having the ingredients" changes.
- All hooks are inert unless a StashNet Crafting Terminal's window is open (a plain workbench is unaffected).
- A locked network (the player isn't allowed) gives an empty listing → behaves exactly like a plain workbench.
- Never lose items: ingredients move network → backpack through the existing Withdraw path (backpack overflow → Return → storage or drop).

## Review Focus
1. **A plain vanilla workbench is unaffected** (counts and crafting as before).
2. **Partly in backpack, partly in network:** only the shortfall is pulled. Totals are correct afterwards.
3. **Craft count > 1** (the spinner): pulls shortfall × count.
4. **Backpack full when pulling:** the pull partially fails → the craft is refused (vanilla "missing resources"); no loss (Return path).
5. **Cancelling a queued craft** refunds to the backpack (vanilla behavior; the ingredients were in the backpack when taken).

### Task 1: Block + server validation
- `blocks.xml`: `stashnetCraftingTerminal` (Extends workbench; WorkstationName/Window; marker; tinted icon; description).
- `recipes.xml`: workbench 1 + stashnetTerminal 1 + resourceElectricParts 4 (backpack).
- `TerminalServer.IsValid`: accept `BlockStashNetTerminal` **or** a block with `StashNetCraftingTerminal`.

### Task 2: `CraftingNetwork` (client cache + shortfall fetch)
- `Open(pos, group)`, `Close()`, `Tick(dt)`, `OnResponse(resp)`, `Count(ItemValue)`, `Stacks()`, `TryFetchShortfall(XUiM_PlayerInventory inv, IList<ItemStack> needs, int multiplier) -> bool`.

### Task 3: Harmony hooks
- `XUiC_WorkstationWindowGroup.OnOpen/OnClose/Update` → Open/Close/Tick when the station is ours.
- `XUiM_PlayerInventory.GetItemCount` ×2, `GetAllItemStacks`, `HasItems`; `XUiC_RecipeList.BuildRecipeInfosList`; `ItemActionEntryCraft.OnActivated` (click flag, try/finally).

### Task 4: v0.7.0, checklist, docs, commit
