# StashNet: project notes

## ▶ RESUME HERE (updated 2026-11-13)
- **Where things are:**
  - `master` = M8 Progression + the Core spacing rule (merged and pushed 2026-11-13; GitHub is in sync). Milestones 0–8 are done. M8 design: `docs/superpowers/specs/2026-11-13-m8-progression-design.md`. M8 was accepted by the owner ("everything seems to work"). The v0.9.0 log was clean, but that session was only about 2.5 min with no `sn heat`, so not every M8 checklist step was confirmed.
  - **Current work: M9 on branch `m9-release`** (ahead of `master`, **not pushed, not merged**). It contains:
    1. The release zip build option (`-p:Package=true` → `dist/StashNet-v<version>.zip`).
    2. A player-facing `README.md`.
    3. `docs/testing/M9-checklist.md`: Part A is solo, joining a dedicated server on the owner's PC; Part B needs a friend.
    4. **New tier looks.** Each bay and Core tier uses its own vanilla model.
    5. **Custom 3D drive icons.**
  - **v0.17.0 BUILT, NOT DEPLOYED (2026-11-14): traders refuse drives that hold items (`DriveSellGuardPatch`, 33 methods patched); drive prices checked, no sell exploit.** v0.16.0 (deployed 2026-11-14): clicking the terminal grid while holding a picked-up stack deposits it (left = whole stack, right = one; drives refused). `XUiC_StashNetTerminal.TryDepositHeld`.** v0.15.0 (deployed 2026-11-14): header deposit-all button removed (backpack move-all replaces it), tabs shown only for categories that hold items (rebuilt on each listing), click = 1 item / shift-click = a stack.** v0.14.1 (deployed 2026-11-14): fixes dragging items while a Storage Terminal is open (owner: dragged items vanished until released). Cause: our window never set `xui.DragAndDropWindow.InMenu`, so the drag window put the item back every frame; vanilla menus set it via WindowSelector / WindowNonPagingHeader. v0.14.0 IS DEPLOYED and loaded clean.** v0.14.0 (2026-11-14): terminal category tabs (vanilla `CategoryList` + `category_icon`, All + ui_display.xml trader_category_display, matched by item/block group names like the backpack filter; selection remembered per game run).** v0.13.0 (also not deployed yet): no "K" names anywhere (item IDs now `stashnetDrive1728`/`stashnetDrive5184`, icons renamed), backpack move-all / fill-and-smart buttons deposit into the network while a terminal is open (`Patches/BackpackMovePatches.cs`), terminal window 874 tall with 11 rows (item info 392 + panel gap 9 + backpack content bottom 473; screenshot pending; on_scroll only on cell buttons, never the grid: a grid collider ties with the buttons and can steal clicks), mouse wheel pages the grid. Expect 32 methods patched.** Previously — **v0.12.0 (2026-11-14): five drive tiers, named by stack count ("48-Stack Drive" … "5184-Stack Drive"; owner: no "1K/4K" names anywhere players see).** v0.12.0 was deployed with K names; the rename to stack-count names (commit 54b5a0d) is **built but not deployed yet**. v0.11.0 (3 tiers 24/72/216) was deployed but never played. Five tiers: 48 / 192 / 576 / 1,728 / 5,184 stacks, each crafted from **3** empty drives of the tier below + parts (spec Revision 1). The 48/192/576 drives keep the old IDs `stashnetDriveBasic`/`Standard`/`HighDensity` (saves keep working); new IDs `stashnetDrive1728`/`stashnetDrive5184`. Heat weights 1/2/4/6/8. New icons for all five (labels show stack counts 48–5184, owner: not tier names). Everything below from v0.11.0 still applies: only empty drives count as ingredients (backpack/toolbelt counts + removal, recipe list, craft-count max). Every drive slot shows a fill bar (green/yellow from 75%/red full) and a centered percent; the info panel adds "N / M stacks used (P%) · T different items". Spec `docs/superpowers/specs/2026-11-14-drive-balance-and-fill-bar-design.md`, checklist `docs/testing/drive-fill-checklist.md` (expect 27 methods patched). Next: owner OKs deploy, runs the checklist; send a screenshot of the bar/percent.
  - **v0.10.9 IS installed and accepted** (owner, 2026-11-13: "All works"). Log `output_log_client__2026-11-13__17-54-53.txt`: `Loaded v0.10.9 (21 methods patched)`, no exceptions. v0.10.9 adds: custom icons for all 8 blocks (`tools/icons/draw-block-icons.ps1`: repainted vanilla icon + tier stripe, core emblem on Cores; no `CustomIcon` on any StashNet block now); pickup of empty Drive Bays (`TEFeaturePickup`, "Take" hidden unless empty, `Patches/BayPickupGuardPatch.cs` re-checks at the timer end) and the Storage Terminal's "take", both only inside your own land claim (owner's rule, like vanilla workbenches/Cores); Core "Show Area"/"Hide Area" (`Blocks/TEFeatureStashNetArea.cs`, reuses vanilla `LandClaimBoundsHelper` rescaled to 2 x reach + 1, client-only, not saved).
  - ✅ **v0.9.1 single-player checks passed (owner, 2026-11-13: "All three work"):** new tier looks, custom drive icons, and Core spacing (M8 checklist step 9). The client log for that session (`output_log_client__2026-11-13__13-54-15.txt`) has no StashNet errors. Spacing refusals are client-side and not logged, so the log can't confirm them on its own. Not yet run: any M9 checklist line as a joined player.
  - ✅ **Salvage look accepted in-game (owner, 2026-11-13: "Silent, looks good").** Design `docs/superpowers/specs/2026-11-13-wrecked-salvage-look-design.md` (Revisions 1–2), plan `docs/superpowers/plans/2026-11-13-wrecked-salvage-look.md` (Revision R), ledger `.superpowers/sdd/2026-11-13-wrecked-salvage-look/progress.md`. How it works: on a StashNet model's first appearance, `src/StashNet/Skins/SkinPainter.cs` reads the vanilla `_MainTex` back from the GPU and repaints a copy on a background thread (`StashNet.Core/Skins/TextureRepaint`: recolor, grime, damage scatter, stamps). It also copies `_RMOM` with alpha (= the game's tint mask) set to 0, because masked parts show only the per-renderer `_Color` property block. Clones of the materials go on StashNet blocks only. Settings: `Mod/Skins/paint.xml` (tint, strength, damage, seed, holes, mask, stamps, screen, mute). Console: `sn skin reload|grid|dump|inspect`. **Pitfalls found:** the game pools models and deletes runtime materials on pool drop (`GameObjectPool`/`Utils.CleanupMaterials`), so `SkinOwner.OnDisable` restores vanilla materials/sound/screen and `OnEnable` repaints only if the world block is still ours. The CRT screen texture goes into the TV's own `(Instance)` screen material (a copy rendered black). The CRT hum: `LightLOD` re-enables its `AudioPlayer` every frame, so mute unhooks it. Storage Terminal = CRT TV (1×1×1). Not done: file cabinet drawer stencils (missed), Storage Core front stamps, optional live drive count on the CRT, the final whole-branch review.
  - **Handoff state (updated 2026-11-13 after the Scrap Core decision and the v0.10.7 install):**
    - **Installed in the game: v0.10.7** (see above). It adds painting the server rack's glass (`_Albedo`, `solid="true"` stamps) and `flat="true"` stamps that flatten the normal map (hides the safe's embossed BeaverBox logo).
    - **Owner-approved looks, tuned live with `paint.xml` + `sn skin reload`:**
      - Drive Bay (cabinet): stencil on the top drawer, LED panel / vent / ports on drawers 2–4, blue stripe on the bottom drawer. Each drawer has its own UV spot, rot 180.
      - Armored bay (safe): logo, flourishes and frame painted over with `paintPatch`; DRIVE ARRAY lights, DATA STORAGE warning, vent, ports, purple stripe; two vents on the back. Front C5–D8 and back A5–B8, both rot 180.
      - CRT terminal: green screen ("DRIVES ....... OK"), silent.
      - Owner removed all "DRV n/n" readouts. Live occupancy would need a texture per bay.
    - **Scrap Storage Core: decided, done (owner, 2026-11-13).** Offered a model swap (`commercialDistributionBox` or `generatorbank`) so "STASHNET SCRAP CORE" could be written readably; the owner chose to **keep the `electricalBox02` model with symbols only**, and said look C is **done as is** (no more damage). The model is mirrored on every face (front A–C rows 6–8, seam at column C, u 0.3125; lid F–H mirrored around H; sides F–G), so never put words on it. Look C: `coreHalfBlue` emblem against the seam, cable runs, hazard bands, strength 0.85 / damage 1.7. Don't re-offer the model swap unless the owner brings it up.
    - **Live-tuning workflow that works:**
      1. `sn skin grid` (or a temporary glyph test pattern copied only into the game's `paint.xml`) to find each face's UV cells and rotation.
      2. Edit the repo `Mod/Skins/paint.xml` (and the atlas via `tools/skin/draw-skin-atlas.ps1` for new sprites).
      3. Copy `paint.xml` / `atlas.xml` / `stashnet-skin.png` into the game's `Mods\StashNet\Skins` (the owner OK'd this for skin files during tuning).
      4. The owner runs `sn skin reload`.

      The atlas is 1024×2048 now. Its first run after an edit can produce a different hash; the next runs are stable.
    - **Done 2026-11-13 (owner: "looks good" for each):** rack glass (the glass shows only the LEFT half of `GlassDirty_d`, u ~0.04-0.46, all v; marks centered at u 0.25); **Storage Core model is now `controlPanelBase05`** (owner found the transformer jarring; same `controlPanelControls` texture as the Advanced Core, front A7-C8 upright, sloped panel A5-C6) with green name on the sloped panel under the buttons, green emblem (half sprite + the same half rot 180) and stripe; Advanced Core got a purple emblem; Crafting Terminal: STASHNET on the green cutting mat's front-left edge (mat = A-C rows 5-6, upside down; the big gear model covers its middle), owner removed the LED strip. Unverified/invisible: the toolbox name (`metalToolBox_d` B1-B3 face, rot 270) and the pegboard port panel never showed; the owner moved on. (most are still tinted vanilla icons; the Drive Bay icon is derived from a vanilla icon); optional live drive count on the CRT; the final whole-branch review; then M9 Part A.
