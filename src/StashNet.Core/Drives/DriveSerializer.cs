using System.Globalization;
using System.Text;

namespace StashNet.Core.Drives
{
    /// <summary>
    /// Text format stored in the drive item's metadata:
    ///   SND1
    ///   name \t count \t variant-or-dash
    ///   ...
    /// Fields escape \\, \t, \n. A plain variant is written as "-"; a literal "-" variant is escaped as "\-".
    /// </summary>
    public static class DriveSerializer
    {
        public const string Header = "SND1";

        public static string Serialize(DriveContents contents)
        {
            var sb = new StringBuilder(Header);
            foreach (DriveEntry e in contents.Entries)
            {
                sb.Append('\n')
                  .Append(Escape(e.Name)).Append('\t')
                  .Append(e.Count.ToString(CultureInfo.InvariantCulture)).Append('\t')
                  .Append(e.Variant == null ? "-" : EscapeVariant(e.Variant));
            }
            return sb.ToString();
        }

        /// <summary>
        /// True unless the drive data is certainly empty (M8 scrap guard). Unreadable data counts as
        /// holding items: it may be someone's stuff, so it must never be thrown away.
        /// </summary>
        public static bool HoldsItems(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            try
            {
                return Deserialize(text).TypeCount > 0;
            }
            catch (DriveFormatException)
            {
                return true;
            }
        }

        public static DriveContents Deserialize(string text)
        {
            var contents = new DriveContents();
            if (string.IsNullOrEmpty(text)) return contents;

            string[] lines = text.Split('\n');
            if (lines[0] != Header) throw new DriveFormatException($"Unknown drive format '{Truncate(lines[0])}'.");

            for (int i = 1; i < lines.Length; i++)
            {
                string[] f = lines[i].Split('\t');
                if (f.Length != 3) throw new DriveFormatException($"Line {i}: expected 3 fields, found {f.Length}.");

                string name = Unescape(f[0], i);
                if (name.Length == 0) throw new DriveFormatException($"Line {i}: missing item name.");

                if (!int.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count <= 0)
                    throw new DriveFormatException($"Line {i}: bad count '{Truncate(f[1])}'.");

                string variant = f[2] == "-" ? null : Unescape(f[2], i);
                contents.AddLoaded(name, variant, count);
            }
            return contents;
        }

        private static string EscapeVariant(string v) => v == "-" ? "\\-" : Escape(v);

        private static string Escape(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\n': sb.Append("\\n"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        private static string Unescape(string s, int line)
        {
            if (s.IndexOf('\\') < 0) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (++i >= s.Length) throw new DriveFormatException($"Line {line}: dangling escape.");
                switch (s[i])
                {
                    case '\\': sb.Append('\\'); break;
                    case 't': sb.Append('\t'); break;
                    case 'n': sb.Append('\n'); break;
                    case '-': sb.Append('-'); break;
                    default: throw new DriveFormatException($"Line {line}: bad escape '\\{s[i]}'.");
                }
            }
            return sb.ToString();
        }

        private static string Truncate(string s) => s.Length <= 20 ? s : s.Substring(0, 20) + "...";
    }
}
