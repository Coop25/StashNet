# StashNet — 7 Days to Die Storage Network Mod

*Working name; rename any time.* Design + build plan, 2026-11-12.

A centralized storage network for 7 Days to Die, inspired by the idea of Applied Energistics (not a copy of it): cobble together a crude, unpowered network in the first week, then grow it into an armored, powered system. Items live on physical drives, everything is protected by one password, and a big hoard runs hot enough to draw screamers.

---

## 1. Who does what

You said you've never made a mod, a texture, or a DLL. That's fine — here's the split:

| Claude does | You do |
|---|---|
| Write all C# code (the DLL) and XML config | Install a few free tools once (section 6) |
| Build the DLL and copy the mod into your game folder | Tell me your game version and install path |
| Write test checklists for each milestone | Launch the game (with EAC off) and run the checklist |
| Read the game logs and fix bugs | Send me the log file / describe what went wrong |
| Make placeholder icons | Decide on balance ("this feels too cheap") |

**Textures and models:** v1 needs **zero** custom art. 7DTD lets a new block borrow the look of an existing one (there are plenty of computer, server-box and safe models in the game) and lets item icons reuse a vanilla icon with a color tint. Custom icons are simple PNGs I can generate. Fully custom 3D models need Unity and are an optional polish step at the very end — skippable.

