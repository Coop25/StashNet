using System.Collections.Generic;
using StashNet.Core.Protocol;

namespace StashNet.Network
{
    /// <summary>
    /// Exact-version handshake (owner, 2026-11-16). Rules in StashNet.Core/Protocol/VersionGate.
    ///
    /// SERVER: when a joined player spawns, it sends its version (NetPackageStashNetHello). The player's
    /// game answers with its own. A different version, or no answer within VersionGate.HelloTimeoutSeconds
    /// (an older StashNet has no handshake), removes the player with a message naming both versions.
    /// Until a matching answer arrives, that player's StashNet requests are ignored.
    ///
    /// PLAYER'S GAME: sends nothing to a server whose version it hasn't confirmed (ClientMayUse), and the
    /// windows that would take items out of the backpack don't open. So no deposit can be stranded by a
    /// refused request. Host and single-player: always allowed (the player is the server).
    /// </summary>
    public static class VersionCheck
    {
        private static bool IsServer => SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;

        // ---- server ----
        private static readonly HashSet<ClientInfo> confirmed = new HashSet<ClientInfo>();
        private static readonly Dictionary<ClientInfo, float> waiting = new Dictionary<ClientInfo, float>();
        private static float nextTimeoutCheck;

        // ---- player's game ----
        /// <summary>The server's StashNet version, null until it says (reset when leaving a game).</summary>
        public static string ServerVersion { get; private set; }

        public static void Init()
        {
            ModEvents.PlayerSpawnedInWorld.RegisterHandler(OnPlayerSpawned);
            ModEvents.PlayerDisconnected.RegisterHandler(OnPlayerDisconnected);
            ModEvents.GameUpdate.RegisterHandler(OnGameUpdate);
            ModEvents.WorldShuttingDown.RegisterHandler(OnWorldShuttingDown);
        }

        private static void OnPlayerSpawned(ref ModEvents.SPlayerSpawnedInWorldData data)
        {
            ClientInfo client = data.ClientInfo;
            if (!IsServer || client == null || data.IsLocalPlayer || confirmed.Contains(client)) return;
            if (!waiting.ContainsKey(client)) waiting[client] = UnityEngine.Time.realtimeSinceStartup;
            client.SendPackage(NetPackageManager.GetPackage<NetPackageStashNetHello>().Setup(StashNetMod.Version));
        }

        private static void OnPlayerDisconnected(ref ModEvents.SPlayerDisconnectedData data)
        {
            if (data.ClientInfo == null) return;
            confirmed.Remove(data.ClientInfo);
            waiting.Remove(data.ClientInfo);
        }

        private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data)
        {
            confirmed.Clear();
            waiting.Clear();
            ServerVersion = null;
        }

        /// <summary>SERVER: removes players whose game never answered (an older StashNet, or none).</summary>
        private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
        {
            if (waiting.Count == 0 || !IsServer) return;
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (now < nextTimeoutCheck) return;
            nextTimeoutCheck = now + 1f;
            foreach (KeyValuePair<ClientInfo, float> w in new List<KeyValuePair<ClientInfo, float>>(waiting))
            {
                if (VersionGate.TimedOut(w.Value, now)) Kick(w.Key, null);
            }
        }

        /// <summary>SERVER: a player's game said its version.</summary>
        internal static void OnClientHello(ClientInfo client, string version)
        {
            waiting.Remove(client);
            if (VersionGate.Compatible(StashNetMod.Version, version))
            {
                confirmed.Add(client);
                return;
            }
            Kick(client, version);
        }

        private static void Kick(ClientInfo client, string clientVersion)
        {
            waiting.Remove(client);
            confirmed.Remove(client);
            string reason = VersionGate.KickReason(StashNetMod.Version, clientVersion);
            Log.Warning($"[StashNet] Removing {client}: {reason}");
            GameUtils.KickPlayerForClientInfo(client, new GameUtils.KickPlayerData(GameUtils.EKickReason.ModDecision, 0, default, reason));
        }

        /// <summary>SERVER: may this joined player's StashNet requests be handled?</summary>
        public static bool ClientConfirmed(ClientInfo client) => client != null && confirmed.Contains(client);

        /// <summary>PLAYER'S GAME: the server said its version. Answer with ours; warn if they differ.</summary>
        internal static void OnServerHello(string version)
        {
            ServerVersion = version;
            SingletonMonoBehaviour<ConnectionManager>.Instance.SendToServer(
                NetPackageManager.GetPackage<NetPackageStashNetHello>().Setup(StashNetMod.Version));
            if (!VersionGate.Compatible(version, StashNetMod.Version))
                Log.Warning($"[StashNet] This server runs StashNet {version}, you have {StashNetMod.Version}. StashNet is disabled here.");
        }

        /// <summary>
        /// PLAYER'S GAME: may StashNet talk to the server? <paramref name="problemText"/> is the message to
        /// show when not (server's version unknown yet, or different).
        /// </summary>
        public static bool ClientMayUse(out string problemText)
        {
            problemText = null;
            if (IsServer) return true;
            if (ServerVersion == null)
            {
                problemText = Localization.Get("stashnetVersionUnknown");
                return false;
            }
            if (VersionGate.Compatible(ServerVersion, StashNetMod.Version)) return true;
            problemText = string.Format(Localization.Get("stashnetVersionMismatch"), ServerVersion, StashNetMod.Version);
            return false;
        }
    }
}