- **Next steps:**
  0. Continue the skin follow-ups ("Still open after that" above), or ask the owner if the looks are finished. Before 1.0: the final branch review.
  1. The owner runs `docs/testing/M9-checklist.md` Part A. The dedicated server **isn't installed yet** (checked 2026-11-13: no `7 Days to Die Dedicated Server` folder under `G:\SteamLibrary\steamapps\common`). Setup steps are at the top of the checklist. Server log: newest `output_log` in the server's `7DaysToDieServer_Data`.
  2. Fix what breaks, bump to **1.0.0**, merge `m9-release` into **`master`** (never `main`), and push only when the owner says to.
- **Assumptions to confirm:** the owner never said who the release is for. M9 assumes friends on a server the owner runs, downloading the zip from GitHub, so protection stays at vanilla's level (placement and drives-only are checked on the player's own game). Revisit if they want a public release.
- **Known gaps:**
  - `sn heat` (and the other `sn` subcommands) use the local player, so they don't work for a joined player on a dedicated server.
  - ~~Selling a full drive to a trader destroys its contents.~~ Fixed in v0.17.0: `Patches/DriveSellGuardPatch.cs` refuses selling a drive that holds items (own vending machine excluded). Drive trade values (60/240/600/1500/4000) are far below their ingredients' value, so crafting drives to sell can't profit (checked 2026-11-14).
