using System.Collections.Generic;
using StashNet.Core.Stations;
using Xunit;

namespace StashNet.Core.Tests
{
    public class RulePlausibilityTests
    {
        private static readonly List<(string, int)> Base = new List<(string, int)> { ("resourceForgedIron", 4), ("resourceLeather", 2) };

        private static StockRule Rule(int output, params (string, int)[] ings) =>
            new StockRule { Item = "x", Recipe = "x", OutputCount = output, Ingredients = new List<(string Name, int Amount)>(ings) };

        [Fact]
        public void SameAsTheRecipeIsFine() =>
            Assert.True(RulePlausibility.Plausible(1, Base, Rule(1, ("resourceForgedIron", 4), ("resourceLeather", 2))));

        [Fact]
        public void PerkReducedAmountsAreFine() =>
            Assert.True(RulePlausibility.Plausible(1, Base, Rule(2, ("resourceForgedIron", 2), ("resourceLeather", 1))));

        [Fact]
        public void IngredientOrderDoesNotMatter() =>
            Assert.True(RulePlausibility.Plausible(1, Base, Rule(1, ("resourceLeather", 2), ("resourceForgedIron", 4))));

        [Fact]
        public void TooCheapIsRefused() =>
            Assert.False(RulePlausibility.Plausible(10, new List<(string, int)> { ("resourceForgedIron", 40) }, Rule(10, ("resourceForgedIron", 1))));

        [Fact]
        public void TooMuchOutputIsRefused() =>
            Assert.False(RulePlausibility.Plausible(1, Base, Rule(10000, ("resourceForgedIron", 4), ("resourceLeather", 2))));

        [Fact]
        public void ExtraOrWrongIngredientIsRefused()
        {
            Assert.False(RulePlausibility.Plausible(1, Base, Rule(1, ("resourceForgedIron", 4), ("resourceLeather", 2), ("resourceWood", 1))));
            Assert.False(RulePlausibility.Plausible(1, Base, Rule(1, ("resourceForgedIron", 4), ("resourceWood", 2))));
        }

        [Fact]
        public void IngredientAPerkReducedToZeroMayBeMissing() =>
            Assert.True(RulePlausibility.Plausible(1, Base, Rule(1, ("resourceForgedIron", 4))));

        [Fact]
        public void NoIngredientsIsRefused() => Assert.False(RulePlausibility.Plausible(1, Base, Rule(1)));

        [Fact]
        public void ZeroOutputIsRefused() =>
            Assert.False(RulePlausibility.Plausible(1, Base, Rule(0, ("resourceForgedIron", 4), ("resourceLeather", 2))));
    }
}
