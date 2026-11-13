# Wrecked-Salvage Look Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give all 8 StashNet blocks the owner's "C3 military salvage, wrecked but working" look by tinting the vanilla models army green and attaching script-drawn decals at runtime. Also turn the Storage Terminal into the vanilla CRT TV.

**Architecture:** Game-free code in `StashNet.Core/Skins` does three things, all tested:
- parses a texture-atlas index (`Mod/Skins/atlas.xml`)
- parses a per-block decal layout (`Mod/Skins/layouts.xml`)
- turns "piece on face X at (u,v)" into quad corners

Thin game glue (`src/StashNet/Skins`) handles the rest:
- loads the atlas PNG
- when a StashNet block's model appears, builds one mesh of quads per material in a **sibling** object next to the model
- keeps a registry, so `sn skin reload` can rebuild skins without restarting the game

A deterministic PowerShell script draws the atlas. A second script draws the block icons from vanilla icons.

**Tech Stack:**
- C# net48 against the game's own libraries
- Unity 2022.3 API (`UnityEngine.CoreModule`, plus `UnityEngine.ImageConversionModule`, new)
- xUnit for `StashNet.Core.Tests`
- PowerShell 5.1 + System.Drawing for the art scripts

**Spec:** `docs/superpowers/specs/2026-11-13-wrecked-salvage-look-design.md`. Mockups are in `docs/superpowers/specs/2026-11-13-wrecked-salvage-look-mockups/`.

## Global Constraints

- **Style rule:** "When in doubt, dirtier and more worn." The owner rejected clean/sci-fi versions.
- **Vanilla untouched:** never edit a shared material or texture. Skin pieces use materials the mod creates.
- **Dedicated server:** all look code returns immediately when `GameManager.IsDedicatedServer`.
- **No gameplay change:** sizes, slots, HP, recipes, heat and upgrades stay the same (except the terminal becoming 1×1×1).
- **Deterministic art:** re-running each script reproduces its files byte for byte.
- **Names start with `stashnet`.** Don't commit game DLLs. Decompiled code goes to a scratch folder, never the repo.
- **Never deploy to the game without the owner's yes for that deploy, with the game closed.** Build to `dist/` only (`dotnet build src/StashNet/StashNet.csproj -c Release`). Before asking, check that `7DaysToDie` isn't running.
- **Branch `m9-release`.** Commit after each task (PowerShell tip from CLAUDE.md: write the message to a file with UTF-8 without BOM and use `git commit -F`). Never push.
- **Tier colors:** tier 1 blue `3A6FB0`, tier 2 green `4CAF50`, tier 3 purple `9C5FD0`. Army green tint `4A503F`; the Armored bay is darker, `3A3F31`. These are starting values from the mockups. Tune them in-game against the mockups.
- **Order:** Phase 1 (Tasks 1–6) ends at an **owner gate**. Phase 2 (Tasks 7–11) starts only after the owner approves the Drive Bay in-game.

## Review Focus

1. **Bad or missing `layouts.xml` / `atlas.xml` / PNG.** Expected: one `[StashNet]` error line, no skins, and the game and StashNet keep working. Test: Task 1 `Parse_MalformedXml_Throws` and `Parse_BadPiece_SkippedWithWarning`, plus the Task 4 load guard.
2. **Upgrading a block in place.** The new tier's model gets its own skin once, never doubled, and the old one goes away with the old model. Test: Task 4 checklist line "upgrade" plus `Apply` idempotence (reuse of an existing `SkinOwner`).
3. **Chunk unload/reload and pick-up.** No leftover floating skin objects. Test: Task 4 `SkinOwner.OnDestroy` destroys the sibling; checklist line "walk away 200 m and back".
4. **Vanilla tint and damage effects.** The game tints every renderer *under* the block transform through `_Color` (`BlockEntityData.SetMaterialColor` → `GetComponentsInChildren`). Skins live in a sibling object, so a blue stripe never turns green. Checklist line "stripe keeps its color".
5. **A piece too big for its face, or a model whose front isn't the axis we assumed.** The piece must still render (clamped inside the face), and the `front` attribute fixes the orientation without code changes. Test: Task 2 `Place_PieceLargerThanFace_ClampedInside` and the `front` axis cases.

---

## Phase 1: Drive Bay only

### Task 1: Skin data files (atlas index + layout) parsed and validated

