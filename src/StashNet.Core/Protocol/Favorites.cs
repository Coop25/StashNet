using System;
using System.Collections.Generic;
using System.Linq;

namespace StashNet.Core.Protocol
{
    /// <summary>
    /// A player's pinned Storage Terminal items (owner, 2026-11-18: per player, saved on their own PC). A pin is an item
    /// name, so every variant of that item counts. Text format: one name per line; blank lines and '#' lines are ignored.
    /// </summary>
    public sealed class Favorites
    {
        private readonly HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

        public bool Contains(string name) => name != null && names.Contains(name);

        /// <summary>Pins the item, or unpins it if it was pinned. Returns whether it is pinned now.</summary>
        public bool Toggle(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (names.Remove(name)) return false;
            names.Add(name);
            return true;
        }

        public string Serialize() => string.Join("\n", names.OrderBy(n => n, StringComparer.Ordinal)) + "\n";

        public static Favorites Parse(string text)
        {
            var f = new Favorites();
            if (text == null) return f;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                f.names.Add(line);
            }
            return f;
        }

        /// <summary>Pinned items first, then the rest; both parts keep the order they came in (the chosen sort).</summary>
        public static List<T> PinnedFirst<T>(IEnumerable<T> items, Func<T, string> name, Favorites favorites)
        {
            var pinned = new List<T>();
            var rest = new List<T>();
            foreach (T item in items)
            {
                if (favorites != null && favorites.Contains(name(item))) pinned.Add(item);
                else rest.Add(item);
            }
            pinned.AddRange(rest);
            return pinned;
        }
    }
}
