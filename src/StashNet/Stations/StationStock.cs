using System.Linq;
using StashNet.Core.Network;
using StashNet.Network;

namespace StashNet.Stations
{
    /// <summary>
    /// SERVER. One station's view of its network for one automation check. Counts come from the shared
    /// live summary (NetworkSummaryCache) until the station actually takes something; the first
    /// Extract/Insert reads the real network fresh (as before), and from then on counts come from that
    /// read. Saving updates the shared summary, so the next station in line sees what was taken.
    /// Stations are checked one at a time on the server's main thread, so takes queue up by themselves.
    /// </summary>
    public sealed class StationStock
    {
        private readonly World world;
        private readonly Vector3i stationPos;
        private readonly NetworkSummary summary;
        private readonly Vector3i corePos;
        private StashNetNetwork net;
        private bool opened;

        private StationStock(World world, Vector3i stationPos, Vector3i corePos, NetworkSummary summary)
        {
            this.world = world;
            this.stationPos = stationPos;
            this.summary = summary;
            this.corePos = corePos;
            CoreLock = NetworkLock.GetCoreLock(world, corePos);
        }

        /// <summary>Null when the station has no Core (nothing to automate from storage).</summary>
        public static StationStock For(World world, Vector3i stationPos)
        {
            if (!NetworkSummaryCache.TryGet(world, stationPos, out Vector3i corePos, out NetworkSummary summary)) return null;
            return new StationStock(world, stationPos, corePos, summary);
        }

        public TEFeatureLockable CoreLock { get; }

        /// <summary>Stored count (plain only, or every variant): the summary until opened, then the real drives.</summary>
        public int Stored(string name, bool plainOnly = false)
        {
            if (net == null) return plainOnly ? summary.Plain(name) : summary.Total(name);
            return net.Storage.Aggregate().Where(i => i.Name == name && (!plainOnly || i.Variant == null)).Sum(i => i.Count);
        }

        public int Extract(string name, int count) => Open() ? net.Storage.Extract(name, null, count) : 0;

        public int Insert(string name, int count) => Open() ? net.Storage.Insert(name, null, count) : 0;

        public void SaveDirty() => net?.SaveDirty();

        private bool Open()
        {
            if (opened) return net != null;
            opened = true;
            // Reuse the read a summary rebuild just did this frame; else a fresh read. Never a different Core
            // than the one whose summary (and lock) this check used.
            net = NetworkSummaryCache.TakeFresh(corePos);
            if (net == null && !StashNetNetwork.TryOpen(world, stationPos, out net, out _, "station takes items")) net = null;
            if (net != null && net.CorePos != corePos) net = null;
            return net != null;
        }
    }
}
