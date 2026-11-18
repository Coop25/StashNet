namespace StashNet.Core.Protocol
{
    /// <summary>
    /// How the Storage Terminal's search box and category tabs affect each other (owner, 2026-11-18): typing a search
    /// jumps to the All tab (search looks through everything); picking any tab other than All clears the search.
    /// </summary>
    public static class TerminalFilterRules
    {
        /// <summary>The player typed into the search box (not a change made by code): show the All tab.</summary>
        public static bool SearchSwitchesToAll(string text, bool fromCode) => !fromCode && !string.IsNullOrEmpty(text);

        /// <summary>A tab other than All ("" = All) was picked while something is searched: clear the search.</summary>
        public static bool TabClearsSearch(string category, string search) =>
            !string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(search);
    }
}
