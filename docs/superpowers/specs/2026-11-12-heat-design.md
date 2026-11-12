# Milestone 7: Heat (short design)

## Intent
A storage network should cost something in stealth. The Core gives off heat that feeds the game's own heat system (the same one forges and campfires feed, which brings screamers). Bigger networks run hotter, but a cap keeps big bases at "screamers visit now and then," never "endless screamers." (From `docs/PLAN.md` section 2.7.)

**Target (owner picked "B", 2026-11-12):** A full early network (about 3 bays, 12 Basic drives) **on its own never** brings screamers. It does make other heat sources (forge, crafting) bring them noticeably sooner. A big late-game network at the cap reaches the screamer line by itself, slowly.

## How the game's heat works (decompiled, V3.4 b10)
- Heat adds up per area of 5×5 chunks (80×80 blocks). Workstations report `HeatMapStrength` about every **167 real seconds** (`AIDirector.GetActivityWorldTimeDelay`), with each report fading over 720 s. Repeated reports from one spot build up to about **4.3× one report**.
- When an area reaches **25**, the game rolls a **20%** chance for screamers and clears the area's heat. It then pauses 4 min (22 min if screamers came).
- Vanilla strengths: forge 6, workbench/campfire/chem station 5, torch 1. A forge alone builds to about 26, so it rolls about every 36 min (~3 h per screamer visit on average).
- Heat is ignored during blood moons and when the heat setting is off. The world's heat-sensitivity setting scales it. All of this is handled by vanilla.

## Approaches
1. **Heat part on the Core (picked).** A new tile-entity feature on the Core, ticked by the game like any workstation. On the forge's rhythm it counts the network's drives and reports heat at the Core. It matches vanilla exactly, only runs while the base is loaded, and runs server-side only. Existing Cores load fine, because the composite save format skips features that are missing or new.
2. Global server timer tracking every Core. Rejected: we'd have to track Cores through chunk load/unload/place/break.
3. Heat only on terminal use. Rejected: a full network you walk away from would be silent, which goes against the plan.

## Design
- **Formula** (game-free, in `StashNet.Core`, unit-tested): `heat per report = min(drives × HeatPerDrive, HeatCap)`. 0 drives = no report.
- **Scrap Core starting numbers** (placeholders, in `blocks.xml` on the Core like `StashNetRadius`):
  - `StashNetHeatPerDrive = 0.25`, `StashNetHeatCap = 6.5`.
  - 12 drives → 3 per report → builds to ~13. Alone: never reaches 25. With a forge: rolls about every 11 min instead of 36.
  - At the cap (26+ drives) → builds to ~28. Alone: rolls about every 25 min.
- **Core tiers (M8):** tier Cores get their own two values (lower per-drive heat = "runs cooler"). Nothing extra to build now.
- **New feature** `TEFeatureStashNetHeat` on `stashnetScrapCore`: in `UpdateTick` (server only), it uses the vanilla throttle pattern (`emitHeatMapEvent`'s world-time delay). It counts drives via the existing network scan and calls `aiDirector.NotifyActivity(Campfire, corePos, heat)`.
- **Terminal bump:** while a Storage Terminal is being used, it adds **0.5** at the terminal on the same rhythm (server side, throttled per terminal). The Crafting Terminal already gets workbench heat (5) while crafting, so nothing new there.
- **Test helper:** console command `stashnet heat`. It shows the nearest network's drive count, heat per report, and the current heat level of the area you stand in, so testing doesn't mean waiting for screamers blind.

## Non-goals
- No Heat Sink, no powered tiers (parked in the plan).
- No heat UI on the terminal.

## Left to the builder
How the drive count is cached (a fresh scan every ~167 s is fine), and exact log/console wording.
