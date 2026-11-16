using System;
using System.Collections.Generic;
using StashNet.Core.Stations;
using Xunit;

namespace StashNet.Core.Tests
{
    public class StationSettingsTests
    {
        private static StockRule Rule(string item = "resourceConcreteMix", int target = 500) => new StockRule
        {
            Item = item,
            Target = target,
            Recipe = item,
            OutputCount = 10,
            CraftTime = 12.5f,
            Tier = 1,
            Owner = "Steam_7656",
            Ingredients = new List<(string Name, int Amount)> { ("resourceRockSmall", 20), ("resourceCement", 5) },
        };

        [Fact]
        public void RoundTrip_KeepsFuelModeAndRules()
        {
            var s = new StationSettings { FuelMode = "resourceCoal" };
            Assert.True(s.SetRule(Rule()));
            Assert.True(s.SetRule(Rule("foodCornBread", 20)));

            StationSettings back = StationSettings.Parse(s.Serialize());

            Assert.Equal("resourceCoal", back.FuelMode);
            Assert.Equal(2, back.Rules.Count);
            StockRule r = back.Rules[0];
            Assert.Equal("resourceConcreteMix", r.Item);
            Assert.Equal(500, r.Target);
            Assert.Equal(10, r.OutputCount);
            Assert.Equal(12.5f, r.CraftTime);
            Assert.Equal(1, r.Tier);
            Assert.Equal("Steam_7656", r.Owner);
            Assert.Equal(new[] { ("resourceRockSmall", 20), ("resourceCement", 5) }, r.Ingredients);
            Assert.Equal("foodCornBread", back.Rules[1].Item);
        }

        [Fact]
        public void Default_IsDefault()
        {
            Assert.True(new StationSettings().IsDefault);
            Assert.True(StationSettings.Parse("").IsDefault);
        }

        [Fact]
        public void Parse_RefusesNameWithSeparator()
        {
            string line = Rule().ToLine().Replace("resourceRockSmall", "resource;Rock");
            Assert.False(StockRule.TryParseLine(line, out _, out _));
            Assert.False(StockRule.TryParseLine(Rule("bad item").ToLine(), out _, out _));
        }

        [Fact]
        public void Parse_RefusesTargetOverMillion()
        {
            Assert.False(StockRule.TryParseLine(Rule(target: 1000001).ToLine(), out _, out string error));
            Assert.NotNull(error);
            Assert.True(StockRule.TryParseLine(Rule(target: 1000000).ToLine(), out _, out _));
            Assert.False(StockRule.TryParseLine(Rule(target: 0).ToLine(), out _, out _));
        }

        [Fact]
        public void Parse_RefusesDuplicateIngredient()
        {
            string line = Rule().ToLine().Replace("resourceCement*5", "resourceRockSmall*5");
            Assert.False(StockRule.TryParseLine(line, out _, out _));
        }

        [Fact]
        public void SerializeForClients_LeavesOutOwners()
        {
            var s = new StationSettings();
            s.SetRule(Rule());
            string text = s.Serialize(includeOwners: false);
            Assert.DoesNotContain("Steam_7656", text);
            Assert.Equal("", StationSettings.Parse(text).Rules[0].Owner);
            Assert.Contains("Steam_7656", s.Serialize());
        }

        [Fact]
        public void Parse_RefusesGarbage()
        {
            Assert.False(StockRule.TryParseLine("rule|x", out _, out _));
            Assert.False(StockRule.TryParseLine("", out _, out _));
            Assert.Throws<FormatException>(() => StationSettings.Parse("fuel Manual\nnonsense"));
        }

        [Fact]
        public void SetRule_ReplacesSameItem()
        {
            var s = new StationSettings();
            s.SetRule(Rule(target: 100));
            s.SetRule(Rule(target: 300));
            Assert.Single(s.Rules);
            Assert.Equal(300, s.Rules[0].Target);
            Assert.True(s.RemoveRule("resourceConcreteMix"));
            Assert.False(s.RemoveRule("resourceConcreteMix"));
            Assert.True(s.IsDefault);
        }

