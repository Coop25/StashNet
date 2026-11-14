# StashNet

A storage network mod for **7 Days to Die**. Cobble together a crude storage network in your first week, then grow it into a big, armored, late-game system.

- **Physical drives** hold your items. Pull a drive out and its contents go with it.
- **Storage Terminal**: every drive in one window, with search, sort, deposit and withdraw.
- **Crafting Terminal**: a real workbench that can use ingredients stored on the network.
- **One code** set on the Core protects the network's Drive Bays and Terminals.
- **Tiers**: bigger drives, tougher bays that hold more drives, and Cores that reach farther. You upgrade bays and Cores in place with a hammer.
- **Heat**: a big hoard runs hot and draws screamers, just like a forge does.

**Version 0.9.1.** Playable and tested in single-player on 7 Days to Die **V3.4 (b10)**. Multiplayer is still being tested.

---

## Requirements

- 7 Days to Die **V3.4**. Other versions may not work.
- **EasyAntiCheat must be off**, because the mod includes a DLL.
- **Multiplayer:** the server **and every player** need the same version of StashNet installed.

## Install

1. Download `StashNet-v<version>.zip` from the GitHub Releases page (or get it from whoever runs your server). Everyone on a server, the server included, needs the same version.
2. Extract it into your game's `Mods` folder, so you end up with `7 Days To Die\Mods\StashNet\ModInfo.xml`. (Steam: right-click the game → Manage → Browse local files.)
3. Start the game **with EasyAntiCheat turned off** (in the game's launcher, switch off EasyAntiCheat before pressing Play).
4. In the console (F1) you should see `[StashNet] Loaded v0.9.1`.

**Dedicated server:** put the same `StashNet` folder in the server's `Mods` folder, and set `<property name="EACEnabled" value="false"/>` in its `serverconfig.xml`.

## ⚠️ Removing the mod

Everything stored on drives lives inside StashNet's items. If you remove the mod from a world that has StashNet blocks or drives, **those blocks, drives and everything on them are lost**. Before uninstalling, withdraw what you want to keep and pick up or break the StashNet blocks.

---

## Getting started

1. **Craft** (from your backpack): a **Scrap Storage Core**, a **Drive Bay**, a few **48-Stack Drives**, and a **Storage Terminal**.
2. **Place the Core**, then place the Drive Bay and the Terminal **within 6 blocks** of it.
3. **Put drives in the Drive Bay.** A bay only accepts drives.
4. **Use the Terminal**: click an item to take one (shift-click for a full stack), shift-click items in your backpack to store them (or pick a stack up and click it onto the terminal's items; right-click drops in just one), or use the backpack's "move all" button to store everything. Tabs under the search box show one kind of item at a time; only kinds you have get a tab.
5. **Lock it**: hold E on the Core and set a code. Everyone else needs the code to open your bays and terminals. The Core's owner always gets in.

### Crafting from storage
Craft a **Crafting Terminal** (it needs a workbench and a Storage Terminal) and place it near your Core. It works exactly like a workbench, but recipes can use ingredients from the network. If an ingredient is in storage, the terminal pulls it into your backpack when you click Craft.

### Bigger drives
| Drive | Holds | Made at |
|---|---|---|
| 48-Stack Drive | 48 stacks | backpack |
| 192-Stack Drive | 192 stacks | workbench (3 empty 48-Stack Drives + forged iron) |
| 576-Stack Drive | 576 stacks | workbench (3 empty 192-Stack Drives + forged steel) |
| 1728-Stack Drive | 1,728 stacks | workbench (3 empty 576-Stack Drives + forged steel) |
| 5184-Stack Drive | 5,184 stacks | workbench (3 empty 1728-Stack Drives + forged steel) |

To move up a size: open a Terminal and **shift-click three drives** of the smaller size in your backpack to empty them into the network. Then craft them into one drive of the next size at a workbench. Only empty drives count as ingredients, and a drive that still holds items can't be scrapped either, so nothing gets thrown away by accident.

### Tougher bays, farther-reaching Cores
| Block | Holds / reaches | Toughness |
|---|---|---|
| Drive Bay → Reinforced → Armored Drive Bay | 4 → 8 → 12 drives | 1,000 → 5,000 → 10,000 |
| Scrap → Storage → Advanced Storage Core | 6 → 12 → 24 blocks | 1,000 → 5,000 → 10,000 |

Craft the next tier at a workbench. Then hold a hammer (or stone axe, or nail gun) with the new block in your inventory, and hit the old one until it upgrades. Drives stay in the bay, and the Core keeps its code and its list of allowed players. You can also place the higher tiers directly.

**Cores must be more than 48 blocks apart**, so two networks never overlap.

### Heat
The Core gives off heat that builds up in its area, the same way a forge or campfire does, and enough heat brings screamers. Bigger drives give off more heat (a 5184-stack drive counts as 8 of the 48-stack ones), and better Cores run cooler. A small early network alone never brings screamers. Heat is capped, so even a huge base only draws them now and then. Type `sn heat` in the console (F1) next to a Core to see the numbers.

---

## For developers

- Build: `dotnet build src/StashNet/StashNet.csproj -c Release` stages the mod into `dist/StashNet/`. Copy `local.props.example` to `local.props` and set your game path first.
  - `-p:Package=true` also writes the release zip `dist/StashNet-v<version>.zip`.
  - `-p:Deploy=true` also copies the mod into your game's `Mods` folder.
- Tests: `dotnet test` runs the game-free logic tests (`src/StashNet.Core`, `tests/StashNet.Core.Tests`).
- Design and roadmap: [docs/PLAN.md](docs/PLAN.md). In-game test checklists: [docs/testing](docs/testing).
