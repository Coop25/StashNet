using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Protocol;

namespace StashNet.Core.Network
{
    /// <summary>
    /// Server-side bookkeeping for live Storage Terminal screens (game-free): who watches which terminal,
    /// which Core each terminal belongs to, the last view sent per player and network, and the push,
    /// lock-change and stale-rebuild throttles.
    /// </summary>
    public sealed class ScreenWatchBook
    {
        public const float PushInterval = 1f;
        public const float StaleInterval = 5f;

        sealed class CoreState
        {
            public bool Dirty;
            public bool Flushed;
            public float LastFlush;
            public bool HasLockPrint;
            public string LockPrint;
            public bool StaleSet;
            public float LastStale;
        }

        // terminal -> watching players
        readonly Dictionary<Pos3, HashSet<int>> _watchers = new Dictionary<Pos3, HashSet<int>>();
        // terminal -> core (null = the terminal has no network); absent = not answered yet
        readonly Dictionary<Pos3, Pos3?> _core = new Dictionary<Pos3, Pos3?>();
        readonly Dictionary<Pos3, CoreState> _state = new Dictionary<Pos3, CoreState>();
        readonly Dictionary<(int, Pos3), ScreenView> _sent = new Dictionary<(int, Pos3), ScreenView>();

        public IReadOnlyCollection<Pos3> Terminals => _watchers.Keys.ToList();

        /// <summary>True when no terminal is watched at all (cheap, no allocation).</summary>
        public bool IsEmpty => _watchers.Count == 0;

        /// <summary>True when at least one watched terminal has a Core (cheap, no allocation): TakeDue can do something.</summary>
        public bool HasWatchedCores => _state.Count > 0;

        public void Watch(int player, Pos3 terminal)
        {
            if (!_watchers.TryGetValue(terminal, out var set))
                _watchers[terminal] = set = new HashSet<int>();
            set.Add(player);
            Prune();
        }

        public void Unwatch(int player, Pos3 terminal)
        {
            if (!_watchers.TryGetValue(terminal, out var set)) return;
            set.Remove(player);
            if (set.Count == 0)
            {
                _watchers.Remove(terminal);
                _core.Remove(terminal);
            }
            Prune();
        }

        public void DropPlayer(int player)
        {
            foreach (var t in _watchers.Keys.ToList())
                Unwatch(player, t);
        }

        public bool TryGetCore(Pos3 terminal, out Pos3 core)
        {
            if (_core.TryGetValue(terminal, out var c) && c.HasValue)
            {
                core = c.Value;
                return true;
            }
            core = default;
            return false;
        }

        /// <summary>Players to tell when the terminal's core changed (first answer counts); empty when unchanged.</summary>
        public IReadOnlyList<int> SetCore(Pos3 terminal, Pos3? core)
        {
            if (!_watchers.TryGetValue(terminal, out var set)) return new int[0];
            if (_core.TryGetValue(terminal, out var old) && old.Equals(core)) return new int[0];
            _core[terminal] = core;
            Prune();
            return set.ToList();
        }

        public IReadOnlyCollection<Pos3> Cores => LiveCores().Keys.ToList();

        public IReadOnlyCollection<int> WatchersOf(Pos3 core)
        {
            return LiveCores().TryGetValue(core, out var set) ? set.ToList() : new List<int>();
        }

        public bool ShouldSend(int player, Pos3 core, ScreenView view)
        {
            var key = (player, core);
            if (_sent.TryGetValue(key, out var last) && last == view) return false;
            _sent[key] = view;
            return true;
        }

        /// <summary>
        /// Forget the view last sent to this player for this network, so the next ShouldSend sends it again
        /// (a new Watch: the player's game may have dropped its copy of that network's numbers).
        /// </summary>
        public void ForgetSent(int player, Pos3 core) => _sent.Remove((player, core));

        public void MarkDirty(Pos3 core)
        {
            if (_state.TryGetValue(core, out var s)) s.Dirty = true;
        }

        /// <summary>True when a watched network waits to be pushed (cheap, no allocation): only then is TakeDue worth calling.</summary>
        public bool HasDirty
        {
            get
            {
                foreach (var s in _state.Values)
                    if (s.Dirty) return true;
                return false;
            }
        }

        readonly List<Pos3> _due = new List<Pos3>();

        /// <summary>Dirty networks whose 1 s push throttle has passed (marked clean). The list is reused by the next call.</summary>
        public List<Pos3> TakeDue(float now)
        {
            _due.Clear();
            foreach (var kv in _state)
            {
                var s = kv.Value;
                if (!s.Dirty) continue;
                if (s.Flushed && now - s.LastFlush < PushInterval) continue;
                s.Dirty = false;
                s.Flushed = true;
                s.LastFlush = now;
                _due.Add(kv.Key);
            }
            return _due;
        }

        /// <summary>True when the lock print differs from the last one; the first print for a network counts as a change.</summary>
        public bool LockChanged(Pos3 core, string lockPrint)
        {
            if (!_state.TryGetValue(core, out var s)) return false;
            if (!s.HasLockPrint)
            {
                s.HasLockPrint = true;
                s.LockPrint = lockPrint;
                return true;
            }
            if (s.LockPrint == lockPrint) return false;
            s.LockPrint = lockPrint;
            return true;
        }

        public bool StaleRebuildAllowed(Pos3 core, float now)
        {
            if (!_state.TryGetValue(core, out var s)) return false;
            if (s.StaleSet && now - s.LastStale < StaleInterval) return false;
            s.StaleSet = true;
            s.LastStale = now;
            return true;
        }

        Dictionary<Pos3, HashSet<int>> LiveCores()
        {
            var result = new Dictionary<Pos3, HashSet<int>>();
            foreach (var kv in _core)
            {
                if (!kv.Value.HasValue || !_watchers.TryGetValue(kv.Key, out var players)) continue;
                var core = kv.Value.Value;
                if (!result.TryGetValue(core, out var set)) result[core] = set = new HashSet<int>();
                foreach (var p in players) set.Add(p);
            }
            return result;
        }

        // Forget per-core and per-(player, core) state that nobody watches any more; create state for new cores.
        void Prune()
        {
            var live = LiveCores();
            foreach (var c in _state.Keys.Where(c => !live.ContainsKey(c)).ToList())
                _state.Remove(c);
            foreach (var k in _sent.Keys.Where(k => !live.TryGetValue(k.Item2, out var set) || !set.Contains(k.Item1)).ToList())
                _sent.Remove(k);
            foreach (var c in live.Keys)
                if (!_state.ContainsKey(c)) _state[c] = new CoreState();
        }
    }
}
