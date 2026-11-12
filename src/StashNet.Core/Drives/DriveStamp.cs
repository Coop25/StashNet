using System.Globalization;

namespace StashNet.Core.Drives
{
    /// <summary>The outcome of checking a drive's lock stamp against a network.</summary>
    public readonly struct StampDecision
    {
        public StampDecision(bool readable, bool changed, string newStamp)
        {
            Readable = readable;
            Changed = changed;
            NewStamp = newStamp;
        }

        /// <summary>The network may read and use this drive.</summary>
        public bool Readable { get; }
        /// <summary>The drive's stamp should be rewritten to <see cref="NewStamp"/>.</summary>
        public bool Changed { get; }
        /// <summary>The stamp to write; null = remove the stamp.</summary>
        public string NewStamp { get; }
    }

    /// <summary>
    /// A drive's lock stamp: "x,y,z|passwordHash" of the Core whose locked network last held it.
    /// A drive is readable by its home Core, or by any network with the same password.
    /// </summary>
    public static class DriveStamp
    {
        public static string Format(int x, int y, int z, string hash) =>
            string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}|{3}", x, y, z, hash);

        public static bool TryParse(string s, out int x, out int y, out int z, out string hash)
        {
            x = y = z = 0;
            hash = null;
            if (string.IsNullOrEmpty(s)) return false;
            int bar = s.IndexOf('|');
            if (bar <= 0 || bar == s.Length - 1) return false;
            string[] parts = s.Substring(0, bar).Split(',');
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x) ||
                !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y) ||
                !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out z)) return false;
            hash = s.Substring(bar + 1);
            return true;
        }

        /// <param name="current">The drive's current stamp (null/empty = none).</param>
        /// <param name="networkHash">The network's password hash, or null if it has no password / isn't locked.</param>
        public static StampDecision Decide(string current, int cx, int cy, int cz, string networkHash)
        {
            string desired = networkHash == null ? null : Format(cx, cy, cz, networkHash);

            if (string.IsNullOrEmpty(current))
            {
                return new StampDecision(true, desired != null, desired);
            }
            if (!TryParse(current, out int x, out int y, out int z, out string hash))
            {
                return new StampDecision(false, false, null);
            }

            bool homeCore = x == cx && y == cy && z == cz;
            bool sameCode = networkHash != null && hash == networkHash;
            if (!homeCore && !sameCode)
            {
                return new StampDecision(false, false, null);
            }
            return new StampDecision(true, current != desired, desired);
        }
    }
}
