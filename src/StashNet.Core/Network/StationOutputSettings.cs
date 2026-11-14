using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StashNet.Core.Network
{
    /// <summary>
    /// Per-station "finished items go into storage" switch (owner, 2026-11-14: on by default).
    /// Only stations switched OFF are kept, one "x,y,z" line each; the server saves this in the
    /// world's save folder.
    /// </summary>
    public sealed class StationOutputSettings
    {
        private readonly HashSet<(int, int, int)> off = new HashSet<(int, int, int)>();

        public int OffCount => off.Count;

        public bool IsOn(int x, int y, int z) => !off.Contains((x, y, z));

        /// <summary>Returns true when the setting changed.</summary>
        public bool Set(int x, int y, int z, bool on) => on ? off.Remove((x, y, z)) : off.Add((x, y, z));

        public string Serialize()
        {
            var sb = new StringBuilder("# StashNet stations whose finished items stay in the station (x,y,z)\n");
            foreach (var p in off.OrderBy(p => p.Item1).ThenBy(p => p.Item2).ThenBy(p => p.Item3))
            {
                sb.Append(p.Item1.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(p.Item2.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(p.Item3.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>Unreadable lines are skipped (that station falls back to "on").</summary>
        public static StationOutputSettings Parse(string text)
        {
            var s = new StationOutputSettings();
            if (string.IsNullOrEmpty(text)) return s;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split(',');
                if (parts.Length != 3) continue;
                if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
                    && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z))
                {
                    s.off.Add((x, y, z));
                }
            }
            return s;
        }
    }
}
