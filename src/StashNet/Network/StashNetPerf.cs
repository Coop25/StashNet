using System.Collections.Generic;

namespace StashNet.Network
{
    /// <summary>
    /// SERVER. Counters for `sn perf`: how often the real network was read (every drive decoded) and how
    /// long that took, how often the shared summary answered instead, and why summaries were rebuilt.
    /// </summary>
    public static class StashNetPerf
    {
        private static float since = UnityEngine.Time.time;
        private static int reads, hits;
        private static double readMs, maxReadMs;
        private static readonly Dictionary<string, int> rebuilds = new Dictionary<string, int>();
        private static readonly Dictionary<string, int> readsBy = new Dictionary<string, int>();

        public static void Read(double ms, string by)
        {
            reads++;
            readMs += ms;
            if (ms > maxReadMs) maxReadMs = ms;
            readsBy.TryGetValue(by, out int n);
            readsBy[by] = n + 1;
        }

        public static void Hit() => hits++;

        public static void Rebuild(string reason)
        {
            rebuilds.TryGetValue(reason, out int n);
            rebuilds[reason] = n + 1;
        }

        public static void Reset()
        {
            since = UnityEngine.Time.time;
            reads = hits = 0;
            readMs = maxReadMs = 0;
            rebuilds.Clear();
            readsBy.Clear();
        }

        public static IEnumerable<string> Report()
        {
            float secs = System.Math.Max(1f, UnityEngine.Time.time - since);
            float mins = secs / 60f;
            yield return $"Since {secs:0} s ago (sn perf reset to restart):";
            yield return $"  Real network reads: {reads} ({reads / mins:0.0}/min), {readMs:0.0} ms total, {(reads > 0 ? readMs / reads : 0):0.00} ms avg, {maxReadMs:0.00} ms max.";
            foreach (var kv in readsBy) yield return $"    by {kv.Key}: {kv.Value}";
            yield return $"  Answered from the shared summary (no read): {hits} ({hits / mins:0.0}/min).";
            foreach (var kv in rebuilds) yield return $"  Summary rebuilds ({kv.Key}): {kv.Value}";
            yield return $"  Networks with a summary: {NetworkSummaryCache.Count}.";
        }
    }
}
