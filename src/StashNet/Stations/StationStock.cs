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
            EnsureCounts();
            int n;
            return (plainOnly ? plainCounts : totalCounts).TryGetValue(name, out n) ? n : 0;
        }

        public int Extract(string name, int count)
        {
            if (!Open()) return 0;
            int got = net.Storage.Extract(name, null, count);
            Adjust(name, -got);
            return got;
        }

        public int Insert(string name, int count)
        {
            if (!Open()) return 0;
            int stored = net.Storage.Insert(name, null, count);
            Adjust(name, stored);
            return stored;
        }

        // Counts from the opened drives, built once and kept up to date by Extract/Insert (plain items only move),
        // instead of walking every drive for each Stored call.
        private System.Collections.Generic.Dictionary<string, int> plainCounts, totalCounts;

        private void EnsureCounts()
        {
            if (plainCounts != null) return;
            plainCounts = new System.Collections.Generic.Dictionary<string, int>();
            totalCounts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var i in net.Storage.Aggregate())
            {
                totalCounts.TryGetValue(i.Name, out int t);
                totalCounts[i.Name] = t + i.Count;
                if (i.Variant != null) continue;
                plainCounts.TryGetValue(i.Name, out int p);
                plainCounts[i.Name] = p + i.Count;
            }
        }

        private void Adjust(string name, int delta)
        {
            if (plainCounts == null || delta == 0) return;
            plainCounts.TryGetValue(name, out int p);
            plainCounts[name] = p + delta;
            totalCounts.TryGetValue(name, out int t);
            totalCounts[name] = t + delta;
        }

        public void SaveDirty() => net?.SaveDirty();

        /// <summary>
        /// Puts plain items taken this check back into storage. Whatever storage can't take (stack rounding,
        /// a full network) goes into a vanilla loot bag on top of the station: never silently lost.
        /// </summary>
        public void PutBack(string name, int count)
        {
            if (count <= 0) return;
            int stored = Insert(name, count);
            int rest = count - stored;
            if (rest <= 0) return;
            ItemValue iv = ItemClass.GetItem(name);
            if (iv == null || iv.IsEmpty() || iv.ItemClass == null)
            {
                Log.Error($"[StashNet] Could not put back {rest} {name} at {stationPos}: the item no longer exists.");
                return;
            }
            int stack = System.Math.Max(1, iv.ItemClass.Stacknumber.Value);
            var stacks = new System.Collections.Generic.List<ItemStack>();
            for (int left = rest; left > 0; left -= stack) stacks.Add(new ItemStack(iv.Clone(), System.Math.Min(left, stack)));
            GameManager.Instance.DropContentInLootContainerServer(-1, "DroppedLootContainer",
                stationPos.ToVector3() + new UnityEngine.Vector3(0.5f, 1.5f, 0.5f), stacks.ToArray());
            Log.Warning($"[StashNet] Storage couldn't take back {rest} {name}; put them in a loot bag on the station at {stationPos}.");
        }

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
