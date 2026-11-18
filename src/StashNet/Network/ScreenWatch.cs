using System;
using System.Collections.Generic;
using System.Linq;
using StashNet.Blocks;
using StashNet.Core.Heat;
using StashNet.Core.Network;
using StashNet.Core.Protocol;
using StashNet.Heat;

namespace StashNet.Network
{
    /// <summary>
    /// SERVER ONLY. Live Storage Terminal screens: which player watches which terminal (players' games send
    /// Watch / Unwatch through ScreenLink), and pushes each watched network's numbers to its watchers.
    ///  - Numbers come from the shared NetworkSummaryCache (no extra drive reads); a refreshed summary
    ///    (Recorded) marks its network dirty, pushed at most once a second per network (ScreenWatchBook).
    ///  - Every 2 s: each watched terminal's Core is looked up again, each watched Core's lock is compared,
    ///    and a stale summary is rebuilt (at most every 5 s per watched network).
    /// Never touches UI or rendering (runs on dedicated servers).
    /// </summary>
    internal static class ScreenWatch
    {
        private const float CheckSeconds = 2f;
        /// <summary>Sanity bound: no one sees a terminal screen from further away.</summary>
        private const int MaxWatchRange = 400;

        private static ScreenWatchBook book = new ScreenWatchBook();
        private static float nextCheck;

        private static bool IsServer => SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;

        public static void Init()
        {
            NetworkSummaryCache.Recorded += OnRecorded;
            ModEvents.GameUpdate.RegisterHandler(OnGameUpdate);
            ModEvents.PlayerDisconnected.RegisterHandler(OnPlayerDisconnected);
            ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShuttingDown);
        }

        /// <summary>SERVER: a player's Watch / Unwatch (from the package, or directly from ScreenLink on a host).</summary>
        public static void Handle(EntityPlayer player, ScreenMessage m)
        {
            World world = GameManager.Instance.World;
            if (!IsServer || world == null || player == null || m == null) return;
            Vector3i terminal = ScreenPos.ToVector(m.Terminal);
            switch (m.Op)
            {
                case ScreenOp.Watch:
                    if (!(world.GetBlock(terminal).Block is BlockStashNetTerminal)) return;
                    if (CubeDistance(World.worldToBlockPos(player.position), terminal) > MaxWatchRange) return;
                    book.Watch(player.entityId, m.Terminal);
                    Pos3? core = ResolveCore(world, terminal);
                    // A new Watch always gets the numbers again: the player's game may have dropped its copy
                    // (another TV of that network unloaded meanwhile), so what it was last sent doesn't count.
                    if (core.HasValue) book.ForgetSent(player.entityId, core.Value);
                    // Others watching this terminal only hear about it when its Core changed; this player always.
                    var tell = new HashSet<int>(book.SetCore(m.Terminal, core)) { player.entityId };
                    foreach (int id in tell) Tell(world, id, m.Terminal, core);
                    break;
                case ScreenOp.Unwatch:
                    book.Unwatch(player.entityId, m.Terminal);
                    break;
            }
        }

        private static void OnRecorded(Vector3i corePos)
        {
            try
            {
                book.MarkDirty(ScreenPos.ToPos3(corePos)); // no-op unless someone watches that network
            }
            catch (Exception e)
            {
                Log.Warning("[StashNet] Screen watch: " + e.Message);
            }
        }

        private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
        {
            if (book.IsEmpty || !IsServer) return; // nothing watched: no work, no allocation
            World world = GameManager.Instance.World;
            if (world == null) return;
            try
            {
                float now = UnityEngine.Time.time;
                if (book.HasDirty) // idle (nothing changed): no TakeDue, no allocation
                {
                    foreach (Pos3 core in book.TakeDue(now)) PushCore(world, core);
                }
                if (now < nextCheck) return;
                nextCheck = now + CheckSeconds;
                Check(world, now);
            }
            catch (Exception e)
            {
                Log.Warning("[StashNet] Screen watch update failed: " + e);
            }
        }

        private static void OnPlayerDisconnected(ref ModEvents.SPlayerDisconnectedData data)
        {
            if (data.ClientInfo != null) book.DropPlayer(data.ClientInfo.entityId);
        }

        private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
        {
            book = new ScreenWatchBook();
            nextCheck = 0f;
        }

        /// <summary>The every-2-s checks: Cores moved or gone, locks changed, stale summaries.</summary>
        private static void Check(World world, float now)
        {
            foreach (Pos3 t in book.Terminals)
            {
                Vector3i terminal = ScreenPos.ToVector(t);
                if (world.GetChunkFromWorldPos(terminal) == null) continue; // not loaded here: keep what we know
                Pos3? core = world.GetBlock(terminal).Block is BlockStashNetTerminal ? ResolveCore(world, terminal) : null;
                foreach (int id in book.SetCore(t, core)) Tell(world, id, t, core);
            }

            foreach (Pos3 core in book.Cores)
            {
                if (book.LockChanged(core, LockPrint(NetworkLock.GetCoreLock(world, ScreenPos.ToVector(core)))))
                    book.MarkDirty(core);
                // Rebuilt only when missing or marked stale (drives changed outside StashNet), never for age
                // alone, never while one of the network's bays is open (spec §2; the throttle isn't used up
                // then), and at most every 5 s per network. A real read; its Record marks the network dirty.
                Vector3i corePos = ScreenPos.ToVector(core);
                bool kept = NetworkSummaryCache.TryPeek(corePos, out _, out bool stale);
                if ((!kept || stale) && !NetworkSummaryCache.AnyBayOpen(world, corePos) && book.StaleRebuildAllowed(core, now))
                    NetworkSummaryCache.TryGet(world, corePos, out _, out _);
            }
        }

