using UnityEngine.Scripting;

namespace StashNet.Network
{
    /// <summary>
    /// Both ways: "I run StashNet &lt;version&gt;". The server sends it to each joining player; the
    /// player's game answers with its own. See VersionCheck. Auto-registered (NetPackage subclass).
    /// Keep this package's format unchanged forever, so any two versions can still compare.
    /// </summary>
    [Preserve]
    public class NetPackageStashNetHello : NetPackage
    {
        private string version = "";

        public override NetPackageDirection PackageDirection => NetPackageDirection.Both;

        public NetPackageStashNetHello Setup(string v)
        {
            version = v ?? "";
            return this;
        }

        public override void read(PooledBinaryReader _reader)
        {
            version = _reader.ReadString();
        }

        public override void write(PooledBinaryWriter _writer)
        {
            base.write(_writer);
            _writer.Write(version);
        }

        public override void ProcessPackage(World _world, GameManager _callbacks)
        {
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                if (Sender != null) VersionCheck.OnClientHello(Sender, version);
            }
            else
            {
                VersionCheck.OnServerHello(version);
            }
        }
    }
}