**Files:**
- Create: `src/StashNet.Core/Skins/SkinAtlas.cs`, `src/StashNet.Core/Skins/SkinLayout.cs`, `src/StashNet.Core/Skins/SkinFormatException.cs`
- Test: `tests/StashNet.Core.Tests/SkinAtlasTests.cs`, `tests/StashNet.Core.Tests/SkinLayoutTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class SkinFormatException : Exception` (message ctor)
  - `public readonly struct UvRect { public readonly float U0, V0, U1, V1; }` (Unity convention: origin bottom-left)
  - `public sealed class SkinAtlas { public static SkinAtlas Parse(string xml); public bool TryGetUv(string sprite, out UvRect uv); public IReadOnlyCollection<string> SpriteNames { get; } }`
  - `public sealed class SkinPiece { public string Sprite; public string Face; public float U, V, W, H, Rot; public bool Glow; }`
  - `public sealed class BlockSkin { public string BlockName; public string Front; public IReadOnlyList<SkinPiece> Pieces; }`
  - `public sealed class SkinLayout { public static SkinLayout Parse(string xml, SkinAtlas atlas, List<string> warnings); public bool TryGet(string blockName, out BlockSkin skin); }`

File formats (exact):

```xml
<!-- Mod/Skins/atlas.xml: pixel rects, origin top-left (as drawn by the script) -->
<atlas width="1024" height="1024">
  <sprite name="bulletHole1" x="0" y="0" w="64" h="64" />
</atlas>

<!-- Mod/Skins/layouts.xml -->
<skins>
  <!-- front: which model-local axis the player sees as the front: +x, -x, +z, -z -->
  <block name="stashnetDriveBay" front="-z">
    <!-- face: front|back|left|right|top (left/right as seen facing the front).
         u,v: piece center as a 0..1 fraction across the face (u=0 viewer's left edge, v=0 bottom edge).
         w,h: size in meters. rot: degrees, counter-clockwise as seen from outside. glow: unlit/bright. -->
    <piece sprite="bulletHole1" face="front" u="0.7" v="0.6" w="0.06" h="0.06" rot="0" glow="false" />
  </block>
</skins>
```

- [ ] **Step 1: Write the failing tests.**
  - `SkinAtlasTests.Parse_ConvertsPixelRectToBottomLeftUv`: `width=1024 height=1024`, sprite `x=0 y=0 w=256 h=128` → `U0=0, V0=0.875, U1=0.25, V1=1` (within 1e-6).
  - `SkinAtlasTests.TryGetUv_UnknownSprite_ReturnsFalse`.
  - `SkinAtlasTests.Parse_MalformedXml_Throws` → `SkinFormatException`.
  - `SkinAtlasTests.Parse_ZeroSize_Throws` → `SkinFormatException`.
  - `SkinLayoutTests.Parse_ReadsBlockAndPieces`: the sample above → `TryGet("stashnetDriveBay")` gives `Front="-z"` and one piece with exactly those values. Missing `rot`/`glow` default to `0`/`false`.
  - `SkinLayoutTests.Parse_BadPiece_SkippedWithWarning`: a piece with an unknown sprite, one with `face="bottom"`, and one with `w="0"` are each skipped, each adds one warning naming the block and the reason, and the valid piece stays.
  - `SkinLayoutTests.Parse_BadFront_BlockSkippedWithWarning`: `front="y"` → the block is absent, with one warning.
  - `SkinLayoutTests.Parse_MalformedXml_Throws` → `SkinFormatException`.
  - `SkinLayoutTests.Parse_DuplicateBlock_LastWinsWithWarning`.
- [ ] **Step 2: Run them and watch them fail.** Command: `dotnet test tests/StashNet.Core.Tests --filter "FullyQualifiedName~Skin"`. Expected: compile errors or FAIL.
- [ ] **Step 3: Implement the three files with `System.Xml.Linq`, invariant culture.** Wrap `XmlException` and bad numbers in `SkinFormatException` for whole-file problems. Skip a single bad piece or block with a warning, and never throw for it.
- [ ] **Step 4: Run the tests and watch them pass.** Run the same command. Expected: all PASS, and the full `dotnet test` stays green.
- [ ] **Step 5: Commit.** Message: "Skins: parse atlas index and per-block decal layouts (game-free, tested)".

### Task 2: Placing a piece on a face of the model's bounds

**Files:**
- Create: `src/StashNet.Core/Skins/FacePlacement.cs`
- Test: `tests/StashNet.Core.Tests/FacePlacementTests.cs`

**Interfaces:**
- Consumes: `SkinPiece` (Task 1).
- Produces:
  - `public readonly struct Vec3 { public readonly float X, Y, Z; public Vec3(float x, float y, float z); }`
  - `public readonly struct Quad { public readonly Vec3 BottomLeft, BottomRight, TopRight, TopLeft, Normal; }` (corners as seen from outside the face)
  - `public static class FacePlacement { public const float Lift = 0.004f; public const float LayerStep = 0.0005f; public static Quad Place(Vec3 boundsMin, Vec3 boundsMax, string front, SkinPiece piece, int layer); }`

