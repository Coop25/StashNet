# Shared live network summary for station automation (short design)

Date: 2026-11-16. Branch `Coop25/continue-stashnet-m9-release`. Target v0.23.0. Approved by the owner.

## Why
Owner plans ~15 forges and wants the storage network "as light from a server's perspective as we possibly
can". Today every Network station with automation re-reads the whole network (find Core, scan bays,
decode every drive) every 5 s and sums storage several times per check; the finished-items mover reads it
again. Owner: "only one thing reading the system, and the networked items poke it for the data", served
live, not rebuilt on a timer. When a station takes items the served data must reflect it; no races —
stations queue up, get the items if they're there, else do what they normally do.

## Approaches (owner picked A, live)
- **A. Shared read-only summary per Core, kept live** (chosen): stations decide from the summary; only a
  station that actually moves items reads the real drives, then updates the summary.
- A, rebuilt every 5 s: simpler, but stale between rebuilds and still reads the network ~once per 5 s per Core.
- B. One live shared network all writes go through: fastest, but every path that changes drives must
  keep it right, or items are lost/duplicated.

## What
- **Summary** (game-free, `StashNet.Core`): per Core, stored counts of plain items by name (what automation
  needs: ingredients, raw scrap, fuel) and free stacks. Built from one real network read.
- **Kept live:** every storage change that goes through StashNet on the server applies its delta to the
  summary at once: automation takes and put-backs, terminal deposits/withdrawals/returns/drive unloads,
  hand-craft pulls, cancel refunds, the finished-items mover. (Simplest correct form: changes made
  through `NetworkStorage` on an open network update that Core's summary from the network after the op.)
- **Rebuilt once** (on the next station check) when a bay's contents change outside StashNet (drive put in
  or taken out by hand, bay placed / broken / upgraded) — hook the bay.
- **Backstop:** a full rebuild at most once a minute per Core, only when a station asks (owner: keep it).
  It bounds how long a summary that shows too little (another mod moved items, admin command) can leave
  stations idle. A summary that shows too much corrects itself on the next real read.
- **Decide from the summary, move from the drives:** stock rules, melt targets and fuel refill compute
  "anything to do?" from the summary (and the station's own slots). Only "take N of X" opens the real
  network (fresh read, as today), extracts, places, puts back what didn't fit, then refreshes the summary
  from that same read (no extra read).
- **No races:** the server ticks stations one at a time on one thread, so takes are naturally queued. A
  station that finds less than the summary promised takes what's there (melt, refill) or puts everything
  back and skips (a craft needs all ingredients) — today's behaviour.
- **Stagger:** each station's first check is offset by a random 0–5 s.
- **Finished-items mover** (closed stations): skips its read when the summary says storage is full.
- **Station → Core lookup** cached (like `CoreLookupCache`), so the decide step reads no chunks at all.
- **`sn perf`**: real network reads (count, total and average ms), summary hits, rebuilds by reason (first,
  bay changed, backstop), since the last `sn perf reset`. Server-side.

## Non-goals
- Terminals and open station windows keep reading the real network per request (exact numbers there).
- No change to what automation does, only to how often it reads.

## Left to the builder
- Exact hooks for bay changes and where the summary refresh sits, `sn perf` format, tests.
