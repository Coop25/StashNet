using System;
using StashNet.Core.Protocol;
using UnityEngine.Scripting;

namespace StashNet.Network
{
    /// <summary>
    /// Both ways, for live Storage Terminal screens: Watch / Unwatch (player's game to server), Assign /
    /// State (server to player's game). Auto-registered by the game (NetPackage subclass).
    /// </summary>
    [Preserve]
    public class NetPackageStashNetScreen : NetPackage
    {
        private ScreenMessage message;

        public override NetPackageDirection PackageDirection => NetPackageDirection.Both;

        public NetPackageStashNetScreen Setup(ScreenMessage m)
        {
            message = m;
            return this;
        }

        public override void read(PooledBinaryReader _reader)
        {
            try
            {
                message = ScreenCodec.Read(_reader);
            }
            catch (Exception e) // FormatException, or EndOfStream from another StashNet version's format
            {
                message = null;
                Log.Warning("[StashNet] Ignored malformed screen message: " + e.Message);
            }
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            ScreenCodec.Write(_writer, message);
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (_world == null || message == null) return;
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                // Only from a joined player confirmed as the same StashNet version (VersionCheck); a matching
                // game never sends before that, so anything else is silently ignored.
                if (Sender == null || !VersionCheck.ClientConfirmed(Sender)) return;
                if (!(_world.GetEntity(Sender.entityId) is EntityPlayer player)) return;
                ScreenWatch.Handle(player, message);
            }
            else
            {
                ScreenLink.Receive(message);
            }
        }
    }
}
