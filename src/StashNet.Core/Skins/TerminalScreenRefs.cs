using System;
using System.Collections.Generic;
using StashNet.Core.Protocol;

namespace StashNet.Core.Skins
{
    /// <summary>
    /// Which terminal TV shows which network (Core position; null = no network yet / none). Tells the caller
    /// when a network's last TV leaves, so its screen textures can be destroyed.
    /// </summary>
    public sealed class TerminalScreenRefs<TKey>
    {
        private static readonly TKey[] None = Array.Empty<TKey>();
        private readonly Dictionary<TKey, Pos3?> coreOf = new Dictionary<TKey, Pos3?>();
        private readonly Dictionary<Pos3, HashSet<TKey>> tvsOf = new Dictionary<Pos3, HashSet<TKey>>();

        /// <summary>Starts tracking a TV (a known TV is moved). Returns a Core whose last TV just left, if any.</summary>
        public Pos3? Add(TKey tv, Pos3? core) => Move(tv, core);

        /// <summary>The TV now shows <paramref name="core"/>. Returns the old Core if its last TV just left.</summary>
        public Pos3? Move(TKey tv, Pos3? core)
        {
            Pos3? freed = coreOf.TryGetValue(tv, out Pos3? old) && !Same(old, core) ? Leave(tv, old) : null;
            coreOf[tv] = core;
            if (core.HasValue)
            {
                if (!tvsOf.TryGetValue(core.Value, out HashSet<TKey> set)) tvsOf[core.Value] = set = new HashSet<TKey>();
                set.Add(tv);
            }
            return freed;
        }

        /// <summary>Stops tracking a TV. Returns its Core if that was the Core's last TV; null otherwise or if unknown.</summary>
        public Pos3? Remove(TKey tv)
        {
            if (!coreOf.TryGetValue(tv, out Pos3? old)) return null;
            coreOf.Remove(tv);
            return Leave(tv, old);
        }

        public bool Contains(TKey tv) => coreOf.ContainsKey(tv);

        public Pos3? CoreOf(TKey tv) => coreOf.TryGetValue(tv, out Pos3? c) ? c : null;

        /// <summary>The TVs showing this Core (live view: don't change the refs while iterating it).</summary>
        public IReadOnlyCollection<TKey> TvsOf(Pos3 core) =>
            tvsOf.TryGetValue(core, out HashSet<TKey> set) ? (IReadOnlyCollection<TKey>)set : None;

        public void Clear()
        {
            coreOf.Clear();
            tvsOf.Clear();
        }

        private Pos3? Leave(TKey tv, Pos3? core)
        {
            if (!core.HasValue || !tvsOf.TryGetValue(core.Value, out HashSet<TKey> set)) return null;
            set.Remove(tv);
            if (set.Count > 0) return null;
            tvsOf.Remove(core.Value);
            return core;
        }

        private static bool Same(Pos3? a, Pos3? b) => a.HasValue == b.HasValue && (!a.HasValue || a.Value.Equals(b.Value));
    }
}
