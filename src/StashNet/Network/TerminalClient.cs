using System.Collections.Generic;
using StashNet.Core.Drives;
using StashNet.Core.Protocol;
using StashNet.Drives;
using StashNet.UI;

namespace StashNet.Network
{
    /// <summary>
    /// Client side of the terminal protocol. Sends requests (directly to TerminalServer when this
    /// machine is the server, otherwise as a NetPackage) and applies responses:
    ///  - Give: into the local backpack; anything that doesn't fit is sent back as a Return.
    ///  - ReturnToDrive: back onto the drive that was emptied for a DriveUnload (found by token);
    ///    if that drive can't be found, into the backpack like Give.
    /// Applying does not depend on the terminal window being open.
    /// </summary>
    public static class TerminalClient
    {
        private static int nextRequestId;
        private static readonly Dictionary<int, Vector3i> pendingPositions = new Dictionary<int, Vector3i>();
        private static readonly Dictionary<int, string> pendingUnloads = new Dictionary<int, string>();

        /// <summary>The id the next Send will use.</summary>
        public static int NextRequestId => nextRequestId + 1;

        public static void Send(TerminalRequest request) => Send(request, null);

        /// <summary>Sends a DriveUnload for a drive already emptied and tagged with <paramref name="token"/>.</summary>
        public static void SendDriveUnload(TerminalRequest request, string token) => Send(request, token);

        private static void Send(TerminalRequest request, string unloadToken)
        {
            request.RequestId = ++nextRequestId;
            pendingPositions[request.RequestId] = new Vector3i(request.X, request.Y, request.Z);
            if (unloadToken != null) pendingUnloads[request.RequestId] = unloadToken;

            ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm.IsServer)
            {
                World world = GameManager.Instance.World;
                EntityPlayerLocal player = world?.GetPrimaryPlayer();
                if (player == null) return;
                Apply(TerminalServer.Handle(world, player, request));
            }
            else
            {
                cm.SendToServer(NetPackageManager.GetPackage<NetPackageStashNetRequest>().Setup(request));
            }
        }

        public static void Apply(TerminalResponse response)
        {
            pendingPositions.TryGetValue(response.RequestId, out Vector3i pos);
            pendingPositions.Remove(response.RequestId);
            pendingUnloads.TryGetValue(response.RequestId, out string unloadToken);
            pendingUnloads.Remove(response.RequestId);

            EntityPlayerLocal player = GameManager.Instance.World?.GetPrimaryPlayer();
            var toBag = new List<ItemAmount>(response.Give);
            if (unloadToken != null)
            {
                if (!RestoreDrive(player, unloadToken, response.ReturnToDrive)) toBag.AddRange(response.ReturnToDrive);
            }
            else
            {
                toBag.AddRange(response.ReturnToDrive); // unexpected, but never lose items
            }

            var overflow = new List<ItemAmount>();
            if (player != null)
            {
                foreach (ItemAmount give in toBag)
                {
                    int moved = 0;
                    if (ItemCodec.TryDecode(give.Name, give.Variant, out ItemValue template, out _))
                    {
                        moved = BagTransfer.GiveToBag(player.bag, template, give.Count, give.Variant == null, out _);
                    }
                    if (moved < give.Count) overflow.Add(new ItemAmount(give.Name, give.Variant, give.Count - moved));
                }
            }
            if (overflow.Count > 0) response.Messages.Add(new TerminalMessage("stashnetBackpackFull", -1));

            XUiC_StashNetTerminal.Current?.OnResponse(response);

            // After updating the screen, so the Return's (newer) listing is what stays displayed.
            if (overflow.Count > 0)
            {
                var ret = new TerminalRequest { Op = TerminalOp.Return, X = pos.x, Y = pos.y, Z = pos.z };
                ret.Items.AddRange(overflow);
                Send(ret);
            }
        }

        /// <summary>Finds the drive tagged with <paramref name="token"/> (backpack, then toolbelt) and puts the leftovers back on it.</summary>
        private static bool RestoreDrive(EntityPlayerLocal player, string token, List<ItemAmount> leftovers)
        {
            if (player == null) return false;

            Bag bag = player.bag;
            for (int i = 0; i < bag.SlotCount; i++)
            {
                ItemStack stack = bag.ItemGrid[i];
                if (!HasToken(stack, token)) continue;
                bag.SetSlot(i, Refill(stack, leftovers));
                return true;
            }

            Inventory toolbelt = player.inventory;
            for (int i = 0; i < toolbelt.SlotCount; i++)
            {
                ItemStack stack = toolbelt.GetItem(i);
                if (!HasToken(stack, token)) continue;
                toolbelt.SetItem(i, Refill(stack, leftovers));
                return true;
            }

            Log.Warning("[StashNet] Unloaded drive not found in backpack/toolbelt; leftovers go to the backpack instead.");
            return false;
        }

        private static bool HasToken(ItemStack stack, string token) =>
            stack != null && !stack.IsEmpty() && stack.itemValue.TryGetMetadata(DriveItem.PendingKey, out string t) && t == token;

        private static ItemStack Refill(ItemStack stack, List<ItemAmount> leftovers)
        {
            ItemValue drive = stack.itemValue.Clone();
            var contents = new DriveContents();
            foreach (ItemAmount a in leftovers) contents.AddLoaded(a.Name, a.Variant, a.Count);
            DriveItem.Save(drive, contents);
            drive.RemoveMetaData(DriveItem.PendingKey);
            return new ItemStack(drive, stack.count);
        }
    }
}
