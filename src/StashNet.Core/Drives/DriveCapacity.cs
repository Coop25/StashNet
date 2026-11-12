namespace StashNet.Core.Drives
{
    /// <summary>How much a drive can hold: distinct item types and total item count.</summary>
    public readonly struct DriveCapacity
    {
        public DriveCapacity(int maxTypes, int maxItems)
        {
            MaxTypes = maxTypes;
            MaxItems = maxItems;
        }

        public int MaxTypes { get; }
        public int MaxItems { get; }
    }
}
