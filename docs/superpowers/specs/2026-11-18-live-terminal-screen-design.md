# Live Storage Terminal screen: design

Date: 2026-11-18. Status: draft for the owner's approval.

## Intent

Today the Storage Terminal's CRT shows a fixed green picture ("DRIVES ....... OK", sprite `crtScreen`
from the skin atlas, `SkinApplier.SetScreens`). The owner wants it to show the network's live state, so
that walking past a terminal tells you how the network is doing without opening it. It should also
glitch now and then, like a salvaged CRT. The vanilla shader is named `GameScreenFlicker`, but the owner
sees no flicker on our TV, so the glitch is ours to build.

## What the screen shows (owner)

```
 STASHNET            [LOCKED]
 STACKS  1,284 / 2,304
 [██████████████░░░░░░░] 56%
 DRIVES  14   (3 FULL)
 SYSTEM HEAT         WARM
```

- **Stacks used / total, with a fill bar.** Bar colors match the drive fill bar (`DriveFill`): green,
  yellow from 75%, red when full.
- **Drives: count, and how many are full.** A drive counts as full at 100% of its stacks, the point where
  its own fill bar turns red. *(Claude's call: the screen fits about five lines, and a network can have
  30+ drives, so it shows a summary instead of a list.)*
- **Heat:** *(Revision 2026-11-18, owner: the bar read like the area's heat, which also counts forges etc.; now the label "SYSTEM HEAT" and only the word, right-aligned.)* Originally a 6-segment bar plus a word (CALM / WARM / HOT), measured as the Core's reported heat over its
  cap (`StashNetHeatPerDrive` × weight, capped at `StashNetHeatCap`, the same numbers as `sn heat`).
  *(Claude's call: thresholds CALM < 40% ≤ WARM < 80% ≤ HOT.)*
- **Lock:** `[LOCKED]` when the Core's lock is locked, otherwise `[OPEN]`. It shows whether a code is
  set, not who is allowed in.
- **Who sees the numbers (owner, 2026-11-18):** when the network is locked, only players allowed through
  the lock see numbers. Everyone else sees a glitching screen with just `[LOCKED]`. Owner's condition:
  it must cost the server and clients essentially nothing extra (see section 2, "Locked watchers").
- **Dropped by the owner:** number of different items.
- **No Core in reach:** `NO SIGNAL` with heavy static, no numbers. *(Claude's call.)*

## Goals

- The screen updates on its own while you're nearby, within a couple of seconds of a change.
- Cheap with many screens. Seven terminals on one network cost about the same as one.
- Light on the server (owner, v0.23.0: "as light on the server as possible"). No traffic while nothing changes.
- Works the same in single-player, on a host, and as a joined player on a dedicated server (the owner
  plays on a hosted supercraft server).
- A cheap glitch, on its own random timing per TV.

## Non-goals

- Per-drive lists, item lists, or who's allowed through the lock.
- Live screens on anything other than the Storage Terminal (the Network stations have no screen).
- Changing the terminal window or any other UI.

## Anti-goals (failures even if it works)

- Reading drives just to update a screen, beyond what the summary already does.
- Steady per-player polling, or per-frame drawing.
- Breaking the pooled-model safety in `SkinOwner` (a reused model must get vanilla materials back, and our
  textures must never be deleted out from under another TV).
- Any server-side code touching rendering or UI (dedicated servers have none).

## Approaches considered

- **A. Watch and push (owner picked).** A client tells the server which terminals it can see; the server
  sends each watching client that network's screen numbers once, then only when they change. No
  traffic while idle, works the same everywhere, and plugs into the existing `NetworkSummaryCache`
  refresh points. Costs a small server-side watch list.
- **B. Client polls every few seconds.** Simpler, but there's steady traffic and server work even when
  nothing changes, multiplied by every player near a base. Against the owner's "light on the server" ask.
- **C. Server draws the picture and sends it.** Dropped: images are far larger than a few numbers, and a
  dedicated server has no rendering.

## Design

### 1. Screen numbers (game-free, `StashNet.Core`)

A small value type `ScreenState`: `HasCore`, `UsedStacks`, `MaxStacks`, `Drives`, `FullDrives`,
`HeatFraction` (0–1, rounded to the 6 bar segments so tiny heat changes don't resend), `Locked`. It has
equality (resend only when it differs), and a binary write/read for the package. Unit-tested.

`NetworkSummary` gains `UsedStacks`, `MaxStacks`, `FullDrives`, computed in `NetworkSummary.From` from
the in-memory `NetworkStorage` (no extra drive reads). Unreadable/skipped drives count in `Drives` as
today; they can't be counted as full.

### 2. Server: watch list and push (`Network/ScreenWatch.cs`, server only)

- **Watch / unwatch.** New package `NetPackageStashNetScreen` (client → server: watch/unwatch a terminal
  position; server → client: terminal-network state). Single-player and host call the server code
  directly, like `TerminalServer`.
- The server checks that the position is a Storage Terminal and that the player passes the range sanity
  bound (below). It then maps the terminal to its Core
  (`NetworkSummaryCache`'s core lookup) and records the watcher under that Core. Watches are per Core, and
  each player is listed once per Core, however many of its terminals they can see.
- **On watch:** reply right away with the current `ScreenState` (from the summary; this may build the
  summary once if there isn't one).
- **On change:** `NetworkSummaryCache.Record` (already called after every real read and save) tells
  `ScreenWatch` that this Core changed. `ScreenWatch` builds the new `ScreenState` and sends it only to
  that Core's watchers, and only if it differs from what was last sent. Sends are throttled to at most
  one per network per second, with the last state always sent (a burst of deposits gives one update).
- **Changes StashNet doesn't make itself:**
  - *Drive moved by hand:* the bay check already marks the summary stale every 2 s. For a Core with
    watchers, the server rebuilds a stale summary at most once every 5 s and never while one of its bays
    is open (the existing `TryOpen` rules). *(Claude's call: without this, a stale summary only refreshes
    when something asks for it.)*
  - *Lock set or cleared:* reading the Core's lock is one bool, so `ScreenWatch` checks it every 2 s for
    Cores with watchers.
  - *Heat:* heat comes from the summary's weight, so it changes with drive changes.
  - *Core removed / out of reach:* the 10 s core lookup notices it, and watchers get `HasCore = false`.
- **Locked watchers.** Each Core's watchers are kept in two groups: *allowed* and *shut out*. A player is
  sorted once, when they start watching (`NetworkLock.IsAllowed` with their platform ID), and all
  watchers are re-sorted only when the lock changes (see below). Number changes go to the allowed group
  only. The shut-out group got `[LOCKED]` once and needs nothing more until the lock changes, so locked
  bases send *fewer* messages than open ones. The 2 s lock check compares the lock state plus the allowed
  list's count and contents (a handful of IDs), only for Cores with watchers.
- **Watch range.** A TV watches for as long as its model is active, which in this game means its chunk is
  loaded. The server's distance check is only a sanity bound (Claude's call: 400 blocks, above any view
  distance), so a watch is never refused for a TV the player can actually have loaded.
- **Cleanup:** a player's watches are dropped on disconnect. A Core's entry is dropped when its last
  watcher leaves.

### 3. Client: drawing (`Skins/TerminalScreen.cs`)

- When a terminal's model is skinned (`SkinApplier.Apply` for `stashnetTerminal`), the client sends
  *watch*. When the model is switched off (`SkinOwner.OnDisable`: chunk unloaded, block removed,
  model pooled), it sends *unwatch*.
- The client keeps **one texture per network** (keyed by Core position from the server's reply), the
  same size as `crtScreen` (256×192). Every TV on that network points its own per-renderer screen
  material (the `(Instance)` material `SetScreens` already uses) at that shared texture. Seven TVs share
  one texture.
- The texture is redrawn only when a new `ScreenState` arrives and differs. Drawing is game-free pixel
  work in `StashNet.Core` (a small bitmap font plus bars, unit-testable), then one upload to the GPU.
- The shut-out `[LOCKED]` picture holds no numbers, so it's identical for every locked network: **one
  texture for the whole game**, drawn once, shared by every TV the player is locked out of. It glitches
  more often than a live screen. *(Claude's call.)*
- Until the first reply arrives, the TV shows today's `crtScreen` picture.
- A network's texture is destroyed when no TV uses it any more. `SkinOwner.Restore` keeps putting the
  vanilla picture back on reuse, so pooled models never keep our texture.

### 4. Glitch (client)

- Along with the clean picture, each network also gets **two damaged copies** drawn at the same time
  (scrambled line bands, color split, static). They're shared by all its TVs, like the clean one.
- Each TV runs its own timer, with a random quiet gap (Claude's call: 8–30 s; owner asked for more often 2026-11-18 → paint.xml 4–15 s, then 2–8 s with random glitch kinds: flicker, jolt, roll, tear, stretch; random strength; a third come in bursts; 4 broken copies per set (`GlitchRun`)). A glitch lasts 0.1–0.4 s
  and does one or more of:
  - nudges the material's texture offset (sideways jolt / vertical roll). This is per TV, costs nothing,
    and needs no redraw;
  - swaps to one of the damaged copies for a few frames, then back.
- `NO SIGNAL` glitches constantly (static cycling through the damaged copies).
- Glitch timing and strength are set in `Mod/Skins/paint.xml` on the terminal's `<block>` (e.g.
  `glitchGap`, `glitchLength`, `glitchStrength`), so they can be live-tuned with `sn skin reload` like
  the rest of the look. *(Claude's call, fits the existing tuning workflow.)*
- Only one small component per TV runs the timer, and it does nothing between glitches except count
  down. TVs in unloaded chunks don't exist and cost nothing.
- The screen texture is set to clamp at its edges, so the roll/jolt switches it to repeat while it's
  shifted, or draws the shift into the damaged copies (builder's choice).

### 5. Compatibility

- The version handshake is exact (`VersionCheck`), so a new package is safe: server and clients always
  run the same version.
- Nothing is saved. Watches live in memory only.
- With the skin turned off (no `screen=` in `paint.xml`), nothing is watched and the TV stays vanilla.

## Constraints

- 7 Days to Die V3.4 b10. Client-only code never runs on a dedicated server (`GameManager.IsDedicatedServer`).
- Game-free logic (screen state, drawing, glitch frames) lives in `StashNet.Core` with xUnit tests.
- Version bump only through `tools/release.ps1 -Version`; deploy only with the owner's OK, game closed.

## Left to the builder

- The exact bitmap font (reuse atlas glyphs or a tiny built-in 5×7 font) and pixel layout, matching the
  sketch.
- Package layout and names, and how the watch list is stored.
- How the damaged copies are generated, within the "cheap, drawn once per change" rule.

## Testing

- Unit tests: `ScreenState` equality/serialization, `NetworkSummary` new counts, full-drive counting,
  heat fraction bucketing, drawing writes the expected pixels in a few spots.
- In-game checklist (owner): screen shows the numbers; deposit/withdraw updates within ~2 s; moving a
  drive by hand updates within ~5 s; set/clear the code updates `[LOCKED]`; a player not on the allowed list sees only `[LOCKED]`, and sees
  numbers right after entering the code; break the Core → `NO SIGNAL`;
  seven terminals on one network all agree; glitches happen on separate timing; on the supercraft
  server, a second player sees the same; `sn perf` shows no extra rebuilds while idle.
