using System;
using System.Collections.Generic;
using System.Linq;
using StashNet.Blocks;
using StashNet.Core.Network;
using StashNet.Core.Protocol;
using StashNet.Drives;
using UnityEngine;

namespace StashNet.Network
{
    /// <summary>
    /// Runs terminal requests. SERVER ONLY (also the host / single-player). The only code that
    /// changes drive data. Must never touch XUi or EntityPlayerLocal (dedicated servers have no UI).
    ///
    /// Note on trust: the client's backpack is client-authoritative in 7DTD, so Deposit/Return trust
    /// the client's claim about what it removed. This protects against accidental duplication
    /// (races, full backpacks), not against modified clients - same as vanilla.
    /// </summary>
    public static class TerminalServer
    {
        public const float MaxReach = 8f;

        public static TerminalResponse Handle(World world, EntityPlayer player, TerminalRequest req)
        {
            var resp = new TerminalResponse { RequestId = req.RequestId };
            var pos = new Vector3i(req.X, req.Y, req.Z);

            if (!IsValid(world, player, pos))
            {
                // Never strand items the client already handed over.
                Refund(player, req, resp);
                resp.Messages.Add(new TerminalMessage("stashnetTooFar", -1));
                return resp;
            }

            if (!StashNetNetwork.TryOpen(world, pos, out StashNetNetwork net, out string problemKey))
            {
                Refund(player, req, resp);
                resp.Messages.Add(new TerminalMessage(problemKey, -1));
                return resp;
            }

            // The network's code lives on the Core (Milestone 4). Not allowed = no listing, no changes.
            if (!NetworkLock.IsAllowed(net.CoreLock, NetworkLock.PlayerId(player)))
            {
                Refund(player, req, resp);
                resp.Messages.Add(new TerminalMessage("stashnetNetworkLocked", -1));
                return resp;
            }

            // Milestone 7: using a Storage Terminal makes a little heat (throttled per terminal).
            // Guarded: a heat failure must never abort a request whose items the client already handed over.
            try
            {
                Heat.HeatEmitter.TerminalUsed(world, pos);
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] heat: terminal bump failed: {e.Message}");
            }

            switch (req.Op)
            {
                case TerminalOp.Withdraw:
                    Withdraw(net, req, resp);
                    break;
                case TerminalOp.Deposit:
                    Deposit(net, req, resp.Give, resp);
                    break;
                case TerminalOp.DriveUnload:
                    Deposit(net, req, resp.ReturnToDrive, resp);
                    // Leftovers stay on the drive, not in the backpack: use the matching message.
                    resp.Messages = resp.Messages
                        .Select(m => m.Key == "stashnetNetworkFull" ? new TerminalMessage("stashnetDriveLeftovers", -1) : m)
                        .ToList();
                    int unloaded = 0;
                    foreach (ItemAmount a in req.Items) unloaded += a.Count;
                    foreach (ItemAmount a in resp.ReturnToDrive) unloaded -= a.Count;
                    resp.Messages.Add(new TerminalMessage("stashnetDriveUnloaded", unloaded));
                    break;
                case TerminalOp.Return:
                    Return(net, player, req);
                    break;
            }

            net.SaveDirty();
            FillListing(net, resp);
            return resp;
        }

        private static void Withdraw(StashNetNetwork net, TerminalRequest req, TerminalResponse resp)
        {
            // Several items when a terminal cell combines look-alikes (engines differing only by
            // seed): each share is extracted on its own, and only what was removed is given.
            foreach (ItemAmount want in req.Items)
            {
                if (want.Count <= 0) continue;
                int removed = net.Storage.Extract(want.Name, want.Variant, want.Count);
                if (removed > 0) resp.Give.Add(new ItemAmount(want.Name, want.Variant, removed));
            }
        }

