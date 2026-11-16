using System;
using StashNet.Core.Protocol;
using UnityEngine.Scripting;

namespace StashNet.Network
{
    /// <summary>Client → server: a terminal request. Auto-registered by the game (NetPackage subclass).</summary>
    [Preserve]
    public class NetPackageStashNetRequest : NetPackage
    {
        private TerminalRequest request;

        public override NetPackageDirection PackageDirection => NetPackageDirection.ToServer;

        public NetPackageStashNetRequest Setup(TerminalRequest r)
        {
            request = r;
            return this;
        }

        public override void read(PooledBinaryReader _reader)
        {
            try
            {
                request = TerminalCodec.ReadRequest(_reader);
            }
            catch (Exception e) // FormatException, or EndOfStream from another StashNet version's format
            {
                request = null;
                Log.Warning("[StashNet] Ignored malformed terminal request: " + e.Message);
            }
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            TerminalCodec.Write(_writer, request);
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null || request == null || Sender == null) return;
            if (!(_world.GetEntity(Sender.entityId) is EntityPlayer player))
            {
                Log.Warning("[StashNet] Terminal request from a client with no player entity; ignored.");
                return;
            }
            TerminalResponse response;
            if (VersionCheck.ClientConfirmed(Sender))
            {
                response = TerminalServer.Handle(_world, player, request);
            }
            else
            {
                // Not confirmed as the same StashNet version (VersionCheck): storage is never touched. Items the
                // player's game already took out of its backpack are handed back, like any refused request
                // (an older StashNet in the seconds before it is removed). A matching game never sends early.
                Log.Warning($"[StashNet] Refused a request from {Sender}: StashNet version not confirmed; items handed back.");
                response = new TerminalResponse { RequestId = request.RequestId };
                TerminalServer.Refund(player, request, response);
                response.Messages.Add(new TerminalMessage("stashnetVersionBlocked", -1));
            }
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                NetPackageManager.GetPackage<NetPackageStashNetResponse>().Setup(response),
                _onlyClientsAttachedToAnEntity: false,
                _attachedToEntityId: Sender.entityId);
        }
    }
}
