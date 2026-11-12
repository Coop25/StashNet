using System.Collections.Generic;
using StashNet.Core.Protocol;
using StashNet.Drives;
using StashNet.UI;

namespace StashNet.Network
{
    /// <summary>
    /// Client side of the terminal protocol. Sends requests (directly to TerminalServer when this
    /// machine is the server, otherwise as a NetPackage) and applies responses: items in Give go
    /// into the local backpack, and anything that doesn't fit is sent back as a Return.
    /// Applying does not depend on the terminal window being open.
    /// </summary>
    public static class TerminalClient
    {
        private static int nextRequestId;
        private static readonly Dictionary<int, Vector3i> pendingPositions = new Dictionary<int, Vector3i>();

        /// <summary>The id the next Send will use.</summary>
        public static int NextRequestId => nextRequestId + 1;

        public static void Send(TerminalRequest request)
        {
            request.RequestId = ++nextRequestId;
            pendingPositions[request.RequestId] = new Vector3i(request.X, request.Y, request.Z);

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

            var overflow = new List<ItemAmount>();
            EntityPlayerLocal player = GameManager.Instance.World?.GetPrimaryPlayer();
            if (player != null)
            {
                foreach (ItemAmount give in response.Give)
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
    }
}
