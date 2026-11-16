using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace StashNet.Core.Stations
{
    /// <summary>
    /// "Keep this many in storage" for one Network station: the item, the target, and the recipe
    /// exactly as the setting player's Craft button would queue it (count, time, tier and per-craft
    /// ingredients after their perks), plus who set it (for the Core lock).
    /// </summary>
    public sealed class StockRule
    {
        public const int MaxTarget = 1000000;
        public const int MaxOutputCount = 10000;
        public const float MaxCraftTime = 86400f;
        public const int MaxTier = 6;
        public const int MaxIngredients = 16;
        public const int MaxIngredientAmount = 100000;

        public string Item;
        public int Target;
        public string Recipe;
        public int OutputCount;
        public float CraftTime;
        public int Tier;
        public string Owner = "";
        public List<(string Name, int Amount)> Ingredients = new List<(string Name, int Amount)>();

        /// <summary>rule|item|target|recipe|outputCount|craftTime|tier|owner|name*amt;name*amt</summary>
        public string ToLine() => ToLine(true);

        /// <param name="includeOwner">False for text sent to clients: other players' platform IDs stay on the server.</param>
        public string ToLine(bool includeOwner)
        {
            string ings = string.Join(";", Ingredients.Select(i => i.Name + "*" + i.Amount.ToString(CultureInfo.InvariantCulture)));
            return string.Join("|", new[]
            {
                "rule", Item, Target.ToString(CultureInfo.InvariantCulture), Recipe,
                OutputCount.ToString(CultureInfo.InvariantCulture), CraftTime.ToString("R", CultureInfo.InvariantCulture),
                Tier.ToString(CultureInfo.InvariantCulture), includeOwner ? Owner ?? "" : "", ings,
            });
        }

        public StockRule Clone() => new StockRule
        {
            Item = Item, Target = Target, Recipe = Recipe, OutputCount = OutputCount, CraftTime = CraftTime,
            Tier = Tier, Owner = Owner, Ingredients = new List<(string Name, int Amount)>(Ingredients),
        };

        public static bool TryParseLine(string line, out StockRule rule, out string error)
        {
            rule = null;
            error = null;
            string[] f = (line ?? "").Split('|');
            if (f.Length != 9 || f[0] != "rule") return Fail("not a rule line", out error);
            var r = new StockRule { Item = f[1], Recipe = f[3], Owner = f[7] };
            if (!ValidName(r.Item) || !ValidName(r.Recipe)) return Fail("bad item or recipe name", out error);
            if (r.Owner.Length > 256 || r.Owner.IndexOfAny(new[] { ';', '*', '\n', '\r' }) >= 0) return Fail("bad owner", out error);
            if (!TryInt(f[2], 1, MaxTarget, out r.Target)) return Fail("bad target", out error);
            if (!TryInt(f[4], 1, MaxOutputCount, out r.OutputCount)) return Fail("bad output count", out error);
            if (!float.TryParse(f[5], NumberStyles.Float, CultureInfo.InvariantCulture, out r.CraftTime)
                || float.IsNaN(r.CraftTime) || r.CraftTime < 0f || r.CraftTime > MaxCraftTime) return Fail("bad craft time", out error);
            if (!TryInt(f[6], 0, MaxTier, out r.Tier)) return Fail("bad tier", out error);

            string[] ings = f[8].Length == 0 ? new string[0] : f[8].Split(';');
            if (ings.Length < 1 || ings.Length > MaxIngredients) return Fail("bad ingredient count", out error);
            foreach (string ing in ings)
            {
                string[] p = ing.Split('*');
                if (p.Length != 2 || !ValidName(p[0]) || !TryInt(p[1], 1, MaxIngredientAmount, out int amt)) return Fail("bad ingredient", out error);
                if (r.Ingredients.Any(i => i.Name == p[0])) return Fail("duplicate ingredient", out error);
                r.Ingredients.Add((p[0], amt));
            }
            rule = r;
            return true;
        }

        /// <summary>Item, recipe and fuel names: non-empty, at most 128 chars, none of | ; * , space or line breaks.</summary>
        public static bool ValidName(string s) =>
            !string.IsNullOrEmpty(s) && s.Length <= 128 && s.IndexOfAny(new[] { '|', ';', '*', ',', ' ', '\n', '\r', '\t' }) < 0;

        private static bool TryInt(string s, int min, int max, out int v) =>
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) && v >= min && v <= max;

        private static bool Fail(string why, out string error)
        {
            error = why;
            return false;
        }
    }

    /// <summary>
    /// Forge: keep this metal's melted amount (the unit_&lt;metal&gt; tally) at Target by feeding raw
    /// scrap from storage, plus who set it (for the Core lock, like stock rules).
    /// </summary>
    public sealed class MeltTarget
    {
        public const int MaxTarget = 1000000;

        public string Metal;
        public int Target;
        public string Owner = "";

        /// <summary>melt|metal|target|owner</summary>
        public string ToLine(bool includeOwner = true) =>
            string.Join("|", new[] { "melt", Metal, Target.ToString(CultureInfo.InvariantCulture), includeOwner ? Owner ?? "" : "" });

        public MeltTarget Clone() => new MeltTarget { Metal = Metal, Target = Target, Owner = Owner };

        public static bool TryParseLine(string line, out MeltTarget melt, out string error)
        {
            melt = null;
            error = null;
            string[] f = (line ?? "").Split('|');
            if (f.Length != 4 || f[0] != "melt") { error = "not a melt line"; return false; }
            if (!StockRule.ValidName(f[1])) { error = "bad metal"; return false; }
            if (!int.TryParse(f[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int target) || target < 1 || target > MaxTarget)
            {
                error = "bad target";
                return false;
            }
            if (f[3].Length > 256 || f[3].IndexOfAny(new[] { ';', '*', '\n', '\r' }) >= 0) { error = "bad owner"; return false; }
            melt = new MeltTarget { Metal = f[1], Target = target, Owner = f[3] };
            return true;
        }
    }

    /// <summary>One Network station's settings: fuel mode, up to 8 stock rules, the output switch, up to 6 melt targets.</summary>
    public sealed class StationSettings
    {
        public const int MaxRules = 8;
        public const int MaxMelts = 6;

        public string FuelMode = FuelModes.Manual;
        public List<StockRule> Rules = new List<StockRule>();
        /// <summary>Finished crafts and cancel refunds go into storage (true, default) or stay in the station.</summary>
        public bool OutputOn = true;
        public List<MeltTarget> Melts = new List<MeltTarget>();

        public bool IsDefault => FuelMode == FuelModes.Manual && Rules.Count == 0 && OutputOn && Melts.Count == 0;

        /// <summary>Adds the target, or replaces the one for the same metal. False when 6 exist already.</summary>
        public bool SetMelt(MeltTarget melt)
        {
            int i = Melts.FindIndex(m => m.Metal == melt.Metal);
            if (i >= 0)
            {
                Melts[i] = melt;
                return true;
            }
            if (Melts.Count >= MaxMelts) return false;
            Melts.Add(melt);
            return true;
        }

        public bool RemoveMelt(string metal) => Melts.RemoveAll(m => m.Metal == metal) > 0;

        /// <summary>Adds the rule, or replaces the rule for the same item. False when 8 rules exist already.</summary>
        public bool SetRule(StockRule rule)
        {
            int i = Rules.FindIndex(r => r.Item == rule.Item);
            if (i >= 0)
            {
                Rules[i] = rule;
                return true;
            }
            if (Rules.Count >= MaxRules) return false;
            Rules.Add(rule);
            return true;
        }

        public bool RemoveRule(string item) => Rules.RemoveAll(r => r.Item == item) > 0;

        public StationSettings Clone() => new StationSettings
        {
            FuelMode = FuelMode,
            Rules = Rules.Select(r => r.Clone()).ToList(),
            OutputOn = OutputOn,
            Melts = Melts.Select(m => m.Clone()).ToList(),
        };

        /// <summary>"fuel &lt;mode&gt;", one rule line each, "output off" when off, one melt line each.</summary>
        /// <param name="includeOwners">False for text sent to clients (see StockRule.ToLine).</param>
        public string Serialize(bool includeOwners = true)
        {
            var sb = new StringBuilder();
            sb.Append("fuel ").Append(FuelMode).Append('\n');
            foreach (StockRule r in Rules) sb.Append(r.ToLine(includeOwners)).Append('\n');
            if (!OutputOn) sb.Append("output off\n");
            foreach (MeltTarget m in Melts) sb.Append(m.ToLine(includeOwners)).Append('\n');
            return sb.ToString();
        }

        /// <summary>Throws FormatException on any bad line. Empty text = default settings.</summary>
        public static StationSettings Parse(string text)
        {
            var s = new StationSettings();
            foreach (string raw in (text ?? "").Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line.StartsWith("fuel ", StringComparison.Ordinal))
                {
                    string mode = line.Substring(5);
                    if (!StockRule.ValidName(mode)) throw new FormatException("Bad fuel mode.");
                    s.FuelMode = mode;
                }
                else if (line == "output off" || line == "output on")
                {
                    s.OutputOn = line == "output on";
                }
                else if (line.StartsWith("melt|", StringComparison.Ordinal))
                {
                    if (!MeltTarget.TryParseLine(line, out MeltTarget melt, out string meltError)) throw new FormatException("Bad melt line: " + meltError);
                    if (!s.SetMelt(melt)) throw new FormatException("Too many melt targets.");
                }
                else if (StockRule.TryParseLine(line, out StockRule rule, out string error))
                {
                    if (!s.SetRule(rule)) throw new FormatException("Too many rules.");
                }
                else throw new FormatException("Bad station line: " + error);
            }
            return s;
        }
    }

    /// <summary>Every station's settings in one world, keyed by position + block name (the server's stations.txt).</summary>
    public sealed class StationSettingsFile
    {
        private readonly Dictionary<(int, int, int), (string Block, StationSettings Settings)> stations =
            new Dictionary<(int, int, int), (string, StationSettings)>();

        /// <summary>A copy; default settings when nothing is stored or another block was stored here.</summary>
        public StationSettings Get(int x, int y, int z, string block) =>
            stations.TryGetValue((x, y, z), out var e) && e.Block == block ? e.Settings.Clone() : new StationSettings();

        /// <summary>Default settings remove the entry.</summary>
        public void Set(int x, int y, int z, string block, StationSettings s)
        {
            if (s == null || s.IsDefault) stations.Remove((x, y, z));
            else stations[(x, y, z)] = (block, s.Clone());
        }

        public bool Remove(int x, int y, int z) => stations.Remove((x, y, z));

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var kv in stations.OrderBy(k => k.Key))
            {
                sb.Append("station ")
                    .Append(kv.Key.Item1.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(kv.Key.Item2.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(kv.Key.Item3.ToString(CultureInfo.InvariantCulture)).Append(' ')
                    .Append(kv.Value.Block).Append('\n')
                    .Append(kv.Value.Settings.Serialize()).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>A station block with any bad line is skipped; the others are kept.</summary>
        public static StationSettingsFile Parse(string text)
        {
            var file = new StationSettingsFile();
            string[] lines = (text ?? "").Replace("\r", "").Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                string header = lines[i++];
                if (!header.StartsWith("station ", StringComparison.Ordinal)) continue;
                var body = new StringBuilder();
                while (i < lines.Length && !lines[i].StartsWith("station ", StringComparison.Ordinal)) body.Append(lines[i++]).Append('\n');

                string[] h = header.Split(' ');
                if (h.Length != 3 || !StockRule.ValidName(h[2])) continue;
                string[] p = h[1].Split(',');
                if (p.Length != 3
                    || !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    || !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
                    || !int.TryParse(p[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z)) continue;
                try
                {
                    file.Set(x, y, z, h[2], StationSettings.Parse(body.ToString()));
                }
                catch (FormatException)
                {
                    // Skip this station only.
                }
            }
            return file;
        }
    }

    /// <summary>Fuel modes: Manual (vanilla), Auto (fire on/off only), or a fuel item name (Auto + refill).</summary>
    public static class FuelModes
    {
        public const string Manual = "Manual";
        public const string Auto = "Auto";

        public static bool IsFuelItem(string mode) => mode != null && mode != Manual && mode != Auto;
    }
}