- **Parked:** a running cost (fuel or electricity) as a balance lever, to decide after the owner has played M8.
- **The owner's StashNet test world contains StashNet blocks.** Don't load it without the mod installed (those blocks would break). Their real saves never had StashNet.
- **Open items:**
  - ⚠️ Multiplayer has never been tested with a second player (M3b Part B, the M4 lockout, M6 client "click twice"). Must happen in M9.
  - Terminal window layout: no screenshot received yet.
  - The owner's preferred workflow: build → ask "can I deploy? (game closed)" → deploy → the owner tests → check the log → record → commit.

A 7 Days to Die mod adding a drive-based storage network (Core, Drive Bays, drives, Storage/Crafting Terminals, password, heat, armor tiers). Full design and roadmap: `docs/PLAN.md`.

## Working with the owner
- The owner is new to modding: no C#, DLL, or texture experience. Claude writes all code/XML and builds the mod. The owner tests in-game and reports back.
- Explain steps in plain language. Give the owner short in-game test checklists at the end of each milestone.

## Conventions
- Never commit game DLLs (Assembly-CSharp.dll, 0Harmony.dll, Unity DLLs). Reference them from the local game install.
- Game-free logic (drive storage, network merge, passwords, heat math) lives in a separate library with automated tests. Game glue stays thin.
- All balance numbers live in one config XML.
- v1 uses vanilla models. Icons: every StashNet block has a custom PNG drawn by `tools/icons/draw-block-icons.ps1` from its vanilla icon (v0.10.9), and the three **drive icons are custom PNGs** (owner asked, 2026-11-13). They're worn 7 Days-style 3D boxes in `Mod/UIAtlases/ItemIconAtlas/<item name>.png`, drawn by `tools/icons/draw-drive-icons.ps1` (deterministic, so re-running reproduces them byte for byte). Drives have no `CustomIcon`, so the game uses the PNG named after the item. No other custom art.

