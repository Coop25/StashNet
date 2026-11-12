using System;
using StashNet.Core.Protocol;
using UnityEngine.Scripting;

namespace StashNet.Network
{
    /// <summary>Server → one client: the result of a terminal request.</summary>
    [Preserve]
    public class NetPackageStashNetResponse : NetPackage
    {
        private TerminalResponse response;

        public override NetPackageDirection PackageDirection => NetPackageDirection.ToClient;

        public NetPackageStashNetResponse Setup(TerminalResponse r)
        {
            response = r;
            return this;
        }

        public override void read(PooledBinaryReader _reader)
        {
            try
            {
                response = TerminalCodec.ReadResponse(_reader);
            }
            catch (FormatException e)
            {
                response = null;
                Log.Warning("[StashNet] Ignored malformed terminal response: " + e.Message);
            }
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            TerminalCodec.Write(_writer, response);
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (response != null) TerminalClient.Apply(response);
        }
    }
}
