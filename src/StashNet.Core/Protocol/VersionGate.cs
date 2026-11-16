namespace StashNet.Core.Protocol
{
    /// <summary>
    /// Server and players must run the exact same StashNet version (owner, 2026-11-16): the network
    /// messages have no version of their own, so two versions could misread each other and lose or
    /// duplicate items. Game glue: Network/VersionCheck.cs.
    /// </summary>
    public static class VersionGate
    {
        /// <summary>How long a joined player's game has to answer with its version before it is removed.</summary>
        public const float HelloTimeoutSeconds = 30f;

        public static bool Compatible(string a, string b) =>
            !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) && a.Trim() == b.Trim();

        public static bool TimedOut(float sentAt, float now) => now - sentAt >= HelloTimeoutSeconds;

        /// <summary>Shown to a removed player (the game puts it under its own "denied by mod" line).</summary>
        public static string KickReason(string serverVersion, string clientVersion) =>
            "This server runs StashNet " + serverVersion + ", you have "
            + (string.IsNullOrWhiteSpace(clientVersion) ? "an older StashNet (or none)" : "StashNet " + clientVersion.Trim())
            + ". Install StashNet " + serverVersion + " to join.";
    }
}
