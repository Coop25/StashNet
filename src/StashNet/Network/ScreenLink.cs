using System.Collections.Generic;
using StashNet.Core.Protocol;
using StashNet.Skins;

namespace StashNet.Network
{
    /// <summary>
    /// PLAYER'S GAME: tells the server which Storage Terminals this player can see (Watch / Unwatch) and
    /// hands the server's answers (Assign / State) to the screens. A watch is never dropped: it waits in
    /// a pending set until the server's StashNet version is confirmed (VersionCheck.ClientMayUse) and, on a
    /// host, until the local player exists. On a host or single-player, ScreenWatch is called directly.
    /// </summary>
    public static class ScreenLink
    {
        /// <summary>Terminals the screens want numbers for (sent or not).</summary>
        private static readonly HashSet<Vector3i> wanted = new HashSet<Vector3i>();
        /// <summary>Wanted but not sent to the server yet.</summary>
        private static readonly HashSet<Vector3i> pending = new HashSet<Vector3i>();

        public static void Init()
        {
            ModEvents.GameUpdate.RegisterHandler(OnGameUpdate);
            ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShuttingDown);
        }

        public static void Watch(Vector3i terminal)
        {
            if (GameManager.IsDedicatedServer) return;
            if (wanted.Add(terminal)) pending.Add(terminal);
        }

        public static void Unwatch(Vector3i terminal)
        {
            if (GameManager.IsDedicatedServer || !wanted.Remove(terminal)) return;
            if (pending.Remove(terminal)) return; // never sent: nothing to take back
            Send(new ScreenMessage { Op = ScreenOp.Unwatch, Terminal = ScreenPos.ToPos3(terminal) });
        }

        /// <summary>A server answer (package, or directly from ScreenWatch on a host).</summary>
        public static void Receive(ScreenMessage m)
        {
            if (GameManager.IsDedicatedServer || m == null) return;
            switch (m.Op)
            {
                case ScreenOp.Assign:
                    Vector3i terminal = ScreenPos.ToVector(m.Terminal);
                    if (!wanted.Contains(terminal)) return; // a late answer for a terminal no longer watched
                    TerminalScreens.OnAssign(terminal, m.HasCore, ScreenPos.ToVector(m.Core));
                    break;
                case ScreenOp.State:
                    if (m.View != null) TerminalScreens.OnState(ScreenPos.ToVector(m.Core), m.View);
                    break;
            }
        }

        private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
        {
            if (pending.Count == 0 || GameManager.IsDedicatedServer) return;
            if (!VersionCheck.ClientMayUse(out _)) return;
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && GameManager.Instance.World?.GetPrimaryPlayer() == null) return;
            var toSend = new List<Vector3i>(pending);
            pending.Clear();
            foreach (Vector3i terminal in toSend)
                Send(new ScreenMessage { Op = ScreenOp.Watch, Terminal = ScreenPos.ToPos3(terminal) });
        }

        private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
        {
            wanted.Clear();
            pending.Clear();
            if (!GameManager.IsDedicatedServer) TerminalScreens.Clear();
        }

        private static void Send(ScreenMessage m)
        {
            ConnectionManager cm = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (cm.IsServer)
            {
                EntityPlayerLocal player = GameManager.Instance.World?.GetPrimaryPlayer();
                if (player != null) ScreenWatch.Handle(player, m);
            }
            else
            {
                cm.SendToServer(NetPackageManager.GetPackage<NetPackageStashNetScreen>().Setup(m));
            }
        }
    }
}
