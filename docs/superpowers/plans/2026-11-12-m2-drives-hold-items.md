# Milestone 2: Drives Hold Items Implementation Plan

> **For agentic workers:** Implement task by task (superpowers:executing-plans). Steps use checkbox (`- [ ]`) syntax.

**Goal:** A drive item stores real items (with quality, durability, and mods intact) within its capacity, and the contents survive save/reload and moving the drive around.

**Architecture:** A game-free library `StashNet.Core` holds drive contents and capacity rules plus a text serializer, with xUnit tests. Its source files are also compiled straight into the mod DLL (no second DLL to load). A thin game layer converts `ItemValue` ↔ drive entries and stores the serialized text in the drive item's metadata (proven in M0). Console commands drive the in-game test, since the Terminal UI comes in M3.

**Tech Stack:** netstandard2.0 (Core), net10.0 + xUnit (tests), net48 (mod), 7DTD V3.4 `ItemValue` metadata.

**Spec:** `docs/PLAN.md` §2.3, roadmap row 2.

## Global Constraints
- **Never lose items.** Every operation removes from the source only what the destination actually accepted. Unreadable drive data is never overwritten. The drive is refused instead.
- **Items are stored by name, never by numeric ID.** Numeric item IDs shift when mods change. Complex items record their own name and the names of nested mods, and those are remapped on load.
- Capacity is read from the drive item's XML properties `StashNetMaxTypes` / `StashNetMaxItems`. Basic Drive: **15 types / 1,500 items**.
- A "type" = one entry = (item name, exact variant). Plain stackable items of the same name share one entry. Items with quality/durability/mods/metadata are a separate entry per distinct variant.
- Drives cannot go inside drives.
- Metadata key on the drive item: `StashNetDrive`. Serialized format header: `SND1`.

## Review Focus
1. **Corrupt/unknown drive data** → the drive is refused with a message, and the data is left untouched. (Core test: parse failure throws `DriveFormatException`. The glue catches it and aborts.)
2. **Backpack full on withdraw** → whatever doesn't fit stays on the drive. Nothing vanishes. (The glue extracts only what the bag accepted. Checklist step.)
3. **Capacity lowered after items are stored** (config change) → existing items are kept, and new inserts are refused. (Core test.)
4. **Weapon/tool with quality, durability and attachments** round-trips identically. (Checklist step. The glue keeps the full variant.)
5. **An item whose mod was uninstalled** (name no longer exists) → the entry is kept as-is and skipped on withdraw, not deleted. (Glue + checklist note.)

---

### Task 1: StashNet.Core: DriveContents + DriveSerializer (TDD)

**Files:**
- Create: `src/StashNet.Core/StashNet.Core.csproj` (netstandard2.0)
- Create: `src/StashNet.Core/Drives/DriveCapacity.cs`, `DriveEntry.cs`, `DriveContents.cs`, `DriveSerializer.cs`, `DriveFormatException.cs`
- Create: `tests/StashNet.Core.Tests/StashNet.Core.Tests.csproj` (net10.0, xUnit), `DriveContentsTests.cs`, `DriveSerializerTests.cs`

**Interfaces (Produces):**
- `readonly struct DriveCapacity(int maxTypes, int maxItems)` with `MaxTypes`, `MaxItems`
- `sealed class DriveEntry { string Name; string Variant /* null = plain */; int Count }`
- `sealed class DriveContents`
  - `IReadOnlyList<DriveEntry> Entries`, `int TypeCount`, `int TotalCount`
  - `int TryInsert(DriveCapacity cap, string name, string variant, int count)`: returns the number accepted (0..count)
  - `int Extract(string name, string variant, int count)`: returns the number removed. Removes the entry at 0.
  - `int CountOf(string name, string variant)`
- `static class DriveSerializer`: `string Serialize(DriveContents)`, `DriveContents Deserialize(string)` (null/empty → empty drive; bad data → `DriveFormatException`)