        private static Pos3? ResolveCore(World world, Vector3i terminal) =>
            NetworkSummaryCache.TryFindCore(world, terminal, out Vector3i core) ? ScreenPos.ToPos3(core) : (Pos3?)null;

        /// <summary>Assign to one player, then (with a Core) that network's numbers if they differ from what it last got.</summary>
        private static void Tell(World world, int entityId, Pos3 terminal, Pos3? core)
        {
            Send(world, entityId, new ScreenMessage
            {
                Op = ScreenOp.Assign,
                Terminal = terminal,
                HasCore = core.HasValue,
                Core = core ?? default,
            });
            if (core.HasValue) SendState(world, entityId, core.Value, CoreView(world, core.Value));
        }

        private static void PushCore(World world, Pos3 core)
        {
            IReadOnlyCollection<int> watchers = book.WatchersOf(core);
            if (watchers.Count == 0) return;
            ScreenView live = CoreView(world, core);
            foreach (int id in watchers) SendState(world, id, core, live);
        }

        private static void SendState(World world, int entityId, Pos3 core, ScreenView live)
        {
            ScreenView view = ViewFor(world, entityId, core, live);
            if (!book.ShouldSend(entityId, core, view)) return;
            Send(world, entityId, new ScreenMessage { Op = ScreenOp.State, HasCore = true, Core = core, View = view });
        }

        /// <summary>A locked network shows "locked out" to anyone not allowed in (the owner is allowed).</summary>
        private static ScreenView ViewFor(World world, int entityId, Pos3 core, ScreenView live)
        {
            if (!live.HasCore) return live;
            TEFeatureLockable coreLock = NetworkLock.GetCoreLock(world, ScreenPos.ToVector(core));
            if (!NetworkLock.IsLocked(coreLock)) return live;
            return NetworkLock.IsAllowed(coreLock, NetworkLock.PlayerId(world.GetEntity(entityId) as EntityPlayer))
                ? live
                : ScreenView.ShutOut();
        }

        /// <summary>The network's numbers as an allowed player sees them; NoSignal when no summary can be had.</summary>
        private static ScreenView CoreView(World world, Pos3 core)
        {
            Vector3i corePos = ScreenPos.ToVector(core);
            if (!NetworkSummaryCache.TryPeek(corePos, out NetworkSummary s, out _))
            {
                // Builds it once (a Core finds itself at distance 0). Another Core answering means this one is gone.
                if (!NetworkSummaryCache.TryGet(world, corePos, out Vector3i found, out s) || found != corePos || s == null)
                    return ScreenView.NoSignal;
            }
            Block coreBlock = world.GetBlock(corePos).Block;
            float cap = HeatEmitter.GetFloat(coreBlock, HeatEmitter.PropHeatCap);
            float heat = NetworkHeat.PerReport(s.HeatWeight, HeatEmitter.GetFloat(coreBlock, HeatEmitter.PropHeatPerDrive), cap);
            bool locked = NetworkLock.IsLocked(NetworkLock.GetCoreLock(world, corePos));
            return new ScreenView(true, false, locked, s.UsedStacks, s.MaxStacks, s.DriveCount, s.FullDrives,
                ScreenView.HeatSegmentsOf(heat, cap));
        }

        /// <summary>Everything about the lock that changes who may look: on/off, owner, allowed players.</summary>
        private static string LockPrint(TEFeatureLockable coreLock)
        {
            if (coreLock == null) return "none";
            string allowed = string.Join(",", coreLock.allowedUserIds.Where(u => u != null).Select(u => u.CombinedString).OrderBy(x => x, StringComparer.Ordinal));
            return $"{coreLock.IsLocked()}|{coreLock.GetOwner()?.CombinedString}|{allowed}";
        }

        /// <summary>To the local player (host) directly, otherwise as a package to that player's game.</summary>
        private static void Send(World world, int entityId, ScreenMessage m)
        {
            if (!GameManager.IsDedicatedServer && entityId == world.GetPrimaryPlayer()?.entityId)
            {
                ScreenLink.Receive(m);
                return;
            }
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendPackage(
                NetPackageManager.GetPackage<NetPackageStashNetScreen>().Setup(m),
                _onlyClientsAttachedToAnEntity: false,
                _attachedToEntityId: entityId);
        }

        private static int CubeDistance(Vector3i a, Vector3i b) =>
            Math.Max(Math.Abs(a.x - b.x), Math.Max(Math.Abs(a.y - b.y), Math.Abs(a.z - b.z)));
    }

    /// <summary>Block position conversions (game-free Pos3 in StashNet.Core, Vector3i here).</summary>
    internal static class ScreenPos
    {
        public static Pos3 ToPos3(Vector3i v) => new Pos3(v.x, v.y, v.z);
        public static Vector3i ToVector(Pos3 p) => new Vector3i(p.X, p.Y, p.Z);
    }
}
