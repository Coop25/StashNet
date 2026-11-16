# StashNet

A storage network mod for **7 Days to Die**. Cobble together a crude storage network in your first week, then grow it into a big, armored, late-game system.

- **Physical drives** hold your items. Pull a drive out and its contents go with it.
- **Storage Terminal**: every drive in one window, with search, sort, deposit and withdraw.
- **Network Workbench**: a real workbench that can use ingredients stored on the network.
- **Station automation**: Network stations can keep items in stock, keep the forge's metals melted, refill their own fuel from storage, put their fire out when idle, and keep finished items in the station when you want them.
- **One code** set on the Core protects the network's Drive Bays and Terminals.
- **Tiers**: bigger drives, tougher bays that hold more drives, and Cores that reach farther. You upgrade bays and Cores in place with a hammer.
- **Heat**: a big hoard runs hot and draws screamers, just like a forge does.

**Version 1.0.0.** Tested on 7 Days to Die **V3.4 (b10)**, in single-player and on a multiplayer server.

**[⬇ Download StashNet 1.0.0](https://github.com/Coop25/StashNet/releases/latest)** (the `StashNet-v1.0.0.zip` file under *Assets*).

---

## Requirements

- 7 Days to Die **V3.4**. Other versions may not work.
- **EasyAntiCheat must be off**, because the mod includes a DLL.
- **Multiplayer:** the server **and every player** need the **exact same version** of StashNet. The server checks this when you join: a player with a different (or older) version is removed with a message naming both versions, before StashNet can touch any items.

## Install

1. Download `StashNet-v1.0.0.zip` from the [latest release](https://github.com/Coop25/StashNet/releases/latest) (or get it from whoever runs your server). Everyone on a server, the server included, needs the same version.
2. Extract it into your game's `Mods` folder, so you end up with `7 Days To Die\Mods\StashNet\ModInfo.xml`. (Steam: right-click the game → Manage → Browse local files.)
3. Start the game **with EasyAntiCheat turned off** (in the game's launcher, switch off EasyAntiCheat before pressing Play).
4. In the console (F1) you should see `[StashNet] Loaded v1.0.0`.

**Dedicated server:** put the same `StashNet` folder in the server's `Mods` folder, and set `<property name="EACEnabled" value="false"/>` in its `serverconfig.xml`.

## Updating

1. Close the game (and stop the server).
2. **Delete** the old `Mods\StashNet` folder, then extract the new zip in its place. (Extracting over the old folder can leave old files behind.)
3. Do the same on the server and for every player: everyone must be on the same version, or the server removes the odd one out when they join.

Your worlds, drives and everything on them carry over, including from the earlier test releases (0.17 and later).

## ⚠️ Removing the mod

Everything stored on drives lives inside StashNet's items. If you remove the mod from a world that has StashNet blocks or drives, **those blocks, drives and everything on them are lost**. Before uninstalling, withdraw what you want to keep and pick up or break the StashNet blocks.

---

## Getting started

1. **Craft** (from your backpack): a **Scrap Storage Core**, a **Drive Bay**, a few **48-Stack Drives**, and a **Storage Terminal**.
2. **Place the Core**, then place the Drive Bay and the Terminal **within 6 blocks** of it.
3. **Put drives in the Drive Bay.** A bay only accepts drives.
4. **Use the Terminal**: click an item to take one (shift-click for a full stack), shift-click items in your backpack to store them (or pick a stack up and click it onto the terminal's items; right-click drops in just one), or use the backpack's "move all" button to store everything. Tabs under the search box show one kind of item at a time; only kinds you have get a tab. **Hold Shift** to freeze the grid while you shift-click stacks out: nothing slides around until you let go.
5. **Lock it**: hold E on the Core and set a code. Everyone else needs the code to open your bays and terminals. The Core's owner always gets in.

### Crafting from storage
Craft a **Network Workbench** (it needs a workbench and a Storage Terminal) and place it near your Core. It works exactly like a workbench, but recipes can use ingredients from the network. If an ingredient is in storage, the workbench pulls it into your backpack when you click Craft.

### Station automation
Every Network station (Workbench, Forge, Chemistry Station, Campfire, Cement Mixer) has an **Automation** tab (the lightning icon) in the recipe box. It opens by itself when no recipe is selected:

- **Keep in stock:** select a recipe, set an amount and click Keep. While someone is nearby, the station crafts from stored ingredients whenever storage holds less than that. Up to 8 items per station. Automatic crafts give no crafting XP.
- **Keep melted** (Forge): pick a metal and an amount. The forge pulls raw scrap from storage (scrap iron, brass and lead; broken glass, then crushed sand; small stones; clay) until that much is melted, then stops. Nothing else is ever melted. With one metal it uses both ore slots; with several, each metal gets one slot, and they take turns when there are more metals than slots.
- **Fire** (Forge, Campfire, Chemistry Station): *Myself* is the normal game. *Auto* lights the fire when there's something to craft or smelt and puts it out when idle. **Refill** picks a fuel from storage (shown with its icon and count) to keep 100 of it in the fuel slots.
- **Finished items:** *To storage* (the default) or *Stay here*, which leaves them in Output and refunds cancelled crafts to your backpack, like a normal station.

Crafting at a Network station can also use stored parts such as vehicle engines and batteries, as long as nothing is attached to them. Batteries and other items with a quality level are taken lowest quality first.

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

### Keeping your items safe
- StashNet never deletes items to make room. Anything that fits nowhere (storage full and your backpack full) goes into a **loot bag** at your feet, never a loose item that despawns.
- While someone has a Drive Bay open, the network leaves that bay's drives alone until it's closed again.
- When two players take the same item at the same moment, whoever reaches the server first gets it. Nothing is ever duplicated.
- A Drive Bay only holds drives. Anything else that ends up in one is moved into a loot bag beside it.

---

## For developers

- Build: `dotnet build src/StashNet/StashNet.csproj -c Release` stages the mod into `dist/StashNet/`. Copy `local.props.example` to `local.props` and set your game path first.
  - `-p:Package=true` also writes the release zip `dist/StashNet-v<version>.zip`.
  - `-p:Deploy=true` also copies the mod into your game's `Mods` folder.
- Tests: `dotnet test` runs the game-free logic tests (`src/StashNet.Core`, `tests/StashNet.Core.Tests`).
- Design and roadmap: [docs/PLAN.md](docs/PLAN.md). In-game test checklists: [docs/testing](docs/testing).