**Tests (names → assertions):**
- `Insert_IntoEmpty_AcceptsAll`: insert 100 wood → 100, TypeCount 1, TotalCount 100
- `Insert_RespectsMaxItems`: cap(15,1500), insert 1400 then 200 of another → second returns 100
- `Insert_NewType_WhenTypesFull_ReturnsZero`: cap(2,1500), 2 types present, a third → 0, TypeCount 2
- `Insert_ExistingType_WhenTypesFull_StillAccepted`: same setup, more of an existing type → accepted
- `Insert_SameNameDifferentVariant_IsSeparateType`: wood(null) + wood("v1") → TypeCount 2
- `Insert_ZeroOrNegative_ReturnsZero`
- `Extract_Partial_KeepsEntry` / `Extract_All_RemovesEntry` / `Extract_More_ThanStored_ReturnsStored` / `Extract_Missing_ReturnsZero`
- `OverCapacity_ExistingItemsKept_InsertRefused`: entries over the cap still enumerate. A further insert → 0.
- `Serializer_RoundTrip_PreservesEntriesAndOrder` (including variants containing `\t`, `\n`, `\\`, unicode)
- `Serializer_Empty_And_Null_GiveEmptyDrive`
- `Serializer_BadHeader_Throws` / `Serializer_BadCount_Throws` / `Serializer_TruncatedLine_Throws`

- [ ] Write the tests, run them (`dotnet test`), and see them fail.
- [ ] Implement until green. Commit.

### Task 2: Game glue: ItemValue ↔ entries, drive load/save

**Files:**
- Modify: `src/StashNet/StashNet.csproj`: compile `src/StashNet.Core/**/*.cs` into the mod
- Create: `src/StashNet/Drives/ItemCodec.cs`, `src/StashNet/Drives/DriveItem.cs`
- Modify: `Mod/Config/items.xml`: add `StashNetMaxTypes` 15 / `StashNetMaxItems` 1500 to the Basic Drive

**Interfaces:**
- `static class ItemCodec`
  - `bool IsPlain(ItemValue iv)`: stackable && quality 0 && useTimes 0 && meta 0 && no metadata && no non-empty mods/cosmetics
  - `string EncodeVariant(ItemValue iv)`: null if plain. Else `names|base64(ItemValue.Write)`, where `names` = comma-joined item names in the order: root, then mods/cosmetics depth-first.
  - `bool TryDecode(string name, string variant, out ItemValue iv)`: plain → `ItemClass.GetItem(name)`. Variant → read blob, then reassign `type` from names (root + nested, same order). Any unknown name → false.
- `static class DriveItem`
  - `bool TryGetCapacity(ItemValue drive, out DriveCapacity cap)`
  - `bool TryLoad(ItemValue drive, out DriveContents contents, out string error)`
  - `void Save(ItemValue drive, DriveContents contents)`: `SetMetadata("StashNetDrive", Serialize(...))`

- [ ] Implement. `dotnet build` with 0 warnings.

### Task 3: Console commands for in-game testing

**Files:** Modify `src/StashNet/Dev/ConsoleCmdStashNet.cs`

- `stashnet drive`: hold a drive. Lists entries (localized name, count, a "★" marker for variants) and `types x/15, items y/1500`.
- `stashnet drive deposit`: moves every backpack stack (not drives) onto the held drive, as capacity allows. Partial stacks stay in the backpack.
- `stashnet drive withdraw`: moves drive contents into the backpack, as space allows. The rest stays on the drive. Entries whose item no longer exists are skipped and reported.
- `stashnet drive wipe`: clears the held drive (testing only).
- Drive-in-drive: deposit skips items tagged `stashnetDrive`.

- [ ] Implement. Build. Version → 0.2.0.

### Task 4: Checklist + docs + commit
- [ ] `docs/testing/M2-checklist.md`: deposit/withdraw plain items, capacity limits (types and items), a weapon with quality/mods round-trip, full backpack on withdraw, save/reload, drive moved through a Drive Bay and a regular box.
- [ ] Update `CLAUDE.md`. Commit and push.
