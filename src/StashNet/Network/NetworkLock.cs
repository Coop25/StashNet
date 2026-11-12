namespace StashNet.Network
{
    /// <summary>
    /// The network's password lives in the Core's vanilla TEFeatureLockable. Bays and terminals
    /// have no lock of their own - they ask the Core (single source of truth).
    /// A Core without lock data (placed before Milestone 4) counts as unlocked.
    /// </summary>
    public static class NetworkLock
    {
        public static TEFeatureLockable GetCoreLock(World world, Vector3i corePos) =>
            (world?.GetTileEntity(corePos) as TileEntityComposite)?.GetFeature<TEFeatureLockable>();

        /// <summary>Finds the Core for a bay/terminal position and its lock (lock may be null).</summary>
        public static bool TryGetForPosition(World world, Vector3i pos, out Vector3i corePos, out TEFeatureLockable coreLock)
        {
            coreLock = null;
            if (!StashNetNetwork.TryFindCore(world, pos, out corePos, out _)) return false;
            coreLock = GetCoreLock(world, corePos);
            return true;
        }

        public static bool IsLocked(TEFeatureLockable coreLock) => coreLock != null && coreLock.IsLocked();

        public static bool IsAllowed(TEFeatureLockable coreLock, PlatformUserIdentifierAbs user) =>
            !IsLocked(coreLock) || (user != null && coreLock.IsUserAllowed(user));

        /// <summary>Server-side identity of a player (for allowed-list checks).</summary>
        public static PlatformUserIdentifierAbs PlayerId(EntityPlayer player) =>
            player == null ? null : GameManager.Instance.persistentPlayers?.GetPlayerDataFromEntityID(player.entityId)?.PrimaryId;
    }
}