## Git
- **The default branch is `master`, never `main`** (owner, 2026-11-13; renamed from `main` that day). Each milestone gets its own branch off `master` and merges back into `master` after the owner's in-game test.

## Never touch the game install without asking
Build to `dist/` only. Deploying (`-p:Deploy=true`) copies into the game's Mods folder. Do it **only after the owner says yes for that deploy**, and only while the game is closed. The owner plays this game, and their real saves run other mods. Read-only inspection of the game folder is fine.

## Build
- `dotnet build src/StashNet/StashNet.csproj -c Release` stages the mod into `dist/StashNet/`.
- Add `-p:Deploy=true` to also copy it into `<GameDir>\Mods\StashNet` (see the rule above).
- The game path comes from `local.props` (gitignored; template in `local.props.example`).
- `nuget.config` pins nuget.org because the owner's global NuGet config has no sources.
- **Releases (owner, 2026-11-14): cut on this PC with `tools/release.ps1`, not GitHub Actions** (the build needs the game's DLLs, which never go to GitHub; the owner declined both a self-hosted runner and committing stripped reference DLLs). `-Version x.y.z` alone sets the version, runs tests and writes `dist/StashNet-v<version>.zip` (local only); `-Tag` also commits + tags `v<version>`; `-Publish` also pushes the branch and tag and runs `gh release create` with the zip (pre-release if not on `master` or below 1.0.0). **`-Publish` publishes: ask the owner before each run.** The repo is private, so friends need repo access to download a release (or get the zip directly).
- Decompiler: `dotnet tool run ilspycmd -- -t <TypeName> "<GameDir>\7DaysToDie_Data\Managed\Assembly-CSharp.dll" -r "<Managed dir>"`. Write decompiled output to a temp/scratch folder, **never into the repo**.

## Game API facts (V3.4 b10, verified by decompiling)
- Mod entry point: `IModApi.InitMod(Mod)`. Harmony comes from `Mods/0_TFP_Harmony/0Harmony.dll`.
- Assembly-CSharp is "publicized": members tagged `[PublicizedFrom(Protected)]` are public in the DLL, so overrides must be `public override`.
- Console commands: subclass `ConsoleCmdAbstract`. They are auto-registered from mod DLLs.
- Item data: `ItemValue.SetMetadata/TryGetMetadata` (int/float/string). Strings are saved with BinaryWriter, with no explicit size cap.
- Storage blocks use `BlockCompositeTileEntity` + `CompositeFeatures` (e.g. `TEFeatureStorage`, `TEFeatureLockable`). Custom features implementing `ITileEntityFeature` in mod DLLs are found **by class name** and need a parameterless constructor.
- Heat: `GameManager.Instance.World.aiDirector.NotifyActivity(EnumAIDirectorChunkEvent, Vector3i, float value, float duration=720)`. It is ignored during blood moons and when the heat setting is off.

## Owner's setup
- Game version: V3.4.0 (b10)
- Game install path: G:\SteamLibrary\steamapps\common\7 Days To Die
- Already runs other DLL mods with EAC off, including storage-related ones (WraithsStorageSolutions, DarkfireAutoSortChest). Watch for conflicts.
- Milestone 0: ✅ done 2026-11-12. All in-game checks passed, including 1,000-type item metadata through save/reload, container, and drop/pickup.
- Milestone 1: ✅ done 2026-11-12. The owner reports all checklist sections work. The log shows `Loaded v0.1.0 (5 methods patched)` with no StashNet errors.
- Milestone 2: ✅ done 2026-11-12. All checklist sections pass (capacity limits, weapon with mod/wear/ammo round-trip, full backpack, moves and save/reload, no drive-in-drive). The log shows 0 StashNet errors.
- Milestone 3a: ✅ done 2026-11-12 (v0.3.2). The owner confirmed deposit/withdraw through the terminal works and asked to move on. Logs for v0.3.0–0.3.2 show no StashNet errors or UI warnings. Not explicitly confirmed: every checklist section, and the window layout (no screenshot received yet).
- Milestone 3b: ✅ single-player done 2026-11-12 (v0.4.0 loads with 6 patches, no StashNet errors). ⚠️ **Multiplayer is UNTESTED.** The owner can't test MP right now. Part B of `docs/testing/M3b-checklist.md` (client use, no-dupe race, client full backpack, live refresh, dedicated server load) must pass before any public release (roadmap M9).
- Milestone 4: ✅ accepted by the owner 2026-11-12 (asked to move on). The v0.5.0 session loaded cleanly (first build against the game's own runtime, lockable Core). ⚠️ v0.5.1 (drive locking removed) had **not been launched in-game** when M5 started, and the M4 checklist results were not reported. Re-check the log for v0.5.1 before claiming it works.

- Milestone 5: ✅ done 2026-11-12 (v0.6.1). The owner confirmed drive unload and the tier/durability bars work. The v0.6.0 and v0.6.1 sessions loaded with 6 patches and no StashNet errors, which also covers v0.5.x's lock changes loading. Scope (owner): unload only, with no fill/wipe/format.
- Milestone 6: ✅ done 2026-11-12 (v0.7.3). The owner confirmed the Crafting Terminal works, including the capped craft count, cancel refunds to storage, and finished crafts to storage. The log shows `Loaded v0.7.3 (19 methods patched)` and no StashNet errors.
- Milestone 7: ✅ done 2026-11-12 (v0.8.0). The owner confirmed it in-game: `sn heat` showed 24 drives → 6 per report, and the area heat rose, faded between reports, and reset (most likely a screamer roll). The log shows `Loaded v0.8.0 (19 methods patched)` and no StashNet errors. Existing Cores loaded with a harmless vanilla warning ("1 feature(s) ... not present in the saved data"). Deferred for M8: overlapping Cores double-count shared bays; each heat report re-reads every drive (re-check with bigger radius/drives). Offered, not done: `sn heat` reading the Core's own area instead of the player's.

## Progression (M8, v0.9.0)
- Owner's balance goal: StashNet should help without being overpowered. M8 balances with prices and size-weighted heat. A running cost (fuel or power) is parked until after playtesting.
- Tiers (all numbers in XML): drives named by stack count = 48/192/576/1,728/5,184 stacks (each crafted from 3 empty drives of the tier below, owner 2026-11-14; display names only, 48-stack/192-stack/576-stack keep the old Basic/Standard/HighDensity item IDs), heat weight 1/2/4/6/8 (`StashNetHeatWeight`). Bays Drive Bay/Reinforced/Armored = 4/8/12 slots (`stashnetDriveBaySlots*` loot containers), 1,000/5,000/10,000 HP. Cores Scrap/Storage/Advanced = reach 6/12/24, heat 0.25/0.18/0.12 per weight, cap 6.5.
- **Tier looks** (owner picked 2026-11-13, on `m9-release`): each tier uses a different vanilla model of the same size, because upgrades swap the block in place. Bays (1×2×1): Drive Bay = tall file cabinet (`filecabinet_tall_closedPrefab`, ModelOffset 0,0,0), Reinforced = server rack, Armored = gun safe (`safeGunLargeFullPrefab`, TintColor 404040 like vanilla). Cores (1×1×1): Scrap = electrical box, Storage = control panel 05 (`controlPanelBase_05Prefab`, was the utility transformer until v0.10.8), Advanced = control panel (`controlPanelBase_01Prefab`). Icons are those models' vanilla icons with our tints. `TintColor` tints the world model (`_Color`); `CustomIconTint` only tints the icon.
- **No drive upgrade kits** (owner). Move up a drive tier by unloading three drives of the tier below (shift-click in a terminal) and crafting them into the next tier (2026-11-14). Only empty drives count as ingredients (`Patches/DriveIngredientGuardPatches.cs`, `DriveRecipeCountPatches.cs`); `Patches/DriveScrapGuardPatch.cs` refuses Scrap on a drive that holds items or can't be read.
- **Bay/Core upgrades = vanilla `UpgradeBlock` with the next tier's block as the item** (owner's choice). Vanilla carries the tile entity over (`GameManager.ChangeBlocks` → `UpgradeDowngradeFrom`: storage grid resized to the new loot size, lock locked/allowed/password, owner). Our items are appended to every tool's `Allowed_upgrade_items` (items.xml; the XPath `append` on an attribute does `Value += text`). No downgrade on break (owner): a broken bay drops its drives in a loot bag.
- Tier blocks are written out in full, **never `Extends`**: inheriting would copy the lower tier's `UpgradeBlock` (a block upgrading into itself) and nested `CompositeFeatures`.
- **Core spacing (v0.9.1, owner):** a Core can't be placed within 2 × the largest Core reach (48) of ANY other Core, any tier or owner. All Core tiers use `Class="StashNet.Blocks.StashNetCore, StashNet"` (`Blocks/BlockStashNetCore.cs`, a `BlockCompositeTileEntity` subclass overriding `CanPlaceBlockAt`; game-free math in `StashNet.Core/Network/CoreSpacing.cs`). It's checked on the placing client, like vanilla placement rules, and skipped in edit mode. Cores already too close keep working.
- Discovery walks chunk tile entities (`Network/TileEntityScan.cs`), not every block. A bay or terminal belongs to the nearest in-reach Core, with ties to the lowest Y, then X, then Z (`StashNet.Core/Network/CorePicker.cs`, tested). This fixed M7's double-counted shared bays. A Core without a tile entity (placed before M4) is no longer found.

## Heat (M7)
- Vanilla heat (decompiled): per 5×5-chunk area; workstations report `HeatMapStrength` every `AIDirector.GetActivityWorldTimeDelay()` (~167 real s), each report fades over 720 s, so repeats build to ~4.3× one report. At 25 the area rolls 20% for screamers and resets. Forge 6, workbench/campfire 5. The game ticks every loaded tile entity (`Chunk.UpdateTick`), composite features included.
- Core feature `TEFeatureStashNetHeat` (server only) reports `min(drives × StashNetHeatPerDrive, StashNetHeatCap)` (0.25 / 6.5) at the Core. The Storage Terminal adds `StashNetHeatOnUse` (0.5) per request, throttled per terminal. The Crafting Terminal already has workbench heat. Owner's target "B": 12 drives alone never bring screamers; at the cap the network slowly does. `sn heat` shows the numbers.
- Adding a feature to an existing composite block is save-safe (composite TE read skips missing/new features by name hash).

## Crafting Terminal (M6)
- The block `Extends="workbench"` with `WorkstationWindow=workstation_workbench` (recipes via the inherited `CraftingAreaRecipes`; **never set `WorkstationName`**: data is filed under it but looked up by block name, so the window won't open), and marker property `StashNetCraftingTerminal`. It's a real vanilla workbench.
- `src/StashNet/Crafting/`: while its window is open, `CraftingNetwork` caches the network's plain items (List request every 2 s). Hooks make GetItemCount/GetAllItemStacks/HasItems/BuildRecipeInfosList include that stock. On a Craft click (`ItemActionEntryCraft.OnActivated`), the HasItems postfix withdraws the shortfall into the backpack, and vanilla then takes ingredients from the backpack as usual. Remote clients: async, so "click Craft again". The craft-count max (`XUiC_RecipeCraftCount.calcMaxCraftable` postfix) is capped to crafts whose shortfall fits in free backpack space (owner chose capping over taking directly from the network, 2026-11-12). Owner request (v0.7.3): cancel refunds go to the network (AddItem redirect during `XUiC_RecipeStack.HandleOnPress`), and finished crafts go to the network: while open, the client moves output-grid stacks when the cached free stacks allow; while closed, the server moves `TileEntityWorkstation.output` (UpdateTick postfix, skipped when `IsUserAccessing` or `LockManager.IsLockedServer`, 5 s retry throttle).
- Harmony note: patched methods must be declared on the patched type (`XUiC_WorkstationWindowGroup.Update/OnOpen/OnClose` are). Otherwise Harmony patches the base class for everything.

## Drive unload (M5)
- Shift-click a drive while a terminal is open → the client empties it in place and tags it (`StashNetPending` token) → `DriveUnload` request → the server inserts and returns leftovers in `TerminalResponse.ReturnToDrive` → the client refills the tagged drive (backpack, then toolbelt). If the drive isn't found, the leftovers go to the backpack.

## Password design (M4)
- The network code = the **Core's** vanilla `TEFeatureLockable` (the Core is a CompositeTileEntity with Lockable + Pickup). Bays and terminals have no lock of their own and ask the Core (`NetworkLock`, `CoreLookupCache` with a 1 s Core-position cache for per-frame prompt checks).
- The server enforces access on every terminal request (`persistentPlayers` PrimaryId). Bay opening is gated client-side by vetoing the storage "Search" command in `TEFeatureStashNetDriveBay.AllowBlockActivationCommand`.
- **Drives are NOT locked to networks** (owner's decision, 2026-11-12, after v0.5.0 briefly stamped them). Any network can read any drive. The lock guards bays and terminals only. Leftover `StashNetLock` metadata from v0.5.0 is cleared on the next server save (`DriveItem.ClearStamp`). Don't reintroduce drive locking unless the owner asks.
- Bays keep their old `TEFeatureLockable` only so pre-M4 saves load. Feature save order is by name, not XML order, so reordering features in XML is safe.

## Build note
The mod compiles against the **game's own** mscorlib/System libraries (`GameFrameworkLib` + the `UseOnlyGameFrameworkLibraries` target in the csproj), not .NET Framework reference assemblies. The game API uses `ReadOnlySpan`, which only exists in Unity's mscorlib.

## Multiplayer design (M3b)
- Every terminal action is a `TerminalRequest` → `TerminalServer.Handle` (server only, the sole writer of drive data) → `TerminalResponse`. Single-player/host call it directly; remote clients use `NetPackageStashNetRequest`/`Response` (auto-registered NetPackage subclasses).
- Deposit: the client removes from its backpack first, and the server gives back leftovers. Withdraw: the server removes from drives, and the client returns overflow via `Return`, which the server stores or drops at the player's feet (never bounced).
- The server validates: the block is a terminal and the player is within 8 blocks. Client backpacks are client-authoritative (vanilla 7DTD), so deposits trust the client. This protects against races, not modified clients.
- Server-side code must never touch XUi/EntityPlayerLocal (dedicated servers have no UI).
- PowerShell 5.1 commit tip: write commit messages to a file with `[IO.File]::WriteAllText(..., UTF8Encoding($false))` and use `git commit -F`. Here-strings with quotes break, and `Out-File` adds a BOM.

## Network + terminal design (M3a)
- `StashNetNetwork.TryOpen` re-scans and reloads on **every** operation (no cached state). Terminal → nearest Core whose `StashNetRadius` reaches it (cube distance) → Drive Bays within that radius of the Core.
- `NetworkStorage` (Core, tested) pools drives: insert prefers drives holding the item, then the most free room.
- Terminal = custom Block class `BlockStashNetTerminal` (XML `Class="StashNet.Blocks.StashNetTerminal, StashNet"`); the UI is `Mod/Config/XUi_InGame/` + `src/StashNet/UI/`.
- 3a works in single-player / host only. Remote clients get a "not supported yet" tooltip.

## Drive storage design (M2)
- `src/StashNet.Core/` is game-free (netstandard2.0) with xUnit tests in `tests/StashNet.Core.Tests/` (`dotnet test`). Its sources are compiled into the mod DLL via a `<Compile Include>` link, so there is no second DLL.
- Drive contents = `DriveContents` serialized as `SND1` text in item metadata key `StashNetDrive`.
- Items are stored by NAME. Non-plain items use our own name-based "variant" encoding (`src/StashNet/Drives/ItemCodec.cs`). **Never store the game's binary ItemValue format.** It uses numeric IDs that shift when mods change, and its reader branches on the item class mid-read.
- Never lose items: move into the destination first, then remove from the source only what arrived. Unreadable drive data is refused and never overwritten.
- **Capacity = stacks only, no type limit** (owner's decisions, 2026-11-12). Each entry uses ceil(count / stack size) stacks. Item prop: `StashNetMaxStacks`. Tiers: 48, 192, 576, 1,728, 5,184 (2026-11-14; was Basic/Standard/High-Density 24/96/384). The terminal shows "N different items", each item counted once across drives (`NetworkStorage.DistinctItems`). Stack sizes come from the game's ItemClass at runtime (`ItemCodec.StackSizeOf`) and are never stored.
- Testing world: the owner reuses one throwaway world for all StashNet testing (started in M0).
- Game logs: `%APPDATA%\7DaysToDie\logs\output_log_client__*.txt` (newest = latest session; one file per game launch). Read-only, OK to check. When the owner reports test results, verify the log shows the expected commands and no StashNet errors.

## Mod content conventions
- Every block and item name starts with `stashnet`. Drives carry the item tag `stashnetDrive`, and code checks the tag, never names.
- The Drive Bay is a `CompositeTileEntity` with `TEFeatureStorage` + `TEFeatureLockable` + our marker `TEFeatureStashNetDriveBay`.
- Drives-only is enforced client-side by Harmony hooks in `src/StashNet/Patches/DriveBayOnlyDrivesPatches.cs` (5 methods patched). There's no server-side enforcement yet.