**About DLLs:** you're right, this needs one. 7DTD mods come in two layers:
- **XML** — defines the blocks, items, recipes and UI layouts. Easy, readable.
- **DLL (C#)** — the actual behavior: networks, drives storing items, terminals, crafting from storage. I write this; you never have to open it.

DLL mods require running the game **without EasyAntiCheat** (the launcher has an option for this). Anyone joining a server with this mod needs the same mod installed.

---

## 2. Design (what we're building)

### 2.1 Blocks and items

| Piece | Type | What it does |
|---|---|---|
| **Scrap Storage Core** | Block | The network's brain. One per network. Holds the password. Emits heat. |
| **Drive Bay** | Block | Holds 4 drives. Upgradeable armor (2.4). |
| **Basic / Standard / High-Density Drive** | Item | Where items actually live. Contents travel with the drive. |
| **Storage Terminal** | Block | Browse, search, sort, deposit, withdraw. Has a Drive Port slot. |
| **Crafting Terminal** | Block | Storage Terminal + a full workbench that crafts from network stock. |
| **Drive Upgrade Kit** (Basic→Standard, Standard→HD) | Item | Upgrades a drive in place, keeping its contents. |
| *Later:* Core upgrades, Network Relay, Heat Sink | Block | See section 4 |

### 2.2 How a network forms

- **Early game: proximity, no power, no wires.** Bays and terminals within the Core's radius join automatically.
  - Scrap Core radius: **6 blocks**. Higher Core tiers: **12**, then **24**.
- **One Core per network.** A bay or terminal in range of two Cores joins the nearest one.
- **Later: powered Network Relays** (wired with the vanilla wire tool) extend reach beyond the radius.
- **Terminals do not need power in v1.** Powered tiers come later as an upgrade, never a requirement.

### 2.3 Drives

- Each drive stores its own contents. Pull it out → the items leave with it.
- Capacity is **stacks only**. Each item type uses `ceil(count ÷ its stack size)` stacks, like slots in a chest: 6,000 stone = 1 stack, 3 rifles = 3 stacks. There is no separate type limit. *(Changed 2026-11-12, twice: raw item counts made 6,000-stack resources overflow a drive, and a per-drive type limit double-counted items spread across drives.)* All numbers are tunable in items.xml:

| Drive | Stacks | Tier |
|---|---|---|
| Basic | 24 | Early (scrap, duct tape, wood) |
| Standard | 96 | Mid (forged iron, electrical parts) |
| High-Density | 384 | Late (forged steel, circuitry) |

  For scale: a vanilla wooden crate holds 48 stacks, so a Drive Bay of 4 Basic Drives (96 stacks) is about two crates.

- **A drive is only readable inside a network.** In a backpack it's a sealed object, not a pocket inventory.
- A destroyed Drive Bay **drops its drives in a loot bag, contents intact.**
- Items with quality/durability/mods (weapons, tools, armor) are stored individually with their stats intact; plain resources stack.

### 2.4 Armored Drive Bays

Uses 7DTD's normal block-upgrade system (hit it with a hammer/nailgun holding the upgrade materials):

| Tier | Upgrade cost | Toughness target |
|---|---|---|
| Drive Bay | base craft | about a vanilla mid-tier block |
| Reinforced Drive Bay | forged iron + mechanical parts | about concrete |
| Armored Drive Bay | forged steel + mechanical parts | about a steel block — **hard cap, never above** |

Mid/late-game, no god-tier materials.

### 2.5 Terminals and the Drive Port

- **Storage Terminal:** combined view of all drives — search box, sort (name / count / category), click to withdraw, drag from inventory to deposit. Deposits go to a drive that already holds that item, otherwise the drive with the most free room.
- **Drive Port** (a slot in every terminal):
  - **Unload to network** — moves the drive's contents onto the network's other drives. If there isn't room, moves what fits and leaves the rest. **Never deletes anything.**
  - **Load from drive** — the reverse: absorbs a drive's contents into the network.
  - **Format** — wipes an *empty* drive.
- **Crafting Terminal:** behaves exactly like a vanilla workbench (same recipes, same perk/skill gates) but pulls ingredients from the network first, then your backpack. Its recipe **includes a vanilla workbench**, so it can't skip progression.

### 2.6 Password / access

- **Set the password on the Core.** Every connected bay and terminal uses it automatically, including ones added later.
- **Enter once:** a correct password adds you to the network's allowed list (like vanilla keypad locks).
- **Changing the password kicks everyone** except the owner off the allowed list.
- **Owner always has access.**
- Protected: opening terminals, inserting/removing drives, the Drive Port, picking up network blocks.
- **Not** protected: being smashed by zombies/explosives/raiders. That's what armor is for.
- **Drives are stamped with the network's lock.** Loading a stamped drive into a *different* network, or formatting it, asks for the original password. Stolen drives are useless bricks.

### 2.7 Heat

Uses the game's existing heat system (the same one forges and campfires feed, which spawns screamers):

- Heat per Core = **(drives in the network) × (heat per drive for that Core tier)**, with a cap.
- Better Cores run cooler per drive — upgrading the Core is also about stealth.
- Small extra heat while a terminal is open or the Crafting Terminal is crafting.
- Cap keeps big late-game bases at "regular screamer visits," not "endless screamers."
- *Later:* a powered **Heat Sink** block that reduces network heat.

All heat numbers are placeholders until playtested.

### 2.8 Multiplayer

The server owns all the data (who's allowed, what's on each drive). Clients only send requests ("withdraw 50 nails") and get updates back. This prevents two players grabbing the same stack and duplicating it.

---

## 3. How it's built (technical approach)

- **Mod layout:** standard 7DTD mod folder — `ModInfo.xml`, `Config/*.xml` (blocks, items, recipes, UI), `StashNet.dll`.
- **Drive Bay** is built on the game's own **secure, password-capable storage container** — so locks, saving, and "drop contents when destroyed" mostly come for free. It only accepts drive items.
- **Drive contents** are saved as extra data attached to the drive item itself, so they travel with it automatically.
- **Core logic in a separate, game-free library** (drive storage math, merging drives into one view, deposit/withdraw rules, password rules, heat formula). This part gets automated tests that run without launching the game — so most bugs get caught before you ever load in.
- **Game glue** (blocks, UI, networking, crafting hooks) is a thin layer on top, tested in-game with checklists.
- **Harmony** (bundled with the game) is used to hook crafting so the Crafting Terminal can pull from the network.

### Unknowns to prove first (Milestone 0)

These are the things most likely to force a design change, so we test them before building on them:

1. Can a drive item hold enough saved data for 100 item types without breaking saves or multiplayer sync? (Fallback: store contents in a mod save file, keyed by a unique drive ID stamped on the item.)
2. Can the Drive Bay reuse the vanilla secure container cleanly, restricted to drives only?
3. What's the exact hook into the game's heat system?
4. Which game version are you on? (Mods often break between major versions.)

**Milestone 0 findings (from reading the game's code, V3.4 b10):**
1. ✅ Confirmed in-game (2026-11-12): fake drives of 100 types (~3.4 KB) and 1,000 types survived save/reload, storing in a box, and drop/pickup. **Drive contents live on the drive item; no fallback needed.**
2. ✅ Storage blocks are built from swappable features (storage, lock, ...). Our own feature classes get picked up by name, so the Drive Bay = lockable storage + a StashNet feature.
3. ✅ The heat hook exists (`AIDirector.NotifyActivity`). It already switches off during blood moons and when the heat setting is off.
4. ✅ V3.4.0 (b10).

---

## 4. Build roadmap

Each milestone ends with something you can load and try in-game. A detailed step-by-step task plan gets written at the start of each milestone, once the previous one has confirmed how the game behaves.

| # | Milestone | You can test… |
|---|---|---|
| **0** | **Setup + proof of concept.** Dev tools, empty mod that loads, answers to the 4 unknowns. | Game loads with mod; log shows "StashNet loaded". |
| **1** | **Blocks & items exist.** Core, Drive Bay, Basic Drive, Storage Terminal — vanilla looks, tinted icons, recipes. Bay only accepts drives. | Craft and place everything; put drives in a bay. |
| **2** | **Drives hold items.** Game-free storage library + tests; saving contents on the drive item. | Items survive save/reload and moving a drive between bays. |
| | *Status: M0 ✅ M1 ✅ M2 ✅ M3a ✅ (2026-11-12)* | |
| **3** | **Network + Storage Terminal.** Proximity discovery, combined view, search/sort, deposit/withdraw. | One screen shows every drive's contents; take and put items. |
| **4** | **Password.** Core lock, propagation, allowed list, drive stamping. | A second player is locked out until they enter the code. |
| **5** | **Drive Port.** Unload / load / format. | Empty a full drive into the network; stolen drive asks for password. |
| **6** | **Crafting Terminal.** Workbench recipes using network stock. | Craft something without carrying the ingredients. |
| **7** | **Heat.** Formula, cap, terminal-use spikes. | Big network → screamers show up. |
| **8** | **Progression.** Standard/HD drives, Upgrade Kits, Reinforced/Armored bays, Core tiers 2–3 and radius. | Upgrade a full drive without losing anything; armored bay soaks damage. |
| **9** | **Multiplayer hardening + release package.** Dedicated server test, dupe tests, zip for sharing. | Two players use one network on a server at once with no dupes. |

**v1 = milestones 0–9.**

### Later (parked, not in v1)

- **Backup system** — snapshot-based Backup Drive → Backup Vault → Trader Escrow, with the anti-duplication rules (snapshots locked until the Core is destroyed; generation numbers make stale drives unreadable; restoring consumes the snapshot; blood-moon sync lockout).
- **Powered Network Relays** (wired range extension) and **Heat Sink**.
- **Container Link** — pull your existing chests into the terminal view.
- **More stations** in the Crafting Terminal (forge, chem station).
- **Autocrafting** — recipe cartridges, crafting queue, sub-recipes.
- **Custom 3D models and textures.**

---

## 5. Risks

| Risk | Mitigation |
|---|---|
| Game update breaks the mod | Keep hooks into game code few and isolated; note the game version on every release. |
| Item duplication bugs | Server owns all data; dupe tests in milestone 9; no live mirrors anywhere. |
| Drive data too large for an item | Milestone 0 spike; fallback to a save file keyed by drive ID. |
| Too strong / too cheap | All numbers in one config XML; tune after playtesting. |
| Players connecting without the mod | Clearly documented: server and all players need it, EAC off. |

---

## 6. What you'll need to install (once)

I'll walk you through each when we start Milestone 0:

1. **7 Days to Die** (you have it) — tell me the version (main menu, bottom corner) and install folder.
2. **.NET SDK** — free Microsoft tool that lets me compile the DLL. One installer.
3. **Optional: Visual Studio 2022 Community** — only if you want to read/edit the code yourself. Not required; I can build from the command line.
4. **Optional: Git** — keeps a history of every change so nothing gets lost.

You will **not** need Unity, Blender, or any art tools for v1.
