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
            catch (FormatException e)
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
            TerminalResponse response = TerminalServer.Handle(_world, player, request);
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                NetPackageManager.GetPackage<NetPackageStashNetResponse>().Setup(response),
                _onlyClientsAttachedToAnEntity: false,
                _attachedToEntityId: Sender.entityId);
        }
    }
}
