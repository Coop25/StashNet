namespace StashNet.Core.Drives
{
    /// <summary>
    /// How much a drive can hold: distinct item types, and stacks. Each item type uses
    /// ceil(count / that item's stack size) stacks, like slots in a chest - so 6,000 stone is
    /// one stack, while three rifles are three.
    /// </summary>
    public readonly struct DriveCapacity
    {
        public DriveCapacity(int maxTypes, int maxStacks)
        {
            MaxTypes = maxTypes;
            MaxStacks = maxStacks;
        }

        public int MaxTypes { get; }
        public int MaxStacks { get; }
    }
}
