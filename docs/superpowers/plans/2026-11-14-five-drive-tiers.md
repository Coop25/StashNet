# Five Drive Tiers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Replace the 3 drive tiers with 5 (1K–256K), each crafted from 3 empty drives of the tier below, with new names and icons.
**Architecture:** XML + localization + icon script only; no C# logic changes (drives are found by tag, capacity/heat by item properties).
**Tech Stack:** 7DTD XML, PowerShell System.Drawing icon script.
**Spec:** `docs/superpowers/specs/2026-11-14-drive-balance-and-fill-bar-design.md` (Revision 1)

## Global Constraints
- Stacks 48/192/576/1728/5184; heat weights 1/2/4/6/8; recipes exactly as Revision 1.
- Keep IDs `stashnetDriveBasic`/`stashnetDriveStandard`/`stashnetDriveHighDensity`; new `stashnetDrive64K`, `stashnetDrive256K` (tag `stashnetDrive`, Stacknumber 1).
- Icons are PNGs named after the item ID in `Mod/UIAtlases/ItemIconAtlas/`; the script stays deterministic.

## Review Focus
1. A drive in an old save (any of the 3 old IDs) still loads with its items: IDs unchanged.
2. The new items have every property the old ones have (DescriptionKey, sounds, mesh), or they'd show/handle oddly.
3. The craft-count and empty-only guards work with count 3 (they read the ingredient count, not a fixed 2).

### Task 1: Items, recipes, names
- [ ] items.xml: caps/weights/Weight/EconomicValue per constraints; add the two new items (copy of the HD block); update the header comment.
- [ ] recipes.xml: ingredient counts 2→3; add 64K and 256K recipes.
- [ ] Localization.csv: names "1K Drive"… "256K Drive"; descriptions per tier (size in stacks + "about N crates", "Crafted from three empty X Drives", heat line, the shared unload/bar sentences).
- [ ] Build → 0 errors; `xmllint`-style check that the XML parses (PowerShell `[xml](Get-Content ...)`).
- [ ] Commit.

### Task 2: Icons
- [ ] Script: five `Drive3D` calls with labels 1K…256K, tape colors (blue, green, purple, orange, red-gold), new face treatments for '64k' (hazard band + steel plates + extra bolts) and '256k' (dark armored face, gold trim, double hazard).
- [ ] Run it twice; the second run's hashes match the first (deterministic).
- [ ] Look at all five PNGs.
- [ ] Commit.

### Task 3: Docs, checklist, version
- [ ] README, PLAN.md, CLAUDE.md tier lines; checklist updated (5 tiers, 3-drive recipes, no over-full case, patch count unchanged at 27); version 0.12.0.
- [ ] Build, `dotnet test`, commit; final review.
