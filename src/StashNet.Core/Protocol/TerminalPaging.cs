namespace StashNet.Core.Protocol
{
    /// <summary>Storage Terminal paging helpers.</summary>
    public static class TerminalPaging
    {
        /// <summary>The page that shows list position <paramref name="index"/>; null when it isn't shown (index -1) or there are no cells.</summary>
        public static int? PageOf(int index, int perPage)
        {
            if (index < 0 || perPage <= 0) return null;
            return index / perPage;
        }
    }
}