Rules:
- The face plane sits `Lift + layer * LayerStep` outside the bounds face. Later pieces in the list draw on top without z-fighting.
- "Viewer's right" for a face is the right-hand direction of someone outside that face looking at it, in Unity's left-handed space with +y up. For `front="-z"`, the front face is at `boundsMin.Z`, its normal is `(0,0,-1)` and its right is `+x`. The `right` face is the one on the viewer's right while facing the front. `top`: u runs along the front's right, and v runs from the front edge (0) to the back edge (1).
- A piece larger than its face is scaled down uniformly to fit inside it, then its center is clamped so the piece stays fully inside.

- [ ] **Step 1: Write the failing tests.** Bounds are `(-0.5,0,-0.5)–(0.5,2,0.5)` unless noted.
  - `Place_FrontMinusZ_Center`: `face=front u=.5 v=.5 w=.2 h=.2 rot=0 layer=0` gives:
    - `Normal=(0,0,-1)`
    - `BottomLeft=(-0.1, 0.9, -0.504)`
    - `TopRight=(0.1, 1.1, -0.504)`
  - `Place_FrontPlusZ_RightIsMinusX`: `front="+z"`, same piece → `BottomLeft.X = 0.1`, `Z = 0.504`.
  - `Place_RightFace_FrontMinusZ_IsPlusX`: `face=right u=0 v=0 w=.2 h=.2`, the corner case after clamping → `Normal=(1,0,0)` and all corners have `X=0.504`.
  - `Place_Rot90_SwapsCornerDirections`: `rot=90` → `BottomLeft` equals the unrotated `BottomRight` (within 1e-5).
  - `Place_Layer2_LiftsFurther`: `Z = -0.505` at `layer=2`.
  - `Place_PieceLargerThanFace_ClampedInside`: `w=3 h=0.2` on the 1 m wide front → `|BottomRight.X - BottomLeft.X| ≤ 1.0` and all X within `[-0.5, 0.5]`.
  - `Place_Top_FrontMinusZ`: `face=top u=.5 v=0` → `Normal=(0,1,0)`, and corners have `Y=2.004` with the bottom edge at `Z=-0.5` (after clamping by half of `h`).
- [ ] **Step 2: Run them and watch them fail.** Command: `dotnet test tests/StashNet.Core.Tests --filter "FullyQualifiedName~FacePlacement"`. Expected: FAIL.
- [ ] **Step 3: Implement `FacePlacement.Place`.** Map `front` plus `face` to an (origin, right, up, normal) basis on the box. Clamp, rotate the 4 half-extent offsets in-plane by `rot`, then lift along the normal. Throw `ArgumentException` for an unknown `front` or `face`; Task 1 already filters those.
- [ ] **Step 4: Run the tests and watch them pass.** Expected: PASS, full suite green.
- [ ] **Step 5: Commit.** Message: "Skins: place decal quads on model bounds faces (tested)".

### Task 3: The texture atlas script (all sprites, both phases)

**Files:**
- Create: `tools/skin/draw-skin-atlas.ps1`
- Create (generated, committed): `Mod/Skins/stashnet-skin.png`, `Mod/Skins/atlas.xml`

**Interfaces:**
- Produces: a 1024×1024 RGBA PNG with transparent background and **4 px transparent padding** around every sprite (mipmap bleed), plus `atlas.xml` in the Task 1 format, with exactly these sprite names:
  - Labels: `stencilStashnet`, `labelUnitBay`, `labelUnitCore`, `labelUnitTerminal`, `labelUnitCraft`
  - Wear: `bareMetal1`, `bareMetal2`, `bareMetal3`, `rustStreak1`, `rustStreak2`, `rustBase`
  - Damage: `bulletHole1`, `bulletHole2`, `bulletDent` (Armored: dented, not through), `dent1`, `clawGouge`, `pryMarks`, `scorch1`, `scorch2`
  - Tier stripes (chipped): `stripeBlue`, `stripeGreen`, `stripePurple`
  - Readouts (green text on near-black, with crack lines and one strip of tape drawn in): `readoutDrv4`, `readoutDrv8`, `readoutDrv12`, `readoutCore`, `readoutCraft`. Their texts:
    - `readoutDrv4`: "DRV 4/4 OK"
    - `readoutDrv8`: "DRV 8/8 OK"
    - `readoutDrv12`: "DRV 12/12"
    - `readoutCore`: "CORE OK"
    - `readoutCraft`: "CRAFT OK"
  - Extras: `ductTape`, `conduit`, `cableBundle`, `wireSpark`
  - CRT screen, 4:3: `crtScreen`. Green `#3FDC66`-style text on `#04140A`, with scan lines and darkened curved corners. Lines exactly:
    - "STASHNET v1.0"
    - "> CORE ......... OK"
    - "> DRIVES ....... 12"
    - "> LINK ........ OK"
    - "> _"
