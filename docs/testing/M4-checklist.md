# Milestone 4: in-game test checklist (password)

**Goal:** one code on the Core protects the network's terminals and bays, using the game's own keypad. Drives are not locked.

**About 15 minutes**, single-player in your throwaway world. Being locked out needs a second real player, so that part waits for the multiplayer check.

## 0. Before you start
- [ ] F1: `[StashNet] Loaded v0.5.1 (6 methods patched)`, and no red errors mentioning StashNet.
- [ ] **Replace your old Scrap Cores.** Cores placed before this version have no lock. Destroy or pick up each old Core and place a fresh one. (If an old Core throws errors when you look at it, screenshot them and then destroy it.)

## 1. The Core has a lock
- [ ] Place a new Core. Look at it and **hold E**. The radial has the vanilla lock options (**Lock**, **Set code**/**Enter code**, **Take**).
- [ ] Set a code (e.g. `1234`) and lock it.
- [ ] Hold E → **Take** picks the Core up after a short delay. (Place it back and lock it again.)

## 2. You (the owner) still have access
- [ ] With the Core locked, open the network's **Terminal**. It works normally.
- [ ] Open a **Drive Bay**. It works normally.

## 3. Drives are NOT locked (moved freely between networks)
- [ ] Build a **second network** some distance away (Core + bay + terminal) and lock its Core with a **different** code.
- [ ] Take a drive **with items on it** out of network 1 and put it in network 2's bay.
- [ ] Network 2's terminal **shows that drive's items** normally (drives aren't tied to a network).
- [ ] Move it back to network 1. The items show there too, untouched.

## 4. Changing the code
- [ ] On network 1's Core, change the code. You (the owner) still have access to network 1's terminal and bays.

## 5. Bays and terminals have no lock of their own
- [ ] Hold E on a Drive Bay. There's **no** "Lock" or "Set code" option on the bay itself anymore.
- [ ] (If you locked a bay back in Milestone 1, its **Unlock** option should still be there to clear it.)

## 6. Old features still fine
- [ ] Deposit, withdraw, search, and sort in the terminal all still work.

## Can't test alone (needs a second real player; part of the later multiplayer check)
- Another player sees "Network locked", the Terminal opens the keypad for them, the right code lets them in, and a code change kicks them out.

## Report back
PASS/FAIL per section, plus screenshots of anything odd (especially the Core's radial menu and the prompts when looking at a bay/terminal).
