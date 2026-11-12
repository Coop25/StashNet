# StashNet: project notes

A 7 Days to Die mod adding a drive-based storage network (Core, Drive Bays, drives, Storage/Crafting Terminals, password, heat, armor tiers). Full design and roadmap: `docs/PLAN.md`.

## Working with the owner
- The owner is new to modding: no C#, DLL, or texture experience. Claude writes all code/XML and builds the mod. The owner tests in-game and reports back.
- Explain steps in plain language. Give the owner short in-game test checklists at the end of each milestone.

## Conventions
- Never commit game DLLs (Assembly-CSharp.dll, 0Harmony.dll, Unity DLLs). Reference them from the local game install.
- Game-free logic (drive storage, network merge, passwords, heat math) lives in a separate library with automated tests. Game glue stays thin.
- All balance numbers live in one config XML.
- v1 uses vanilla models and tinted vanilla icons only. No custom art.

## Never touch the game install without asking
Build to `dist/` only. Deploying (`-p:Deploy=true`) copies into the game's Mods folder. Do it **only after the owner says yes for that deploy**, and only while the game is closed. The owner plays this game, and their real saves run other mods. Read-only inspection of the game folder is fine.

## Build
- `dotnet build src/StashNet/StashNet.csproj -c Release` stages the mod into `dist/StashNet/`.
- Add `-p:Deploy=true` to also copy it into `<GameDir>\Mods\StashNet` (see the rule above).
- The game path comes from `local.props` (gitignored; template in `local.props.example`).
- `nuget.config` pins nuget.org because the owner's global NuGet config has no sources.
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
- Current milestone: 4 (password), not started.

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
- **Capacity = stacks only, no type limit** (owner's decisions, 2026-11-12). Each entry uses ceil(count / stack size) stacks. Item prop: `StashNetMaxStacks`. Tiers: Basic 24, Standard 96, High-Density 384. The terminal shows "N different items", each item counted once across drives (`NetworkStorage.DistinctItems`). Stack sizes come from the game's ItemClass at runtime (`ItemCodec.StackSizeOf`) and are never stored.
- Testing world: the owner reuses one throwaway world for all StashNet testing (started in M0).
- Game logs: `%APPDATA%\7DaysToDie\logs\output_log_client__*.txt` (newest = latest session; one file per game launch). Read-only, OK to check. When the owner reports test results, verify the log shows the expected commands and no StashNet errors.

## Mod content conventions
- Every block and item name starts with `stashnet`. Drives carry the item tag `stashnetDrive`, and code checks the tag, never names.
- The Drive Bay is a `CompositeTileEntity` with `TEFeatureStorage` + `TEFeatureLockable` + our marker `TEFeatureStashNetDriveBay`.
- Drives-only is enforced client-side by Harmony hooks in `src/StashNet/Patches/DriveBayOnlyDrivesPatches.cs` (5 methods patched). There's no server-side enforcement yet.
