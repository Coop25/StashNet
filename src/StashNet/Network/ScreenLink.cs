using System;
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
        /// <summary>Pending watches whose last send threw: the server may have them, so an Unwatch is still sent.</summary>
        private static readonly HashSet<Vector3i> failed = new HashSet<Vector3i>();
        /// <summary>After a failed send, pending watches wait this long before the next try (no retry every frame).</summary>
        private const float RetrySeconds = 3f;
        private static float retryAt;

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
            if (pending.Remove(terminal) && !failed.Remove(terminal)) return; // never sent: nothing to take back
            try
            {
                Send(new ScreenMessage { Op = ScreenOp.Unwatch, Terminal = ScreenPos.ToPos3(terminal) });
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] Screen unwatch for the terminal at {terminal} failed: {e}");
            }
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
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now < retryAt) return;
            // A watch that throws goes back to pending (retried after RetrySeconds) and doesn't stop the others.
            // It leaves pending just before its send, so an Unwatch arriving during the send (host: the answer
            // runs synchronously) counts it as sent and tells the server.
            foreach (Vector3i terminal in new List<Vector3i>(pending))
            {
                if (!pending.Remove(terminal)) continue; // unwatched meanwhile
                failed.Remove(terminal);
                try
                {
                    Send(new ScreenMessage { Op = ScreenOp.Watch, Terminal = ScreenPos.ToPos3(terminal) });
                }
                catch (Exception e)
                {
                    if (wanted.Contains(terminal))
                    {
                        pending.Add(terminal);
                        failed.Add(terminal);
                    }
                    retryAt = now + RetrySeconds;
                    Log.Warning($"[StashNet] Screen watch for the terminal at {terminal} failed (retrying in {RetrySeconds} s): {e}");
                }
            }
        }

        private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
        {
            wanted.Clear();
            pending.Clear();
            failed.Clear();
            retryAt = 0f;
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
