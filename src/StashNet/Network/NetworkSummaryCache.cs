using System.Collections.Generic;
using StashNet.Blocks;
using StashNet.Core.Network;
using StashNet.Drives;

namespace StashNet.Network
{
    /// <summary>
    /// SERVER ONLY. One live NetworkSummary per Core, shared by every Network station of that Core (and
    /// by heat), so the real drives are read only to move items (owner, 2026-11-16: "only one thing
    /// reading the system"):
    ///  - refreshed from memory after every real read (StashNetNetwork.TryOpen) and every save (SaveDirty),
    ///    so StashNet's own changes show at once;
    ///  - marked stale when a bay's slots change outside StashNet (drive moved by hand, another mod): each
    ///    bay compares type / count / drive data of its slots every 2 s (CheckBay; the game updates slot
    ///    items in place, so object identity isn't enough), or when a bay is removed;
    ///  - rebuilt at most once a minute anyway (backstop), only when something asks.
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
            /// <summary>What each bay of the network held at the last refresh (null = bay was open then).</summary>
            public readonly Dictionary<Vector3i, BaySlotPrint[]> Bays = new Dictionary<Vector3i, BaySlotPrint[]>();
            /// <summary>Bays in reach that belong to another Core: never part of this summary.</summary>
            public readonly HashSet<Vector3i> OtherCoreBays = new HashSet<Vector3i>();
        }

        private static readonly Dictionary<Vector3i, Entry> entries = new Dictionary<Vector3i, Entry>();
        private static readonly Dictionary<Vector3i, (bool Found, Vector3i Core, float Until)> coreOf =
            new Dictionary<Vector3i, (bool, Vector3i, float)>();
        private static readonly Dictionary<Vector3i, float> nextBayCheck = new Dictionary<Vector3i, float>();

        /// <summary>The network a rebuild just read, reusable by a take in the same frame (no second read).</summary>
        private static StashNetNetwork freshNet;
        private static int freshFrame = -1;

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
            if (!StashNetNetwork.TryOpen(world, corePos, out StashNetNetwork net, out _, "summary")) return false;
            freshNet = net;
            freshFrame = UnityEngine.Time.frameCount;
            if (!entries.TryGetValue(corePos, out e)) return false;
            summary = e.Summary;
            return true;
        }

        /// <summary>
        /// The network a summary rebuild read for this Core in this same frame, handed out once (so a take
        /// right after a rebuild doesn't read every drive a second time). Null otherwise.
        /// </summary>
        public static StashNetNetwork TakeFresh(Vector3i corePos)
        {
            StashNetNetwork net = freshNet;
            freshNet = null;
            // Also refused if a bay it read has been opened since (never write into an open bay).
            return net != null && freshFrame == UnityEngine.Time.frameCount && net.CorePos == corePos && !net.AnyReadBayOpen() ? net : null;
        }

        /// <summary>Called after a real read and after a save: this network's state, from memory.</summary>
        public static void Record(StashNetNetwork net, IEnumerable<(Vector3i Pos, TEFeatureStorage Storage, bool Ours)> bays, int radius)
        {
            if (!IsServer) return;
            // Any other read or save may have changed drives: the kept rebuild read is no longer safe to reuse.
            if (!ReferenceEquals(net, freshNet)) freshNet = null;
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
            e.OtherCoreBays.Clear();
            foreach (var (pos, storage, ours) in bays)
            {
                if (!ours) e.OtherCoreBays.Add(pos);
                else e.Bays[pos] = storage == null ? null : Prints(storage);
            }
        }

        /// <summary>SERVER, from the bay feature's OnRemove: its network's summary no longer matches.</summary>
        public static void BayRemoved(Vector3i bayPos)
        {
            foreach (Entry e in entries.Values)
            {
                if (e.Bays.ContainsKey(bayPos)) e.Stale = true;
            }
        }

        /// <summary>
        /// SERVER, from the bay's UpdateTick: every 2 s, compare what the bay's slots hold with what its
        /// network's summary saw. A difference (drive put in, taken out or swapped by anything other than
        /// StashNet) marks that summary stale. Skipped while someone has the bay open.
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
            if (storage == null || StashNetNetwork.BayIsOpen(storage)) return;

            BaySlotPrint[] current = null;
            foreach (var kv in entries)
            {
                Entry e = kv.Value;
                if (e.Stale || e.OtherCoreBays.Contains(pos)) continue;
                if (e.Bays.TryGetValue(pos, out BaySlotPrint[] seen))
                {
                    current = current ?? Prints(storage);
                    if (seen == null || !BaySlotPrint.Same(seen, current)) e.Stale = true;
                }
                else if (InReach(kv.Key, pos, e.Radius) && HasDrive(storage))
                {
                    e.Stale = true; // a new bay with drives in it
                }
            }
        }

        private static BaySlotPrint[] Prints(TEFeatureStorage storage)
        {
            var prints = new BaySlotPrint[storage.ItemGrid.Length];
            for (int i = 0; i < prints.Length; i++)
            {
                ItemStack s = storage.ItemGrid[i];
                ItemValue iv = s?.itemValue;
                string data = null;
                if (iv != null && !iv.IsEmpty()) iv.TryGetMetadata(DriveItem.MetadataKey, out data);
                prints[i] = new BaySlotPrint(iv == null || iv.IsEmpty() ? 0 : iv.type, s?.count ?? 0, data);
            }
            return prints;
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
