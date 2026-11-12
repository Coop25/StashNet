namespace StashNet.Core.Drives
{
    /// <summary>
    /// One "type" on a drive. Name is the item's name (never its numeric ID).
    /// Variant is null for plain stackable items; otherwise an opaque string (encoded by the game
    /// layer) describing quality, durability, mods, etc. Same name + same variant = same entry.
    /// </summary>
    public sealed class DriveEntry
    {
        public DriveEntry(string name, string variant, int count)
        {
            Name = name;
            Variant = variant;
            Count = count;
        }

        public string Name { get; }
        public string Variant { get; }
        public int Count { get; internal set; }

        internal bool Matches(string name, string variant) =>
            string.Equals(Name, name, System.StringComparison.Ordinal) &&
            string.Equals(Variant, variant, System.StringComparison.Ordinal);
    }
}
