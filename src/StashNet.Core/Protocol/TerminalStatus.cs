namespace StashNet.Core.Protocol
{
    /// <summary>The Storage Terminal's status line after a server answer.</summary>
    public static class TerminalStatus
    {
        /// <summary>
        /// The answer to the player's own action always sets the line; a background refresh only replaces it when it has
        /// something to say, except that the first listing clears the "Connecting to storage..." text (it stayed forever
        /// before 2026-11-18).
        /// </summary>
        public static string Next(string current, string loadingText, bool hasListing, bool ownAction, string messages)
        {
            if (ownAction || !string.IsNullOrEmpty(messages)) return messages ?? "";
            if (hasListing && current == loadingText) return "";
            return current;
        }
    }
}
