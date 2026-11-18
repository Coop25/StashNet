# Terminal Favorites Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Per-player pinned items in the Storage Terminal: Pin/Unpin in the info panel, pinned first, star marker.
**Architecture:** Game-free `Favorites` (set of item names, text format, pinned-first ordering) + client glue (file in the user data dir, an info-panel action entry, render order, a cell binding).
**Tech Stack:** C# netstandard2.0 + xUnit; 7DTD XUi.
**Spec:** `docs/superpowers/specs/2026-11-18-terminal-favorites-design.md`

## Global Constraints
- Client only; nothing sent to or saved on the server. Never crash on a missing/bad file. Version stays 1.1.0 (rebuild zip with `tools/release.ps1 -Version 1.1.0`). Never deploy without the owner's OK.

## Review Focus
1. Bad or missing favorites file → empty pins, no exception. → Task 1 `ParseIgnoresBlankAndBadLines`.
2. Pinning while a tab/search filters → pinned item still respects the filter. → Task 2 manual (checklist F3).
3. Pinned order stays in the chosen sort among pins. → Task 1 `PinnedFirstKeepsOrder`.

### Task 1: Favorites (game-free)
Files: `src/StashNet.Core/Protocol/Favorites.cs`, `tests/StashNet.Core.Tests/FavoritesTests.cs`.
Produces `public sealed class Favorites { bool Contains(string name); bool Toggle(string name) /*returns pinned now*/; string Serialize(); static Favorites Parse(string text); static List<T> PinnedFirst<T>(IEnumerable<T> items, Func<T,string> name, Favorites f); }` — Serialize: sorted names, one per line; Parse: trims, skips blank lines and lines starting with '#', ordinal names.
- [ ] Tests: `ToggleAddsThenRemoves`, `SerializeParseRoundTrip`, `ParseIgnoresBlankAndBadLines` (null, "", whitespace, '#'), `PinnedFirstKeepsOrder` (stable within both parts).
- [ ] RED → implement → GREEN → full suite → commit.

### Task 2: Client glue
Files: `src/StashNet/UI/FavoritesStore.cs` (static: lazy load from `Path.Combine(GameIO.GetUserGameDataDir(), "StashNet", "favorites.txt")`, `Save()` on change, try/catch + one Log.Warning), `src/StashNet/UI/StashNetPinAction.cs` (BaseItemActionEntry, label `stashnetPin`/`stashnetUnpin`, sprite `ui_game_symbol_star`; OnActivated → `XUiC_StashNetTerminal.Current?.TogglePinSelected()`), `XUiC_StashNetTerminal` (ShowInfo adds the action; `TogglePinSelected` toggles, re-shows info, Render; Render applies `Favorites.PinnedFirst` after sorting), `XUiC_StashNetEntry` binding `stashnet_pinned`, `Mod/Config/XUi_InGame/windows.xml` star sprite in the cell (bottom-left, 16 px, depth 7, `[white]`-ish tint, `visible="{stashnet_pinned}"`), `Mod/Config/Localization.txt` keys `stashnetPin`/`stashnetUnpin`.
- [ ] Build 0 errors/0 warnings; full tests; commit.

### Task 3: Checklist + zip + notes
- [ ] Checklist section "Favorites (1.1.0)": F1 pin an item → star + first; F2 unpin → back in sort order; F3 pinned item on another tab/search only shows where it belongs; F4 restart the game → pins kept; F5 on the server: pins are yours only.
- [ ] `tools/release.ps1 -Version 1.1.0`; CLAUDE.md RESUME line; commit.
