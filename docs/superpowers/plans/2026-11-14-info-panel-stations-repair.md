# Info panel, stations, repair Implementation Plan (v0.19.0)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Inspect stored items in the vanilla info panel and take exact amounts; four StashNet stations that craft from storage with an on/off "output into storage" switch; repair kits pulled from storage.

**Architecture:** Stations reuse the Crafting Terminal's marker property (`StashNetCraftingTerminal`), so every crafting hook and server check applies. The output switch is server-held per station position (game-free `StationOutputSettings`, a new `SetStationOutput` op, a `StationOutput` byte on every response). Repair reuses `CraftingNetwork` counting + shortfall fetch, guarded so vanilla only ever consumes backpack kits. The info panel is fed through `XUiC_ItemInfoWindow.SetInfo(stack, cell, None)` plus our own action entries.

**Tech Stack:** C#, Harmony, 7DTD XUi/XML, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-14-info-panel-stations-repair-design.md`

## Global Constraints
- Block names `stashnetForge`, `stashnetChemistryStation`, `stashnetCampfire`, `stashnetCementMixer`; display "Network Forge" etc.
- Recipe: vanilla station + 1 `stashnetTerminal` + 4 `resourceElectricParts`, no craft_area.
- Output switch default ON; only OFF positions are saved.
- Never consume network items without them first arriving in the backpack (no free crafts/repairs).
- Version via `tools/release.ps1 -Version 0.19.0`.

## Review Focus
- Repair with a network window open on a remote client: never repairs for free; "click again" when the kit is in flight.
- Existing bug: repairing while the Crafting Terminal is open counted network kits but consumed only backpack kits (free repair). Must be fixed.
- Output switch OFF: window-open mover, closed-window server mover and cancel refunds all leave items alone.
- Info panel after the selected item leaves storage (another player took it): panel clears, Take does nothing harmful.
- Amount box: never above stored count; typing non-numbers or 0.

---

### Task 1: Four StashNet stations
Files: `Mod/Config/blocks.xml`, `recipes.xml`, `Localization.csv`, `tools/icons/draw-block-icons.ps1` (+ generated PNGs in `Mod/UIAtlases/ItemIconAtlas`).
- Each block: `Extends` the vanilla station (exclude UnlockedBy, DescriptionKey, CustomIcon, CustomIconTint, SortOrder1, SortOrder2, TraderStageTemplate), `StashNetCraftingTerminal=true`, explicit `WorkstationWindow` (`workstation_forge`, `workstation_chemistryStation`, `workstation_campfire`, `workstation_cementMixer`), `<property class="Workstation"><property name="CraftingAreaRecipes" value="<vanilla name>"/></property>`, DescriptionKey, SortOrder like the Crafting Terminal.
- [ ] Build passes; XML well-formed.

### Task 2: Station output switch
Files: Create `src/StashNet.Core/Network/StationOutputSettings.cs`, tests `StationOutputSettingsTests.cs`; modify `TerminalMessages.cs` (+tests), `TerminalServer.cs`, `CraftingNetwork.cs`, `CraftingOutputServer.cs`, `windows.xml`, new `UI/XUiC_StashNetStationOutput.cs`, Localization.
- `StationOutputSettings`: `bool IsOn(int x,int y,int z)`, `void Set(x,y,z,bool on)`, `string Serialize()`, `static StationOutputSettings Parse(string)` (lines "x,y,z"; bad lines skipped).
- Protocol: `TerminalOp.SetStationOutput = 6`; `TerminalRequest.Flag` (bool, written after Items); `TerminalResponse.StationOutput` (byte: 0 n/a, 1 on, 2 off, written last).
- Tests: settings default on / set off / round-trip / bad lines; codec round-trips Flag and StationOutput.
- Server: file `<save dir>/StashNet/station-output.txt`, loaded lazily, saved on change; responses from a Crafting-Terminal-marked block carry StationOutput; SetStationOutput sets it.
- Client: CraftingNetwork keeps last StationOutput; mover and refund skip when off. Server closed-window mover skips when off.
- UI: button in vanilla `windowOutput` header (left of move-all), visible only while CraftingNetwork.Active, tooltip says the state; click sends SetStationOutput(!current).

### Task 3: Repair from storage
Files: `src/StashNet/Crafting/RepairPatches.cs`, `CraftingNetwork.cs`, `UI/XUiC_StashNetTerminal.cs`, Localization.
- Storage Terminal open → `CraftingNetwork.OpenForRepair(pos)` (counts apply only inside repair checks); close → Close.
- `ItemActionEntryRepair.RefreshEnabled` prefix/postfix sets `CraftingNetwork.InRepairCheck` (counts include stock).
- `ItemActionEntryRepair.OnActivated` prefix: compute kits needed (vanilla formula), `TryFetchShortfall`; if not all in the backpack, tooltip "stashnetFetchingRepair" and skip; during vanilla OnActivated set `CraftingNetwork.SuppressStock` so counts are raw.
- [ ] Build passes.

### Task 4: Info panel + amount
Files: `src/StashNet.Core/Protocol/TakeAmount.cs` (+tests), `UI/XUiC_StashNetTerminal.cs`, `UI/XUiC_StashNetEntry.cs`, `UI/StashNetTakeAction.cs`, `windows.xml`, Localization.
- `TakeAmount.Clamp(int requested, int stored)` → 1..stored (0 when stored 0); `TakeAmount.Step(int current, int delta, int stored)`.
- Click unselected cell = select + show info; click selected cell = take 1; shift/right unchanged. Selection kept by group key across listing refreshes; cleared when gone.
- Info panel: `ClearSelectedStacks`, `makeVisible(true)`, `SetInfo(stack, entry, None)`, add `StashNetTakeAction` ("Take" uses the amount, "Take stack").
- Footer: selected name, [-] amount [+] (shift = 10), typed input; selected cell highlighted.

### Task 5: Release
- [ ] `tools/release.ps1 -Version 0.19.0`; checklist section; CLAUDE.md note; final review subagent; commit.