- Usage line in the header (same style as `tools/icons/draw-drive-icons.ps1`): `powershell -NoProfile -ExecutionPolicy Bypass -File tools/skin/draw-skin-atlas.ps1 -OutDir Mod/Skins`

- [ ] **Step 1: Write the script.** Reuse the drawing helpers' approach from `draw-drive-icons.ps1`: fixed `System.Random` seeds, 2× supersampling, a monospace font for readouts and the CRT, and `Courier New` bold for stencils. Pack sprites with a simple shelf packer in a fixed order. Write `atlas.xml` from the packer's rects.
- [ ] **Step 2: Run it twice and compare hashes.**
  - Commands: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/skin/draw-skin-atlas.ps1 -OutDir Mod/Skins`, then `Get-FileHash Mod/Skins/*`, then run it again and hash again.
  - Expected: identical hashes, and `atlas.xml` lists all sprite names above.
- [ ] **Step 3: Check that `atlas.xml` parses with Task 1's parser.**
  - Add a test `SkinAtlasTests.ShippedAtlas_ParsesAndHasAllSprites`. It reads `../../../../../Mod/Skins/atlas.xml` relative to the test assembly; set it as a linked `None` item copied to the output if that's easier. It asserts every name in the list above is present.
  - Run: `dotnet test`. Expected: PASS.
- [ ] **Step 4: Look at the PNG.** Open it in the browser pane or an image viewer. Check each sprite against the mockup files: dirty, worn and readable. Fix and re-run until it matches the style rule.
- [ ] **Step 5: Commit** the script, PNG, xml and test. Message: "Skins: deterministic atlas of salvage decals and the CRT screen".

### Task 4: Runtime skins on the Drive Bay

**Files:**
- Create: `src/StashNet/Skins/SkinAssets.cs`, `src/StashNet/Skins/SkinApplier.cs`, `src/StashNet/Skins/SkinOwner.cs`, `src/StashNet/Blocks/BlockStashNetDriveBay.cs`, `Mod/Skins/layouts.xml`
- Modify:
  - `src/StashNet/StashNet.csproj`: add `<Reference Include="UnityEngine.ImageConversionModule">` like the other Unity references, with `Private=false`
  - `src/StashNet/StashNetMod.cs`: store `modInstance.Path` in `public static string ModPath`. Verify the member name on `Mod` by decompiling into the scratch folder.
  - `src/StashNet/Dev/ConsoleCmdStashNet.cs`: add the subcommand `skin` (`sn skin reload`) and add it to the help text and description
  - `Mod/Config/blocks.xml`, `stashnetDriveBay` only: `Class` → `StashNet.Blocks.StashNetDriveBay, StashNet`, and add `<property name="TintColor" value="4A503F" />`

**Interfaces:**
- Consumes: `SkinAtlas`, `SkinLayout`, `BlockSkin`, `SkinPiece`, `FacePlacement.Place`, `Vec3`, `Quad` (Tasks 1–2).
- Produces:
  - `internal static class SkinAssets`
    - `public static bool TryGet(out SkinAtlas atlas, out SkinLayout layout, out Material normal, out Material glow)`: loads lazily from `ModPath/Skins/`. On any failure it logs one `[StashNet] Skins disabled: <reason>` line and returns false from then on.
    - `public static void Reload()`: drops the cache; the next `TryGet` reloads. Logs each layout warning once as `[StashNet] Skin layout: <warning>`.
  - `internal static class SkinApplier`
    - `public static void Apply(Block block, BlockEntityData bed)`
    - `public static int ReapplyAll()`: returns how many blocks it rebuilt
  - `public class SkinOwner : MonoBehaviour`. It sits on `bed.transform` and has these fields:
    - `public string BlockName`
    - `public GameObject Root`
    - `public BlockEntityData Bed`
    
    It keeps a static registry of all live owners, and `OnDestroy` destroys `Root` and leaves the registry.
  - `public class BlockStashNetDriveBay : BlockCompositeTileEntity`. It overrides `OnBlockEntityTransformAfterActivated(WorldBase, Vector3i, BlockValue, BlockEntityData)`, calls `base`, then `SkinApplier.Apply(this, _bed)`.

Decisions for `SkinApplier.Apply`:
- Return if `GameManager.IsDedicatedServer`, if `bed?.transform == null`, if there's no layout for `block.GetBlockName()`, or if `SkinAssets.TryGet` fails.
- **Bounds:** union of `MeshFilter.sharedMesh.bounds` for every `MeshFilter` under `bed.transform`, transformed into `bed.transform` local space. Log `[StashNet] Skin <block>: model bounds <min>..<max>` once per block name, which helps tune `front`.
- **Sibling root:** create `GameObject "StashNetSkin"` with `parent = bed.transform.parent` and the same `localPosition`, `localRotation` and `localScale` as `bed.transform`. It must not be a child; see Review Focus 4.
- Build **one mesh per material** in child objects `"Skin"` (normal pieces) and `"SkinGlow"` (glow pieces). Each piece is one quad from `FacePlacement.Place(..., layer = piece index)`, with UVs from `atlas.TryGetUv` and normals from `Quad.Normal`. Use `MeshRenderer` with no collider, `shadowCastingMode = Off`, `receiveShadows = true` for normal pieces and `false` for glow.
- **Idempotent:** if `bed.transform` already has a `SkinOwner`, destroy its old `Root` and reuse the owner. Otherwise add one. Set `Root`, `BlockName` and `Bed`.
- **Materials (the open risk; try in this order and log the choice once):**
  - Normal pieces:
    1. `new Material(<first model renderer>.sharedMaterial.shader)` with `mainTexture` = atlas, if the shader has `_MainTex` and `_Cutoff` (set `_Cutoff` = 0.5).
    2. `Shader.Find("Legacy Shaders/Transparent/Cutout/Diffuse")`.
    3. `Shader.Find("Unlit/Transparent Cutout")`.
  - Glow pieces: `Shader.Find("Unlit/Transparent")`, then `Shader.Find("Unlit/Transparent Cutout")`.
  - If nothing is found → "Skins disabled: no usable shader".
- **Atlas texture:** `new Texture2D(2, 2, TextureFormat.RGBA32, true)` plus `ImageConversion.LoadImage(tex, bytes, false)`, with `wrapMode = Clamp` and `filterMode = Bilinear`.
- `ReapplyAll`: for each live `SkinOwner` with a non-null `Bed`, look up the `Block` from `Bed.blockValue.Block` and call `Apply`.

`Mod/Skins/layouts.xml` content for `stashnetDriveBay` (blue tier, file cabinet; positions are starting guesses to tune in-game). The block uses `front="-z"`, and all pieces below are on the front face unless noted:

| sprite | u | v | w | h | rot | glow | notes |
|---|---|---|---|---|---|---|---|
| `bareMetal1` | 0.12 | 0.95 | 0.18 | 0.12 | | | |
| `bareMetal2` | 0.92 | 0.55 | 0.12 | 0.18 | | | |
| `scorch1` | 0.70 | 0.86 | 0.30 | 0.22 | | | |
| `scorch2` | 0.30 | 0.12 | 0.26 | 0.20 | | | |
| `rustStreak1` | 0.25 | 0.62 | 0.06 | 0.40 | | | |
| `rustStreak2` | 0.80 | 0.22 | 0.07 | 0.50 | | | |
| `rustBase` | 0.5 | 0.03 | 0.62 | 0.10 | | | |
| `clawGouge` | 0.30 | 0.55 | 0.22 | 0.34 | 15 | | |
| `bulletHole1` | 0.72 | 0.60 | 0.05 | 0.05 | | | |
| `bulletHole2` | 0.80 | 0.54 | 0.05 | 0.05 | 40 | | |
| `bulletHole1` | 0.33 | 0.18 | 0.05 | 0.05 | 120 | | |
| `stencilStashnet` | 0.5 | 0.91 | 0.40 | 0.07 | | | |
| `labelUnitBay` | 0.5 | 0.86 | 0.30 | 0.04 | | | |
| `stripeBlue` | 0.5 | 0.27 | 0.62 | 0.05 | | | |
| `readoutDrv4` | 0.5 | 0.76 | 0.30 | 0.11 | | true | |
| `conduit` | | | | | | | **right** face: u 0.5, v 0.5, w 0.05, h 1.6 |
| `wireSpark` | | | | | | | **right** face: u 0.6, v 0.45, w 0.18, h 0.14 |

- [ ] **Step 1: Add the reference, `ModPath`, `SkinOwner`, `SkinAssets`, `SkinApplier`, `BlockStashNetDriveBay`, the `sn skin reload` command and `layouts.xml`.** The `skin reload` output is `Skins reloaded: <n> blocks rebuilt.`
- [ ] **Step 2: Change `stashnetDriveBay` in `blocks.xml`** (Class and TintColor) **and check it builds.**
  - Run: `dotnet build src/StashNet/StashNet.csproj -c Release`.
  - Expected: success, and `dist/StashNet/Skins/` contains `stashnet-skin.png`, `atlas.xml` and `layouts.xml`. `StageMod` copies all of `Mod/**`, so no csproj change is needed for this.
- [ ] **Step 3: Run the full test suite.** Command: `dotnet test`. Expected: all PASS.
- [ ] **Step 4: Commit.** Message: "Skins: runtime decals on the Drive Bay (sibling mesh, reloadable), army-green tint".

### Task 5: Drive Bay icon

**Files:**
- Create: `tools/icons/draw-block-icons.ps1`
- Create (generated, committed): `Mod/UIAtlases/ItemIconAtlas/stashnetDriveBay.png`
- Modify: `Mod/Config/blocks.xml` (`stashnetDriveBay`): remove `CustomIcon` and `CustomIconTint`

**Interfaces:**
- Produces: `draw-block-icons.ps1 -GameDir <path> -OutDir Mod/UIAtlases/ItemIconAtlas [-Only <blockName>]`. Phase 2 extends it to all 8 blocks. Its table maps block name → vanilla icon file in `<GameDir>\Data\ItemIcons\`, tint, tier stripe color and readout sprite text. Phase 1 has one row: `stashnetDriveBay` → `cntFileCabinetTallClosed.png`, tint `4A503F`, stripe `3A6FB0`.

Steps:
- [ ] **Step 1: Write the script.**
  - Load the 160×160 vanilla icon and recolor it army green: keep luminance and multiply by the tint.
  - Then draw a chipped tier stripe, the stencil, 2–3 bullet holes, a scorch blotch, rust streaks and a tiny green readout in the same visual language as the atlas.
  - Use fixed seeds and the drive-icon helpers' approach.
  - Write `<blockName>.png`.
- [ ] **Step 2: Run it twice and compare hashes.** Command: `powershell -NoProfile -ExecutionPolicy Bypass -File tools/icons/draw-block-icons.ps1 -GameDir "G:\SteamLibrary\steamapps\common\7 Days To Die" -OutDir Mod/UIAtlases/ItemIconAtlas`. Expected: identical hashes on both runs.
- [ ] **Step 3: Look at it** next to `stashnetDriveBasic.png`. They should look like the same family of worn icons. Then remove `CustomIcon`/`CustomIconTint` from `stashnetDriveBay` and run the build. Expected: success.
- [ ] **Step 4: Commit.** Message: "Drive Bay icon: army-green salvage version of the file cabinet icon, plus the script".

### Task 6: Version, owner check, tuning (owner gate)

**Files:**
- Modify: `Mod/ModInfo.xml` and `src/StashNet/StashNetMod.cs` (`Version`) → `0.10.0`
- Create: `docs/testing/skin-checklist.md`, Part 1 (Drive Bay)
- Modify: `CLAUDE.md` RESUME HERE (state and next step)

- [ ] **Step 1: Bump the version, build, and run `dotnet test`.** Expected: both succeed.
- [ ] **Step 2: Write the Part 1 checklist in plain language for the owner.**
  - **Before installing:** nothing to pick up yet (the terminal change is Phase 2).
  - **Checks:**
    - Place a Drive Bay and look at it from front, sides and top, by day and at night.
    - Nothing floats away from the cabinet or flickers into it.
    - The blue stripe stays blue (not green).
    - The green readout is readable in the dark.
    - A vanilla file cabinet elsewhere looks unchanged.
    - Upgrade it to Reinforced: the old skin goes away, and the Reinforced bay looks as before (Phase 2 dresses it).
    - Walk 200 m away and back: the skin is still there, once.
    - Pick the bay up: no skin left floating.
    - The icon matches.
    - Send the newest log.
  - **Tuning:** tell me what to move, and I'll edit `layouts.xml`. After I copy the new file in, run `sn skin reload` (F1) to see it without restarting.
- [ ] **Step 3: Ask the owner "Can I deploy v0.10.0? (game closed)".** Check that `7DaysToDie` isn't running. Only on a yes: `dotnet build src/StashNet/StashNet.csproj -c Release -p:Deploy=true`.
- [ ] **Step 4: After the owner's session, read the newest client log.**
  - Expected:
    - `Loaded v0.10.0`
    - one `Skin stashnetDriveBay: model bounds` line
    - no `Skins disabled` line
    - no StashNet exceptions
  - If the shader fallback order failed or looks wrong (too dark, no cutout, not glowing), fix it in `SkinApplier` and repeat from Step 3.
- [ ] **Step 5: Tune with the owner.** Edit `Mod/Skins/layouts.xml`, including `front` if the bounds log shows the front is another axis. Each tuning round re-deploys only after a yes; for a layouts-only change, copying just that file is fine with the owner's yes. The owner runs `sn skin reload`.
- [ ] **Step 6: Record and commit.** Record the owner's verdict, the shader that worked and the final tint in `CLAUDE.md` and the design doc ("Open technical risk" → settled). Commit with message "v0.10.0: Drive Bay salvage look (owner-checked)".
- [ ] **GATE: stop here until the owner approves the Drive Bay look in-game.** If they ask for a different direction, go back to the design doc before Phase 2.

---

## Phase 2: Everything else (after the gate)

Every block below follows the same recipe:
- its class calls `SkinApplier.Apply` in `OnBlockEntityTransformAfterActivated`, after `base`
- a `TintColor`
- a `layouts.xml` block entry using the Drive Bay entry as the template, with the tier's stripe and readout, and the same density of damage
- an icon row in `draw-block-icons.ps1`, with `CustomIcon`/`CustomIconTint` removed

Positions are tuned in-game with `sn skin reload`, as in Task 6.

### Task 7: Other bays and all Cores

**Files:**
- Modify:
  - `src/StashNet/Blocks/BlockStashNetCore.cs`: add the override
  - `Mod/Config/blocks.xml`: Reinforced and Armored bays → `Class="StashNet.Blocks.StashNetDriveBay, StashNet"`; all six blocks get `TintColor`; remove `CustomIcon*`
  - `Mod/Skins/layouts.xml`
  - `tools/icons/draw-block-icons.ps1`
- Create (generated): 5 icons `stashnetDriveBayReinforced.png`, `stashnetDriveBayArmored.png`, `stashnetScrapCore.png`, `stashnetStorageCore.png`, `stashnetAdvancedCore.png`

**Interfaces:**
- Consumes: `SkinApplier.Apply(Block, BlockEntityData)`, `BlockStashNetDriveBay` (Task 4).

Per block (vanilla icon file → tint, stripe, readout, specifics from the spec):

| Block | Vanilla icon | Tint | Stripe | Readout | Specifics |
|---|---|---|---|---|---|
| `stashnetDriveBayReinforced` | `decoServerRackWhite.png` | `4A503F` | `stripeGreen` | `readoutDrv8` | `cableBundle` spilling at the bottom front; one rack unit "hanging half out" is drawn as a `bareMetal` + `dent1` piece |
| `stashnetDriveBayArmored` | `cntGunSafeWhite.png` | `3A3F31` (replaces `404040`) | `stripePurple` | `readoutDrv12` | `bulletDent` only, **no** `bulletHole*`; `pryMarks` along the door edge |
| `stashnetScrapCore` | `electricalBox02.png` | `4A503F` | `stripeBlue` | `readoutCore` | `labelUnitCore` |
| `stashnetStorageCore` | `utilityTransformer.png` | `4A503F` | `stripeGreen` | `readoutCore` | curved model: fewer, smaller pieces on its flattest face |
| `stashnetAdvancedCore` | `controlPanelBase01.png` | `4A503F` | `stripePurple` | `readoutCore` | |

- [ ] **Step 1: Add the Core override and the XML changes, then build.** Command: `dotnet build ... -c Release`. Expected: success.
- [ ] **Step 2: Add the 5 layout entries and the 5 icon rows.** Run the icon script twice. Expected: identical hashes.
- [ ] **Step 3: Run `dotnet test`.** Expected: PASS.
- [ ] **Step 4: Commit.** Message: "Skins: Reinforced/Armored bays and all Core tiers".

### Task 8: Storage Terminal becomes the CRT TV

**Files:**
- Modify:
  - `src/StashNet/Blocks/BlockStashNetTerminal.cs`: add the override
  - `Mod/Config/blocks.xml` (`stashnetTerminal`):
    - `Model` → `@:Entities/Electrical/tvCRTPlayerOnPrefab.prefab`
    - remove `DisplayType`, `MultiBlockDim` and `ModelOffset`
    - keep `GndAlign=1` and `Place=TowardsPlacerInverted`
    - add `<property name="Collide" value="movement,melee,bullet,arrow,rocket" />`
    - add `TintColor 4A503F` (only if it doesn't muddy the TV's glass; otherwise leave the TV untinted and rely on decals)
    - remove `CustomIcon*`
  - `Mod/Skins/layouts.xml`: the terminal entry is `crtScreen` (glow) covering the glass on the front, plus `labelUnitTerminal`, `stencilStashnet`, scuffs (`bareMetal*`, `scorch1`), one `bulletHole1` on the casing and `rustStreak1`
  - `tools/icons/draw-block-icons.ps1`: a row for `stashnetTerminal` from `tvCRT_Player.png`, with the green text screen composited on the glass
- Modify: `docs/testing/skin-checklist.md`, Part 2. Its first line: "**Before installing:** pick up every Storage Terminal in your test world (they're 2 wide now and become 1 wide). Place them again afterwards."

- [ ] **Step 1: Make the XML and class changes, then build.** Expected: success.
- [ ] **Step 2: Add the layout entry and the icon row.** Run the icon script twice. Expected: identical hashes.
- [ ] **Step 3: Run `dotnet test`.** Expected: PASS.
- [ ] **Step 4: Commit.** Message: "Storage Terminal: vanilla CRT TV with the StashNet green text screen".

### Task 9: Crafting Terminal

**Files:**
- Create: `src/StashNet/Blocks/BlockStashNetCraftingTerminal.cs`. It's `public class BlockStashNetCraftingTerminal : BlockWorkstation` and overrides `OnBlockEntityTransformAfterActivated` (base, then Apply).
- Modify: `Mod/Config/blocks.xml` (`stashnetCraftingTerminal`): add `<property name="Class" value="StashNet.Blocks.StashNetCraftingTerminal, StashNet" />`, add `TintColor 4A503F`, and remove `CustomIcon*` (also remove them from the `Extends` param1 list).
- Modify: `Mod/Skins/layouts.xml` (`readoutCraft`, `labelUnitCraft`, damage) and `tools/icons/draw-block-icons.ps1` (a row from `workbench.png`).

- [ ] **Step 1: Make the class and XML changes, then build.**
  - Expected: success.
  - Confirm by grep that `src/StashNet/Crafting/` finds the block by its `StashNetCraftingTerminal` marker property, not by class type. If anything checks `is BlockWorkstation`, the subclass still satisfies it.
- [ ] **Step 2: Add the layout entry and the icon row.** Run the icon script twice. Expected: identical hashes.
- [ ] **Step 3: Run `dotnet test`.** Expected: PASS.
- [ ] **Step 4: Commit.** Message: "Crafting Terminal: salvage look on the workbench".

### Task 10: Screen flicker (cheap or dropped)

**Files:**
- Create: `src/StashNet/Skins/SkinFlicker.cs`. It's `public class SkinFlicker : MonoBehaviour`, added by `SkinApplier` to the `"SkinGlow"` object only.
- Modify: `src/StashNet/Skins/SkinApplier.cs`

Decision:
- Every 0.08–0.25 s (random per instance, seeded from block position), the screen dims to 55–85% brightness with a 10% chance, otherwise stays at 100%.
- Brightness is set through a `MaterialPropertyBlock` color on that one renderer, never through the shared material.
- `enabled = false` when the renderer isn't visible (`OnBecameInvisible` / `OnBecameVisible`).
- If it shows up in profiling or the owner dislikes it, delete the class and its one `AddComponent` line.

- [ ] **Step 1: Implement it, build, and run `dotnet test`.** Expected: success, PASS.
- [ ] **Step 2: Commit.** Message: "Skins: subtle flicker on green screens".

### Task 11: Full owner check and notes

**Files:**
- Modify:
  - `Mod/ModInfo.xml` and `StashNetMod.Version` → `0.10.1`
  - `docs/testing/skin-checklist.md`, Part 2
  - `CLAUDE.md`: Conventions → "vanilla shapes with StashNet skins, custom icons for all blocks and drives"; Tier looks → terminal is the CRT; RESUME HERE
  - `docs/testing/M9-checklist.md`, A6 "New looks": describe the salvage look and the CRT terminal

Steps:
- [ ] **Step 1: Write the Part 2 checklist.**
  - Every block in all tiers matches the description.
  - Upgrades swap the look in place.
  - The CRT terminal sits flat on the floor, opens and works as before.
  - These vanilla blocks look unchanged: TV, safe, server rack, file cabinet, electrical box, transformer, control panel, workbench.
  - FPS is fine with about 30 StashNet blocks in view.
  - The icons match.
  - Send the log.
- [ ] **Step 2: Bump the version, build, and run `dotnet test`.** Then ask "Can I deploy v0.10.1? (game closed)". Deploy only on a yes.
- [ ] **Step 3: Read the log after the owner's session.**
  - Expected:
    - `Loaded v0.10.1`
    - one bounds line per block name
    - no `Skins disabled` line
    - no StashNet exceptions
  - Tune with `sn skin reload` as needed.
- [ ] **Step 4: Record the owner's verdict in `CLAUDE.md` and commit.** Message: "v0.10.1: salvage look on all blocks (owner-checked)". The next step is M9 Part A (dedicated server), which also covers Review Focus 2 on a server.
