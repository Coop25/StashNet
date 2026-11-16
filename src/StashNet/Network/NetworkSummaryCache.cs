using System.Collections.Generic;
using StashNet.Blocks;
using StashNet.Core.Network;

namespace StashNet.Network
{
    /// <summary>
    /// SERVER ONLY. One live NetworkSummary per Core, shared by every Network station of that Core
    /// (owner, 2026-11-16: "only one thing reading the system"):
    ///  - refreshed for free after every real read (StashNetNetwork.TryOpen) and every save (SaveDirty),
    ///    so StashNet's own changes show at once;
    ///  - marked stale when a bay's slots change outside StashNet (drive moved by hand, another mod):
    ///    each bay compares its slot references every 2 s (CheckBay), no drive decoding;
    ///  - rebuilt at most once a minute anyway (backstop), only when a station asks.
    /// Decisions only: moving items always reads the real drives.
    /// </summary>
    public static class NetworkSummaryCache
    {
        private const float BackstopSeconds = 60f;
        private const float LookupSeconds = 10f;
        private const float BayCheckSeconds = 2f;

        private sealed class Entry
        {
            public NetworkSummary Summary;
            public float BuiltAt;
            public bool Stale;
            public int Radius;
            /// <summary>Slot item references of every bay in the network at the last refresh (null = bay open then).</summary>
            public Dictionary<Vector3i, ItemValue[]> Bays = new Dictionary<Vector3i, ItemValue[]>();
        }

        private static readonly Dictionary<Vector3i, Entry> entries = new Dictionary<Vector3i, Entry>();
        private static readonly Dictionary<Vector3i, (bool Found, Vector3i Core, float Until)> coreOf =
            new Dictionary<Vector3i, (bool, Vector3i, float)>();
        private static readonly Dictionary<Vector3i, float> nextBayCheck = new Dictionary<Vector3i, float>();

        public static int Count => entries.Count;

        private static bool IsServer => SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;

        /// <summary>
        /// The live summary for the network serving <paramref name="stationPos"/>. Reads the real network
        /// only to build a missing, stale or minute-old summary. False when there is no Core.
        /// </summary>
        public static bool TryGet(World world, Vector3i stationPos, out Vector3i corePos, out NetworkSummary summary)
        {
            summary = null;
            float now = UnityEngine.Time.time;
            if (!coreOf.TryGetValue(stationPos, out var c) || now > c.Until)
            {
                c.Found = StashNetNetwork.TryFindCore(world, stationPos, out c.Core, out _);
                c.Until = now + LookupSeconds;
                if (coreOf.Count > 512) coreOf.Clear();
                coreOf[stationPos] = c;
            }
            corePos = c.Core;
            if (!c.Found) return false;

            if (entries.TryGetValue(corePos, out Entry e) && !NetworkSummary.NeedsRebuild(e.Stale, e.BuiltAt, now, BackstopSeconds))
            {
                StashNetPerf.Hit();
                summary = e.Summary;
                return true;
            }
            StashNetPerf.Rebuild(e == null ? "first" : e.Stale ? "drives changed" : "1-minute backstop");
            // A real read; TryOpen records the fresh summary itself.
            if (!StashNetNetwork.TryOpen(world, corePos, out _, out _, "summary")) return false;
            if (!entries.TryGetValue(corePos, out e)) return false;
            summary = e.Summary;
            return true;
        }

        /// <summary>Called after a real read and after a save: this network's state, from memory.</summary>
        public static void Record(StashNetNetwork net, IEnumerable<(Vector3i Pos, TEFeatureStorage Storage)> bays, int radius)
        {
            if (!IsServer) return;
            if (!entries.TryGetValue(net.CorePos, out Entry e))
            {
                if (entries.Count > 256) entries.Clear();
                e = new Entry();
                entries[net.CorePos] = e;
            }
            e.Summary = NetworkSummary.From(net.Storage, net.HeatWeight, net.Storage.Drives.Count + net.SkippedDrives);
            e.BuiltAt = UnityEngine.Time.time;
            e.Stale = false;
            e.Radius = radius;
            e.Bays.Clear();
            foreach (var (pos, storage) in bays) e.Bays[pos] = storage == null ? null : Fingerprint(storage);
        }

        public static void MarkStale(Vector3i corePos)
        {
            if (entries.TryGetValue(corePos, out Entry e)) e.Stale = true;
        }

        /// <summary>
        /// SERVER, from the bay's UpdateTick: every 2 s, compare the bay's slot references with what its
        /// network's summary saw. A mismatch (drive put in or taken out by anything other than StashNet)
        /// marks that summary stale. Skipped while someone has the bay open, and free when no summary exists.
        /// </summary>
        public static void CheckBay(World world, TileEntityComposite bay)
        {
            if (entries.Count == 0 || bay == null || !IsServer) return;
            Vector3i pos = bay.ToWorldPos();
            float now = UnityEngine.Time.time;
            if (nextBayCheck.TryGetValue(pos, out float t) && now < t) return;
            if (nextBayCheck.Count > 4096) nextBayCheck.Clear();
            nextBayCheck[pos] = now + BayCheckSeconds;

            TEFeatureStorage storage = bay.GetFeature<TEFeatureStorage>();
            if (storage == null || storage.IsUserAccessing()) return;

            foreach (var kv in entries)
            {
                Entry e = kv.Value;
                if (e.Stale) continue;
                if (e.Bays.TryGetValue(pos, out ItemValue[] seen))
                {
                    if (seen == null || !SameSlots(seen, storage)) e.Stale = true;
                }
                else if (InReach(kv.Key, pos, e.Radius) && HasDrive(storage))
                {
                    e.Stale = true; // a new bay with drives in it
                }
            }
        }

        private static ItemValue[] Fingerprint(TEFeatureStorage storage)
        {
            var refs = new ItemValue[storage.ItemGrid.Length];
            for (int i = 0; i < refs.Length; i++) refs[i] = storage.ItemGrid[i].itemValue;
            return refs;
        }

        private static bool SameSlots(ItemValue[] seen, TEFeatureStorage storage)
        {
            if (seen.Length != storage.ItemGrid.Length) return false;
            for (int i = 0; i < seen.Length; i++)
            {
                if (!ReferenceEquals(seen[i], storage.ItemGrid[i].itemValue)) return false;
            }
            return true;
        }

        private static bool HasDrive(TEFeatureStorage storage)
        {
            for (int i = 0; i < storage.ItemGrid.Length; i++)
            {
                ItemValue iv = storage.ItemGrid[i].itemValue;
                if (iv != null && !iv.IsEmpty() && DriveRules.IsDrive(iv)) return true;
            }
            return false;
        }

        private static bool InReach(Vector3i core, Vector3i pos, int radius) =>
            System.Math.Abs(core.x - pos.x) <= radius && System.Math.Abs(core.y - pos.y) <= radius && System.Math.Abs(core.z - pos.z) <= radius;
    }
}