        [Fact]
        public void SetRule_RefusesNinthRule()
        {
            var s = new StationSettings();
            for (int i = 0; i < StationSettings.MaxRules; i++) Assert.True(s.SetRule(Rule("item" + i)));
            Assert.False(s.SetRule(Rule("item8")));
            Assert.Equal(8, s.Rules.Count);
            Assert.True(s.SetRule(Rule("item3", 7))); // replacing still works when full
        }

        [Fact]
        public void File_BadStationBlockSkippedOthersKept()
        {
            var f = new StationSettingsFile();
            var s = new StationSettings { FuelMode = "Auto" };
            f.Set(1, 2, 3, "stashnetForge", s);
            f.Set(-4, 5, -6, "stashnetCampfire", new StationSettings { FuelMode = "resourceWood" });
            string text = f.Serialize().Replace("fuel resourceWood", "fuel resourceWood\nrule|garbage");

            StationSettingsFile back = StationSettingsFile.Parse(text);

            Assert.Equal("Auto", back.Get(1, 2, 3, "stashnetForge").FuelMode);
            Assert.True(back.Get(-4, 5, -6, "stashnetCampfire").IsDefault);
        }

        [Fact]
        public void File_DifferentBlockNameAtSamePos_GetsDefault()
        {
            var f = new StationSettingsFile();
            f.Set(1, 2, 3, "stashnetForge", new StationSettings { FuelMode = "Auto" });
            Assert.True(f.Get(1, 2, 3, "stashnetCampfire").IsDefault);
            Assert.True(StationSettingsFile.Parse(f.Serialize()).Get(1, 2, 3, "stashnetCampfire").IsDefault);
        }

        [Fact]
        public void File_SetDefault_RemovesEntry()
        {
            var f = new StationSettingsFile();
            f.Set(1, 2, 3, "stashnetForge", new StationSettings { FuelMode = "Auto" });
            f.Set(1, 2, 3, "stashnetForge", new StationSettings());
            Assert.Equal("", f.Serialize().Trim());
            f.Set(1, 2, 3, "stashnetForge", new StationSettings { FuelMode = "Auto" });
            Assert.True(f.Remove(1, 2, 3));
            Assert.False(f.Remove(1, 2, 3));
        }

        [Fact]
        public void File_GetReturnsCopy()
        {
            var f = new StationSettingsFile();
            f.Set(1, 2, 3, "stashnetForge", new StationSettings { FuelMode = "Auto" });
            f.Get(1, 2, 3, "stashnetForge").FuelMode = "Manual";
            Assert.Equal("Auto", f.Get(1, 2, 3, "stashnetForge").FuelMode);
        }

        [Fact]
        public void FuelModes_IsFuelItem()
        {
            Assert.True(FuelModes.IsFuelItem("coal"));
            Assert.False(FuelModes.IsFuelItem("Auto"));
            Assert.False(FuelModes.IsFuelItem("Manual"));
        }

    }

    public class StockMathTests
    {
        [Fact]
        public void CraftsNeeded()
        {
            Assert.Equal(19, StockMath.CraftsNeeded(500, 312, 10));
            Assert.Equal(0, StockMath.CraftsNeeded(500, 500, 10));
            Assert.Equal(0, StockMath.CraftsNeeded(500, 600, 1));
            Assert.Equal(0, StockMath.CraftsNeeded(500, 0, 0));
        }

        [Fact]
        public void MaxCraftsPerEntry_OneOutputStack()
        {
            Assert.Equal(1000, StockMath.MaxCraftsPerEntry(1, 1000));
            Assert.Equal(10, StockMath.MaxCraftsPerEntry(10, 100));
            Assert.Equal(1, StockMath.MaxCraftsPerEntry(5, 1)); // a non-stacking item: one craft per entry
            Assert.Equal(1, StockMath.MaxCraftsPerEntry(0, 0));
        }

        [Fact]
        public void CraftsAffordable()
        {
            Assert.Equal(4, StockMath.CraftsAffordable(new[] { (2, 9), (1, 100) }));
            Assert.Equal(0, StockMath.CraftsAffordable(new[] { (2, 1) }));
            Assert.Equal(0, StockMath.CraftsAffordable(new (int, int)[0]));
        }
    }
}
