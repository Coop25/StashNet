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
- Current milestone: 1 (blocks & items exist), not started.
