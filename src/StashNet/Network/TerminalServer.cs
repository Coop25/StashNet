using System;
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
                if (req.Op == TerminalOp.Deposit) resp.Give.AddRange(req.Items);
                if (req.Op == TerminalOp.Return) DropAll(player, req.Items);
                resp.Messages.Add(new TerminalMessage("stashnetTooFar", -1));
                return resp;
            }

            if (!StashNetNetwork.TryOpen(world, pos, out StashNetNetwork net, out string problemKey))
            {
                if (req.Op == TerminalOp.Deposit) resp.Give.AddRange(req.Items);
                if (req.Op == TerminalOp.Return) DropAll(player, req.Items);
                resp.Messages.Add(new TerminalMessage(problemKey, -1));
                return resp;
            }

            // The network's code lives on the Core (Milestone 4). Not allowed = no listing, no changes.
            if (!NetworkLock.IsAllowed(net.CoreLock, NetworkLock.PlayerId(player)))
            {
                if (req.Op == TerminalOp.Deposit) resp.Give.AddRange(req.Items);
                if (req.Op == TerminalOp.Return) DropAll(player, req.Items);
                resp.Messages.Add(new TerminalMessage("stashnetNetworkLocked", -1));
                return resp;
            }

            switch (req.Op)
            {
                case TerminalOp.Withdraw:
                    Withdraw(net, req, resp);
                    break;
                case TerminalOp.Deposit:
                    Deposit(net, req, resp);
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
            if (req.Items.Count != 1) return;
            ItemAmount want = req.Items[0];
            int removed = net.Storage.Extract(want.Name, want.Variant, want.Count);
            if (removed > 0) resp.Give.Add(new ItemAmount(want.Name, want.Variant, removed));
        }

        private static void Deposit(StashNetNetwork net, TerminalRequest req, TerminalResponse resp)
        {
            bool full = false, refused = false;
            foreach (ItemAmount a in req.Items)
            {
                int accepted = Storable(a) ? net.Storage.Insert(a.Name, a.Variant, a.Count) : 0;
                if (!Storable(a)) refused = true;
                else if (accepted < a.Count) full = true;
                if (accepted < a.Count) resp.Give.Add(new ItemAmount(a.Name, a.Variant, a.Count - accepted));
            }
            if (refused) resp.Messages.Add(new TerminalMessage("stashnetCantStore", -1));
            if (full) resp.Messages.Add(new TerminalMessage("stashnetNetworkFull", -1));
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
            if (net.BayCount == 0) resp.Messages.Add(new TerminalMessage("stashnetNoBays", -1));
            else if (net.Storage.Drives.Count == 0) resp.Messages.Add(new TerminalMessage("stashnetNoDrives", -1));
            if (net.SkippedBays > 0) resp.Messages.Add(new TerminalMessage("stashnetSkippedBays", net.SkippedBays));
            if (net.SkippedDrives > 0) resp.Messages.Add(new TerminalMessage("stashnetSkippedDrives", net.SkippedDrives));
            if (net.LockedDrives > 0) resp.Messages.Add(new TerminalMessage("stashnetLockedDrives", net.LockedDrives));
        }

        private static bool IsValid(World world, EntityPlayer player, Vector3i pos)
        {
            if (world == null || player == null) return false;
            if (!(world.GetBlock(pos).Block is BlockStashNetTerminal)) return false;
            var center = new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f);
            return (player.position - center).magnitude <= MaxReach;
        }

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
