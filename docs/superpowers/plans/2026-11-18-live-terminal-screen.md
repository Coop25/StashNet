# Live Storage Terminal Screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The Storage Terminal's CRT shows its network's live stacks, drives, heat and lock state (numbers hidden from players locked out), with a cheap random glitch.

**Architecture:** Server keeps a watch list (who sees which Core's screen), pushes a tiny `ScreenView` only when it changes (throttled 1/s per network), from the existing `NetworkSummaryCache` refresh points. Client draws one texture set per network (clean + 2 damaged copies), shared by every TV of that network; one shared set for "locked out", one for "no signal". A per-TV component nudges texture offset / swaps to a damaged copy on a random timer.

**Tech Stack:** C# (netstandard2.0 game-free `StashNet.Core` + xUnit; game glue against 7DTD V3.4 b10 Assembly-CSharp, Unity), NetPackage.

**Spec:** `docs/superpowers/specs/2026-11-18-live-terminal-screen-design.md`

## Global Constraints

- Game-free logic goes in `src/StashNet.Core/` with tests in `tests/StashNet.Core.Tests/`; game glue stays thin.
- Server code never touches XUi/rendering/`EntityPlayerLocal` (dedicated servers have none). Client code returns early on `GameManager.IsDedicatedServer`.
- No drive reads for screens beyond the existing summary rebuild rules. No per-frame drawing; no polling from clients.
- Screen size 256×192 RGBA, rows bottom-up (Unity `SetPixelData` order; `TextureRepaint.Crop` already produces this).
- Never "K" abbreviations in player-visible numbers; use thousands separators (`1,284`).
- Throttle: at most one push per network per 1 s, last state always delivered. Server checks (lock, core, stale) every 2 s. Stale rebuild for watched networks at most every 5 s. Watch range sanity bound 400 blocks.
- Heat: 6 segments, `segments = round(min(1, PerReport(weight, perDrive, cap) / cap) * 6)`; word CALM for 0–2, WARM 3–4, HOT 5–6.
- Glitch defaults (paint.xml overrides): gap 8–30 s, length up to 0.4 s, strength 0.6. Locked-out and no-signal screens glitch 3× as often.
- Build: `dotnet build src/StashNet/StashNet.csproj -c Release`. Tests: `dotnet test tests/StashNet.Core.Tests`. Never deploy without the owner's OK (game closed). Version bump only with `tools/release.ps1 -Version`.
- Commit messages end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (PowerShell: write message to a file, `git commit -F`).

## Review Focus

1. A terminal TV becomes active before the version handshake or before the local player exists (world load): its watch must be queued and sent later, never dropped. → Task 6 (ScreenLink pending set) + checklist step 1.
2. A player gains or loses access while watching (enters the code; owner clears the code; owner changes it): their screen flips between numbers and `[LOCKED]` within ~2 s. → Task 5 test `LockChangeResendsOnlyChangedViews` + checklist step 5.
3. Core broken or placed while terminals are watched: `NO SIGNAL` / numbers within ~10 s, watchers moved to the new Core. → Task 5 test `MovingTerminalToAnotherCoreMovesItsWatchers` + checklist step 6.
4. A terminal model is pooled and reused (block broken, chunk unloaded, `sn skin reload`): unwatch sent once, vanilla screen restored, a network texture is destroyed only when no TV uses it. → Task 7 refcount test in `TerminalScreenRefsTests` + checklist step 8.
5. A burst of deposits (shift-click 40 stacks): at most one push per second per network and the final numbers are right. → Task 5 test `DirtyCoreIsDueOncePerSecond` + checklist step 3.

---

### Task 1: Screen view and message codec (game-free)

**Files:**
- Create: `src/StashNet.Core/Protocol/ScreenMessages.cs`
- Test: `tests/StashNet.Core.Tests/ScreenMessagesTests.cs`

**Interfaces:**
- Produces:
  - `public readonly struct Pos3 : IEquatable<Pos3> { Pos3(int x,int y,int z); int X,Y,Z; }` (value equality, `GetHashCode`, `ToString` "x,y,z").
  - `public sealed class ScreenView : IEquatable<ScreenView>` with init-only-style public properties set via constructor: `bool HasCore, bool Shut, bool Locked, int UsedStacks, int MaxStacks, int Drives, int FullDrives, int HeatSegments` (0–6, clamped). Static `ScreenView NoSignal`; `static ScreenView ShutOut()` (HasCore true, Shut true, Locked true, numbers 0); `static string HeatWord(int segments)` → "CALM"/"WARM"/"HOT"; `static int HeatSegmentsOf(float heat, float cap)`.
  - `public enum ScreenOp : byte { Watch = 1, Unwatch = 2, Assign = 3, State = 4 }`
  - `public sealed class ScreenMessage { ScreenOp Op; Pos3 Terminal; bool HasCore; Pos3 Core; ScreenView View; }`
  - `public static class ScreenCodec { void Write(BinaryWriter, ScreenMessage); ScreenMessage Read(BinaryReader); }` — Watch/Unwatch carry Terminal; Assign carries Terminal + HasCore + Core; State carries Core + View. Unknown op → `FormatException`.

- [ ] **Step 1: Write failing tests** in `ScreenMessagesTests`:
  - `ViewsWithSameNumbersAreEqual` / `ViewsDifferingInOneFieldAreNotEqual` (try each field).
  - `HeatSegmentsRoundAndClamp`: `HeatSegmentsOf(0,6.5f)==0`, `(3.25f,6.5f)==3`, `(6.5f,6.5f)==6`, `(9f,6.5f)==6`, `(1f,0f)==0`.
  - `HeatWords`: 0,2→"CALM"; 3,4→"WARM"; 5,6→"HOT".
  - `EachOpRoundTrips`: write/read via `MemoryStream` for all four ops, assert all carried fields equal.
  - `UnknownOpThrows`: stream with op byte 99 → `Assert.Throws<FormatException>`.
- [ ] **Step 2:** `dotnet test tests/StashNet.Core.Tests --filter ScreenMessagesTests` → fails to compile (types missing).
- [ ] **Step 3:** Implement `ScreenMessages.cs` per Interfaces.
- [ ] **Step 4:** Same command → all pass.
- [ ] **Step 5:** Commit "Screen view and message codec".

### Task 2: Summary counts for the screen (game-free)

**Files:**
- Modify: `src/StashNet.Core/Network/NetworkStorage.cs` (add `UsedStacksOf`)
- Modify: `src/StashNet.Core/Network/NetworkSummary.cs`
- Test: `tests/StashNet.Core.Tests/NetworkSummaryTests.cs`

**Interfaces:**
- Produces: `NetworkStorage.UsedStacksOf(NetworkDrive d) -> int`; `NetworkSummary.UsedStacks`, `.MaxStacks`, `.FullDrives` (int, private set, filled in `From`). A drive is full when `MaxStacks > 0 && used >= MaxStacks`.

- [ ] **Step 1: Failing tests** in `NetworkSummaryTests` using the existing `Storage()` helper (2 drives × 10 stacks, 7 used):
  - `UsedAndMaxStacks`: `UsedStacks == 7`, `MaxStacks == 20`.
  - `CountsFullDrives`: build storage with one drive of capacity 3 holding 3 stacks of a 1-stack item and one half-used drive → `FullDrives == 1`; an over-full drive (4 stacks in capacity 3) also counts.
- [ ] **Step 2:** `dotnet test tests/StashNet.Core.Tests --filter NetworkSummaryTests` → compile failure.
- [ ] **Step 3:** Implement; compute each drive's used stacks once in `From` (reuse for `FullDrives`).
- [ ] **Step 4:** Tests pass (whole suite still green: `dotnet test tests/StashNet.Core.Tests`).
- [ ] **Step 5:** Commit "Network summary: used/max stacks and full drives".

### Task 3: Screen drawing (game-free)

**Files:**
- Create: `src/StashNet.Core/Skins/ScreenFont.cs` (5×7 bitmap glyphs for `A–Z 0–9 space / % [ ] ( ) . , :` ; unknown char draws nothing)
- Create: `src/StashNet.Core/Skins/ScreenPainter.cs`
- Test: `tests/StashNet.Core.Tests/ScreenPainterTests.cs`

**Interfaces:**
- Produces:
  - `public static class ScreenPainter { const int Width = 256, Height = 192; byte[] Draw(ScreenView v); static string Thousands(int n); }` — returns `Width*Height*4` RGBA, bottom-up rows.
  - Colors as `public static readonly (byte R, byte G, byte B)` fields: `Background (3,18,8)`, `Text (57,255,106)`, `BarHigh (255,210,58)`, `BarFull (255,74,58)`, `Dim (20,70,35)` (empty bar segments/frame).
- Layout (text scale 2 → glyph 10×14, advance 12 px; margin 10 px; line pitch 30 px, line 1 top at y=12 from the top):
  1. `STASHNET` left; `[LOCKED]` or `[OPEN]` right-aligned.
  2. `STACKS` left; percent right (`used*100/max`, floor, `0%` when max 0).
  3. Bar: full width between margins, 14 px tall, 2 px frame in `Dim`; fill color `Text` below 75%, `BarHigh` from 75%, `BarFull` when used ≥ max.
  4. `1,284 / 2,304` left.
  5. `DRIVES 14` left; `3 FULL` right (omitted when 0).
  6. `HEAT` left, then 6 boxes (12×14, gap 4) filled `Text` for each segment else `Dim`, then the word.
  - `Shut`: `STASHNET` small at line 1 and `[LOCKED]` at scale 4 centered vertically.
  - `!HasCore`: `NO SIGNAL` at scale 3 centered.

- [ ] **Step 1: Failing tests:**
  - `ThousandsSeparators`: `Thousands(0)=="0"`, `(1284)=="1,284"`, `(248832)=="248,832"`.
  - `DrawReturnsFullSizeBuffer`: length `256*192*4`, every alpha byte 255.
  - `BarFillMatchesPercent`: view 50/100 → pixel at bar row, x just left of the bar's midpoint is `Text` color, just right is not; view 80/100 → filled pixel is `BarHigh`; 100/100 → `BarFull`. (Write a test helper `Px(byte[] buf, int x, int yFromTop)` that flips to bottom-up.)
  - `LockedVsOpenDiffer` and `ShutHidesNumbers`: `Draw(Shut)` equals `Draw(ShutOut())` regardless of numbers fed (construct two shut views with different numbers → identical buffers).
  - `NoSignalDiffersFromLive`.
- [ ] **Step 2:** Run `--filter ScreenPainterTests` → compile failure.
- [ ] **Step 3:** Implement font + painter.
- [ ] **Step 4:** Tests pass.
- [ ] **Step 5:** Commit "Screen painter and bitmap font".

### Task 4: Glitch frames and paint.xml glitch settings (game-free)

**Files:**
- Create: `src/StashNet.Core/Skins/ScreenGlitch.cs`
- Modify: `src/StashNet.Core/Skins/PaintSpec.cs` (`BlockPaint`: `Live`, `GlitchMinGap`, `GlitchMaxGap`, `GlitchLength`, `GlitchStrength`)
- Test: `tests/StashNet.Core.Tests/ScreenGlitchTests.cs`, `tests/StashNet.Core.Tests/PaintSpecTests.cs`

**Interfaces:**
- Produces:
  - `ScreenGlitch.Damage(byte[] clean, int w, int h, int seed, float strength) -> byte[]` (new buffer; horizontal band tears, RGB channel split, sparse noise; deterministic per seed; strength 0 → identical copy).
  - `ScreenGlitch.Static(int w, int h, int seed) -> byte[]` (dim green noise, alpha 255).
  - `ScreenGlitch.NextGap(Random rng, float min, float max) -> float`, `ScreenGlitch.NextLength(Random rng, float max) -> float` (uniform in [0.1, max], max floored at 0.1).
  - `BlockPaint.Live` (`live="true"`), `GlitchMinGap=8`, `GlitchMaxGap=30`, `GlitchLength=0.4`, `GlitchStrength=0.6` (attrs `glitchMin`, `glitchMax`, `glitchLength`, `glitchStrength`; min > max → warning + swap).

- [ ] **Step 1: Failing tests:**
  - `DamageIsDeterministicAndDifferent`: same seed → equal; different seed → not equal; result ≠ clean at strength 0.6; `Damage(...,0f)` equals clean; input buffer unchanged.
  - `StaticIsOpaqueAndVaries`: all alpha 255, two seeds differ.
  - `GapAndLengthStayInRange`: 1000 draws with fixed seed stay within bounds.
  - In `PaintSpecTests`: `ReadsLiveAndGlitchSettings`, `GlitchDefaultsWhenAbsent`, `SwapsReversedGlitchGap` (warning added).
- [ ] **Step 2:** Run `--filter "ScreenGlitchTests|PaintSpecTests"` → fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Pass; full suite green.
- [ ] **Step 5:** Commit "Screen glitch frames and paint.xml glitch settings".

### Task 5: Server watch book (game-free)

**Files:**
- Create: `src/StashNet.Core/Network/ScreenWatchBook.cs`
- Test: `tests/StashNet.Core.Tests/ScreenWatchBookTests.cs`

**Interfaces:**
- Consumes: `Pos3`, `ScreenView` (Task 1).
- Produces `public sealed class ScreenWatchBook`:
  - `void Watch(int player, Pos3 terminal)`, `void Unwatch(int player, Pos3 terminal)`, `void DropPlayer(int player)`.
  - `IReadOnlyCollection<Pos3> Terminals` (watched), `bool TryGetCore(Pos3 terminal, out Pos3 core)`.
  - `IReadOnlyList<int> SetCore(Pos3 terminal, Pos3? core)` → players watching that terminal (to send Assign) when the core changed, empty when unchanged.
  - `IReadOnlyCollection<Pos3> Cores` (cores with ≥1 watcher), `IReadOnlyCollection<int> WatchersOf(Pos3 core)` (distinct).
  - `bool ShouldSend(int player, Pos3 core, ScreenView view)` (true and records when it differs from the last view sent to that player for that core; forgotten when the player stops watching that core).
  - `void MarkDirty(Pos3 core)`, `List<Pos3> TakeDue(float now)` (a core is due if dirty and `now - lastFlush >= 1f`; taking records `lastFlush = now`).
  - `bool LockChanged(Pos3 core, string lockPrint)` (true when different from the last print stored; first call stores and returns false).
  - `bool StaleRebuildAllowed(Pos3 core, float now)` (true at most every 5 s per core; records).
  - Removing the last watcher of a core forgets all its per-core state.

- [ ] **Step 1: Failing tests:**
  - `PlayerListedOncePerCore`: player 1 watches terminals A and B, both set to core C → `WatchersOf(C)` = {1}.
  - `UnwatchingOneTerminalKeepsTheOther`; `DropPlayerClearsEverything` (`Cores` empty).
  - `MovingTerminalToAnotherCoreMovesItsWatchers`: `SetCore(A, C2)` returns [1]; `WatchersOf(C)` empty, `WatchersOf(C2)` = {1}; `SetCore(A, C2)` again returns empty; `SetCore(A, null)` returns [1] and `Cores` empty.
  - `ShouldSendOnlyChanges`: same view twice → true then false; changed view → true; after unwatch+rewatch → true again.
  - `LockChangeResendsOnlyChangedViews`: two players on C; player 2 shut-out view sent; lock changes; `ShouldSend(1, C, sameLiveView)` false, `ShouldSend(2, C, liveView)` true.
  - `DirtyCoreIsDueOncePerSecond`: `MarkDirty(C)` at t=0 → `TakeDue(0)` = [C]; mark again at 0.3 and 0.6 → `TakeDue(0.6)` empty, `TakeDue(1.0)` = [C], then `TakeDue(1.1)` empty.
  - `LockChangedFirstCallFalse`, `StaleRebuildEveryFiveSeconds`.
- [ ] **Step 2:** Run `--filter ScreenWatchBookTests` → fail.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Pass.
- [ ] **Step 5:** Commit "Screen watch book".

### Task 6: Server and network glue

**Files:**
- Modify: `src/StashNet/Network/NetworkSummaryCache.cs`
- Create: `src/StashNet/Network/NetPackageStashNetScreen.cs`
- Create: `src/StashNet/Network/ScreenWatch.cs` (server)
- Create: `src/StashNet/Network/ScreenLink.cs` (client transport)
- Modify: `src/StashNet/StashNetMod.cs` (call `ScreenWatch.Init()` and `ScreenLink.Init()` after `VersionCheck.Init()`)

**Interfaces:**
- Consumes: Tasks 1, 2, 5; `NetworkLock`, `NetworkHeat.PerReport`, `HeatEmitter.PropHeatPerDrive/PropHeatCap/GetFloat`, `VersionCheck.ClientConfirmed/ClientMayUse`.
- Produces:
  - `NetworkSummaryCache.TryFindCore(World world, Vector3i pos, out Vector3i corePos) -> bool` (the existing 10 s `coreOf` lookup, factored out of `TryGet`, which now calls it).
  - `NetworkSummaryCache.TryPeek(Vector3i corePos, out NetworkSummary summary, out bool stale) -> bool` (no rebuild).
  - `public static event Action<Vector3i> NetworkSummaryCache.Recorded` raised at the end of `Record`.
  - `NetPackageStashNetScreen.Setup(ScreenMessage m)`; direction `Both`; read wraps `ScreenCodec.Read` in try/catch like `NetPackageStashNetRequest`; server side requires `VersionCheck.ClientConfirmed(Sender)` and a player entity, then `ScreenWatch.Handle(player, msg)`; client side `ScreenLink.Receive(msg)`.
  - `ScreenWatch.Handle(EntityPlayer player, ScreenMessage m)` (Watch/Unwatch only; Watch validates the block is `BlockStashNetTerminal` and cube distance ≤ 400, then `TryFindCore`, `book.SetCore`, sends Assign + State).
  - `ScreenLink.Watch(Vector3i terminal)`, `ScreenLink.Unwatch(Vector3i terminal)`, `ScreenLink.Receive(ScreenMessage m)` (forwards to `TerminalScreens.OnAssign/OnState`, Task 7).
- Behavior to implement:
  - `ScreenWatch` view for a player: `!TryPeek` → call `TryGet` once (builds); no core → `ScreenView.NoSignal`; `locked && !NetworkLock.IsAllowed(lock, PlayerId(player))` → `ScreenView.ShutOut()`; else live view from summary + `HeatSegmentsOf(PerReport(weight, perDrive, cap), cap)` from the Core block's properties.
  - `Recorded` handler → `book.MarkDirty` when the core has watchers.
  - `ModEvents.GameUpdate` (server only): every frame `TakeDue(Time.time)` → for each watcher, `ShouldSend` → send State. Every 2 s: re-resolve each watched terminal's core (`SetCore`, send Assign + State to returned players); per watched core build lock print (`locked|owner.CombinedString|sorted allowedUserIds CombinedString`) and on `LockChanged` mark dirty; if `TryPeek` says stale and `StaleRebuildAllowed` → `TryGet` (its `Record` marks dirty).
  - Sending: if not dedicated and `entityId == world.GetPrimaryPlayer()?.entityId` → `ScreenLink.Receive` directly; else `SendPackage(..., _attachedToEntityId: entityId)`.
  - `ModEvents.PlayerDisconnected` → `book.DropPlayer`; `WorldShuttingDown` → new book.
  - `ScreenLink` keeps a `HashSet<Vector3i>` of wanted terminals and a pending queue; on `GameUpdate` it flushes only when `VersionCheck.ClientMayUse(out _)` and (host) the local player exists. On world shutdown it clears. On a host it calls `ScreenWatch.Handle` directly with the local player.

- [ ] **Step 1:** Implement the three `NetworkSummaryCache` additions; `TryGet` behavior unchanged.
- [ ] **Step 2:** Implement package, `ScreenWatch`, `ScreenLink` (stub `TerminalScreens.OnAssign/OnState` calls compile against Task 7 names; if Task 7 isn't there yet, add `src/StashNet/Skins/TerminalScreens.cs` with empty `internal static void OnAssign(Vector3i terminal, bool hasCore, Vector3i core)` / `OnState(Vector3i core, ScreenView view)` for Task 7 to fill).
- [ ] **Step 3:** `dotnet build src/StashNet/StashNet.csproj -c Release` → 0 errors; `dotnet test tests/StashNet.Core.Tests` green.
- [ ] **Step 4:** Commit "Screen watch: server push and client link".

### Task 7: Client screens and glitch

**Files:**
- Create/fill: `src/StashNet/Skins/TerminalScreens.cs`
- Create: `src/StashNet/Skins/ScreenGlitcher.cs` (MonoBehaviour)
- Create: `src/StashNet.Core/Skins/TerminalScreenRefs.cs` + test `tests/StashNet.Core.Tests/TerminalScreenRefsTests.cs`
- Modify: `src/StashNet/Skins/SkinApplier.cs` (`SetScreens`), `src/StashNet/Skins/SkinOwner.cs` (`OnDisable`), `src/StashNet/Skins/SkinCommand*` reload path if it clears textures
- Modify: `Mod/Skins/paint.xml` (`stashnetTerminal`: add `live="true"`, glitch attrs at defaults)

**Interfaces:**
- Consumes: Tasks 1, 3, 4, 6.
- Produces:
  - Game-free `TerminalScreenRefs<TKey>`: `Add(TKey tv, Pos3? core)`, `Move(TKey tv, Pos3? core)`, `Remove(TKey tv) -> Pos3?` returning a core whose last TV just left (texture to destroy), `TvsOf(Pos3 core)`.
  - `TerminalScreens.Register(SkinOwner owner, BlockPaint paint)` (idempotent; adds/updates `ScreenGlitcher`, calls `ScreenLink.Watch(owner.Bed.pos)` once), `TerminalScreens.Unregister(SkinOwner owner)` (`ScreenLink.Unwatch`, refs remove, destroy freed textures), `OnAssign`, `OnState`, `Clear()` (world shutdown / reload).
  - Texture sets: `Texture2D[3]` = clean, damaged seed 1, damaged seed 2; created with `new Texture2D(256,192,RGBA32,false)`, `wrapMode = Repeat`, filled with `SetPixelData` + `Apply(false)`; redrawn in place when the view changes (no new textures). One shared shut-out set and one shared no-signal set (3 static frames), created on first use, destroyed in `Clear()`.
  - `ScreenGlitcher.Show(Texture2D[] set, bool restless)`: sets frame 0 on every screen material of the owner (the `(Instance)` materials `SetScreens` already finds); timer from `ScreenGlitch.NextGap` (gap ÷ 3 when restless); during a glitch, each 0.05 s picks offset jolt (`SetTextureOffset` random x in ±0.05·strength, or y roll) and/or a damaged frame; at the end resets offset to 0 and frame 0.
  - Until a State arrives the TV keeps today's `crtScreen` picture (current `SetScreens` path).
  - `SetScreens` for a block whose paint has `Live` → `TerminalScreens.Register` after putting the `crtScreen` fallback; `SkinOwner.OnDisable` → `TerminalScreens.Unregister(this)` before `Restore()`.

- [ ] **Step 1: Failing test** `TerminalScreenRefsTests`: `TwoTvsOneCoreFreedOnlyAfterBoth` (remove first → null, remove second → core), `MovingTvFreesOldCoreIfLast`, `RemoveUnknownIsNull`.
- [ ] **Step 2:** Run `--filter TerminalScreenRefsTests` → fail; implement; pass.
- [ ] **Step 3:** Implement `TerminalScreens`, `ScreenGlitcher`, hooks, `paint.xml` attrs.
- [ ] **Step 4:** Build 0 errors, full test suite green.
- [ ] **Step 5:** Commit "Live terminal screens with glitch".

### Task 8: Checklist, version, notes

**Files:**
- Create: `docs/testing/live-screen-checklist.md`
- Modify: `CLAUDE.md` (RESUME HERE entry), version via `tools/release.ps1 -Version 1.1.0`

- [ ] **Step 1:** Write the checklist, plain language, steps:
  1. Load the world: every terminal screen switches from the old green picture to live numbers within a few seconds.
  2. Numbers match the terminal window (stacks used / total, drive count).
  3. Shift-click 40 stacks in: screen keeps up, ends on the right number.
  4. Take a drive out of a bay by hand: screen updates within ~5 s.
  5. Set a code on the Core: `[LOCKED]` shows; a second player not on the list sees only `[LOCKED]`; after they enter the code they see numbers within ~2 s.
  6. Break the Core: `NO SIGNAL` within ~10 s; place it back: numbers return.
  7. Seven terminals on one network all show the same, glitch at different moments.
  8. Break a terminal, place a vanilla TV in the same spot: it shows the vanilla picture. `sn skin reload`: screens come back live.
  9. On the supercraft server with a friend: both see live screens; `sn perf` shows no rebuilds while nobody touches anything.
  10. Check the log for StashNet errors.
- [ ] **Step 2:** `powershell -File tools/release.ps1 -Version 1.1.0` → tests pass, `dist/StashNet-v1.1.0.zip` written.
- [ ] **Step 3:** Update `CLAUDE.md` RESUME HERE with v1.1.0 status (built, not deployed), spec/plan/checklist paths.
- [ ] **Step 4:** Commit "v1.1.0: live terminal screen".
