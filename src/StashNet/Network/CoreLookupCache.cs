using System.Collections.Generic;
using Platform;

namespace StashNet.Network
{
    /// <summary>
    /// Activation prompts/commands are re-evaluated every frame while a player looks at a block,
    /// and finding the Core scans ~2,000 blocks. Cache "which Core serves this position" for 1 s.
    /// The lock itself is read fresh each time (cheap), so code changes show immediately.
    /// </summary>
    public static class CoreLookupCache
    {
        private const float Lifetime = 1f;

        private struct Entry
        {
            public float Until;
            public bool Found;
            public Vector3i Core;
        }

        private static readonly Dictionary<Vector3i, Entry> cache = new Dictionary<Vector3i, Entry>();

        /// <summary>The Core's lock for this bay/terminal position (null = no Core, or a pre-M4 Core).</summary>
        public static TEFeatureLockable GetLock(World world, Vector3i pos, out bool hasCore)
        {
            float now = UnityEngine.Time.time;
            if (!cache.TryGetValue(pos, out Entry e) || now > e.Until)
            {
                e.Found = StashNetNetwork.TryFindCore(world, pos, out e.Core, out _);
                e.Until = now + Lifetime;
                if (cache.Count > 256) cache.Clear();
                cache[pos] = e;
            }
            hasCore = e.Found;
            return e.Found ? NetworkLock.GetCoreLock(world, e.Core) : null;
        }

        /// <summary>True if the network is locked and the local player isn't on its allowed list.</summary>
        public static bool LockedForLocalPlayer(World world, Vector3i pos, out TEFeatureLockable coreLock)
        {
            coreLock = GetLock(world, pos, out _);
            return !NetworkLock.IsAllowed(coreLock, PlatformManager.InternalLocalUserIdentifier);
        }
    }
}
