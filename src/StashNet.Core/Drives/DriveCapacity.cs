namespace StashNet.Core.Drives
{
    /// <summary>
    /// How much a drive can hold, in stacks. Each item type uses ceil(count / that item's stack
    /// size) stacks, like slots in a chest - so 6,000 stone is one stack, while three rifles are
    /// three. There is no separate limit on item types (owner's decision, 2026-11-12).
    /// </summary>
    public readonly struct DriveCapacity
    {
        public DriveCapacity(int maxStacks)
        {
            MaxStacks = maxStacks;
        }

        public int MaxStacks { get; }
    }
}
