# Shared Network Summary Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Station automation, the finished-items mover and heat decide from one live per-Core summary and read the real drives only when they move items.

**Architecture:** Game-free `NetworkSummary` (counts, free stacks, heat weight) built from an open `NetworkStorage`. Server cache `NetworkSummaryCache` keyed by Core: refreshed for free after every real read (`TryOpen`) and every `SaveDirty`; marked stale by a cheap per-bay slot-reference check in the bay feature's `UpdateTick`; rebuilt at most once a minute as a backstop. Automation uses a lazy `StationStock` that answers counts from the summary and opens the real network on the first take.

**Tech Stack:** C# netstandard2.0 core + xUnit, Harmony-free game glue.

**Spec:** `docs/superpowers/specs/2026-11-16-shared-network-snapshot-design.md`

## Global Constraints
- Server only; never touches XUi. Items move only on a fresh real read (`TryOpen`); never lose items.
- Backstop rebuild 60 s; bay check every 2 s per bay, skipped while the bay is open; stagger first station check by random 0–5 s.
- Terminals and open windows unchanged (exact numbers).

## Review Focus
- A bay opened by a player (skipped by `TryOpen`) must not cause a rebuild every 2 s while open; one rebuild after it closes if its drives changed.
- After StashNet's own `SaveDirty`, the bay check must NOT see a mismatch (fingerprints re-recorded), else every take costs a second read.
- A summary showing more than storage holds: the take finds less, does today's behaviour, and the summary is corrected by that read.
- Network with no automated stations: bay checks return immediately (no cache entries).
- Station whose Core was removed: lookup cache expires (10 s) and no read happens without a Core.

---

### Task 1: NetworkSummary (core)
**Files:** Create `src/StashNet.Core/Network/NetworkSummary.cs`; Test `tests/StashNet.Core.Tests/NetworkSummaryTests.cs`
**Produces:** `NetworkSummary.From(NetworkStorage s, float heatWeight)`; `int Plain(string name)` (variant == null), `int Total(string name)` (all variants), `int FreeStacks`, `float HeatWeight`; `static bool NeedsRebuild(bool stale, float builtAt, float now, float backstopSeconds)`.
- [ ] Tests: `CountsPlainAndTotalSeparately`, `FreeStacksIsMaxMinusUsed`, `NeedsRebuildWhenStaleOrOld` (stale → true; age 61 s with backstop 60 → true; age 5 s → false).
- [ ] RED, implement, GREEN, commit "Core: network summary".

### Task 2: Summary cache, bay check, perf counters (game)
**Files:** Create `src/StashNet/Network/NetworkSummaryCache.cs`, `src/StashNet/Network/StashNetPerf.cs`, `src/StashNet/Commands/` `sn perf` subcommand (follow the existing `sn` command file); Modify `StashNetNetwork.cs` (time + count every `TryOpen`; record bays `(Vector3i pos, TEFeatureStorage)` incl. skipped/open ones as null; call `NetworkSummaryCache.Record(this)` at the end of a successful `TryOpen` and of `SaveDirty`, server only), `Blocks/TEFeatureStashNetDriveBay.cs` (`UpdateTick` → `NetworkSummaryCache.CheckBay`).
**Produces:** `NetworkSummaryCache.TryGet(World, Vector3i stationPos, out Vector3i corePos, out NetworkSummary)`, `Record(StashNetNetwork)`, `CheckBay(World, TileEntityComposite bay)`, `MarkStale(Vector3i corePos)`; `StashNetPerf.Read(ms)`, `.Hit()`, `.Rebuild(reason)`.
- [ ] Fingerprint = the `ItemValue` references of every slot; recorded for each bay of the network on `Record`; `CheckBay` (2 s throttle, skip while `IsUserAccessing`, return at once when the cache is empty) compares and marks owning entries stale on mismatch; an unknown bay with any drive inside a cached Core's reach marks it stale.
- [ ] Build succeeds; commit "Network summary cache, bay change check, sn perf".

### Task 3: Automation, mover and heat use the summary
**Files:** Create `src/StashNet/Stations/StationStock.cs`; Modify `Stations/StationAutomation.cs`, `Crafting/CraftingOutputServer.cs`, `Heat/HeatEmitter.cs`.
- [ ] `StationStock`: `Stored(name, plainOnly)` from summary until opened, from the real network after; `Extract/Insert` open the real network on first use (`TryOpen`, null network → 0); `Save()` = `SaveDirty`; `CoreLock` from `NetworkLock.GetCoreLock(world, corePos)`.
- [ ] Automation: replace `net` by `StationStock`; first check per station delayed by random 0–5 s.
- [ ] Mover: skip when the summary says `FreeStacks == 0`. Heat: use summary `HeatWeight` instead of opening the network.
- [ ] Build + full suite; commit "Automation reads the shared summary; real reads only to move items".

### Task 4: Release
- [ ] Checklist steps (automation still works; `sn perf` shows few reads with 2+ forges idle at target; drive moved by hand picked up within ~2 s), CLAUDE.md, `tools/release.ps1 -Version 0.23.0`, final review, commit.
