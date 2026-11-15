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
        public string ToLine()
        {
            string ings = string.Join(";", Ingredients.Select(i => i.Name + "*" + i.Amount.ToString(CultureInfo.InvariantCulture)));
            return string.Join("|", new[]
            {
                "rule", Item, Target.ToString(CultureInfo.InvariantCulture), Recipe,
                OutputCount.ToString(CultureInfo.InvariantCulture), CraftTime.ToString("R", CultureInfo.InvariantCulture),
                Tier.ToString(CultureInfo.InvariantCulture), Owner ?? "", ings,
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

    /// <summary>One Network station's automation settings: fuel mode and up to 8 stock rules.</summary>
    public sealed class StationSettings
    {
        public const int MaxRules = 8;

        public string FuelMode = FuelModes.Manual;
        public List<StockRule> Rules = new List<StockRule>();

        public bool IsDefault => FuelMode == FuelModes.Manual && Rules.Count == 0;

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

        public StationSettings Clone() => new StationSettings { FuelMode = FuelMode, Rules = Rules.Select(r => r.Clone()).ToList() };

        /// <summary>"fuel &lt;mode&gt;" then one rule line each.</summary>
        public string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("fuel ").Append(FuelMode).Append('\n');
            foreach (StockRule r in Rules) sb.Append(r.ToLine()).Append('\n');
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

        /// <summary>The fuel button's next mode: Manual → Auto → fuels in storage (by name) → Manual.</summary>
        public static string Next(string current, IEnumerable<string> fuelsInStorage)
        {
            List<string> order = new List<string> { Manual, Auto };
            order.AddRange(fuelsInStorage.Distinct().OrderBy(n => n, StringComparer.Ordinal));
            int i = order.IndexOf(current);
            if (i < 0) return Manual; // the chosen fuel ran out of storage
            return order[(i + 1) % order.Count];
        }
    }
}
