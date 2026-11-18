# Drag items out of the Storage Terminal: design (short)

Date: 2026-11-18. Approved by the owner in conversation ("Let's do it").

## Intent
The owner keeps trying to drag tools from the Storage Terminal onto the hotbar, and whole stacks out of the network,
the way the backpack works. Today every take goes straight into the backpack.

## What it does
- Press on a terminal cell and move the mouse (vanilla's own pick-up distance, `XUiC_ItemStack.PickupSnapDistance`)
  with the left button held: the terminal takes **one stack** onto the cursor. Tools/weapons (stack size 1) = one
  item; resources = a full stack, or everything stored if less (`TakeAmount.Stack`, same as shift-click).
- The item is then on the cursor exactly like a vanilla pick-up: click a toolbelt/backpack slot to place it, click
  the terminal grid to put it back (existing `TryDepositHeld`), close the window and vanilla puts it in the backpack
  (`XUiC_DragAndDropWindow.PlaceItemBackInInventory`). Vanilla slots also place on click, not on release, so this
  matches the backpack.
- Plain click / second click / shift-click / right-click are unchanged. The press that ends a drag doesn't also
  select or take.
- If something is already on the cursor, a press stores it as today; no drag pick-up.

## How
Client only. The drag sends the ordinary Withdraw request; the client remembers that request id as "to cursor".
When the reply arrives (at once on a host, after the round trip on a server), the first returned stack goes on the
cursor if the cursor is still empty, and anything else (or everything, if the cursor got filled meanwhile) goes to
the backpack as today. No protocol, server or save change.

## Approaches considered
- **Drag picks up onto the cursor (chosen):** matches the backpack, no server change.
- Drop straight into the slot under the mouse on release: vanilla doesn't do this, and on a server the item arrives
  after the release, so the target slot would be guesswork.
- A "take to hotbar" button: not what the owner tries to do; more UI.

## Left to the builder
Where exactly the mouse-state check lives (the cell's `Update`, mirroring vanilla's `XUiC_ItemStack.Update`), how
the "ends a drag" press is suppressed, and how the cursor stack is built from the reply (`ItemCodec`).