        /// <summary>Store what fits; anything else goes to <paramref name="leftovers"/> (backpack, or the drive for DriveUnload).</summary>
        private static void Deposit(StashNetNetwork net, TerminalRequest req, List<ItemAmount> leftovers, TerminalResponse resp)
        {
            bool full = false, refused = false;
            foreach (ItemAmount a in req.Items)
            {
                int accepted = Storable(a) ? net.Storage.Insert(a.Name, a.Variant, a.Count) : 0;
                if (!Storable(a)) refused = true;
                else if (accepted < a.Count) full = true;
                if (accepted < a.Count) leftovers.Add(new ItemAmount(a.Name, a.Variant, a.Count - accepted));
            }
            if (refused) resp.Messages.Add(new TerminalMessage("stashnetCantStore", -1));
            if (full) resp.Messages.Add(new TerminalMessage("stashnetNetworkFull", -1));
        }

        /// <summary>Request refused before anything was stored: hand back whatever the client gave up.</summary>
        private static void Refund(EntityPlayer player, TerminalRequest req, TerminalResponse resp)
        {
            if (req.Op == TerminalOp.Deposit) resp.Give.AddRange(req.Items);
            if (req.Op == TerminalOp.DriveUnload) resp.ReturnToDrive.AddRange(req.Items);
            if (req.Op == TerminalOp.Return) DropAll(player, req.Items);
        }

        /// <summary>Overflow from a client: store what fits, drop the rest. Never sent back.</summary>
        private static void Return(StashNetNetwork net, EntityPlayer player, TerminalRequest req)
        {
            foreach (ItemAmount a in req.Items)
            {
                int accepted = Storable(a) ? net.Storage.Insert(a.Name, a.Variant, a.Count) : 0;
                if (accepted < a.Count) Drop(player, new ItemAmount(a.Name, a.Variant, a.Count - accepted));
            }
        }

        private static void FillListing(StashNetNetwork net, TerminalResponse resp)
        {
            resp.HasListing = true;
            foreach (NetworkItem item in net.Storage.Aggregate()) resp.Listing.Add(new ItemAmount(item.Name, item.Variant, item.Count));
            resp.DistinctItems = resp.Listing.Count;
            resp.UsedStacks = net.Storage.UsedStacks;
            resp.MaxStacks = net.Storage.MaxStacks;
            resp.Drives = net.Storage.Drives.Count;
            resp.Messages.AddRange(NetworkStatus.Messages(net.BayCount, net.OtherCoreBays, net.Storage.Drives.Count, net.SkippedBays, net.SkippedDrives));
        }

        private static bool IsValid(World world, EntityPlayer player, Vector3i pos)
        {
            if (world == null || player == null) return false;
            if (!IsTerminalBlock(world.GetBlock(pos).Block)) return false;
            var center = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
            return (player.position - center).magnitude <= MaxReach;
        }

        /// <summary>Storage Terminal, or Crafting Terminal (a workbench block marked StashNetCraftingTerminal).</summary>
        public static bool IsTerminalBlock(Block block) =>
            block is BlockStashNetTerminal || (block != null && block.Properties.Contains("StashNetCraftingTerminal"));

        /// <summary>Drives can't go in drives, and the item must exist in this game.</summary>
        private static bool Storable(ItemAmount a)
        {
            if (!ItemCodec.TryDecode(a.Name, a.Variant, out ItemValue iv, out _)) return false;
            return !DriveRules.IsDrive(iv) && ItemCodec.CanStore(iv);
        }

        private static void DropAll(EntityPlayer player, System.Collections.Generic.List<ItemAmount> items)
        {
            foreach (ItemAmount a in items) Drop(player, a);
        }

        private static void Drop(EntityPlayer player, ItemAmount a)
        {
            if (player == null || !ItemCodec.TryDecode(a.Name, a.Variant, out ItemValue template, out _))
            {
                Log.Warning($"[StashNet] Could not drop {a} (item or player missing).");
                return;
            }
            int maxStack = Math.Max(1, template.ItemClass.Stacknumber.Value);
            for (int left = a.Count; left > 0; left -= maxStack)
            {
                var stack = new ItemStack(template.Clone(), Math.Min(left, maxStack));
                GameManager.Instance.ItemDropServer(stack, player.position + Vector3.up, Vector3.zero, player.entityId, 60f);
            }
            Log.Out($"[StashNet] Dropped {a} at {player.EntityName}'s feet (storage and backpack full).");
        }
    }
}
