using StashNet.Core.Stations;
using Xunit;

namespace StashNet.Core.Tests
{
    public class AutomationTabStateTests
    {
        private const string Forge = "stashnetForge";

        [Fact]
        public void NoRecipeAtStationOpensStockTabFirstTime()
        {
            var t = new AutomationTabState();
            t.Update(atStation: true, hasRecipe: false, station: Forge);
            Assert.True(t.Open);
            Assert.Equal(AutomationTab.Stock, t.Current);
        }

        [Fact]
        public void NoRecipeOpensLastTabUsedAtThatKindOfStation()
        {
            var t = new AutomationTabState();
            t.OpenByPlayer(AutomationTab.Station, atStation: true, station: Forge);
            t.Reset(); // window closed
            t.Update(atStation: true, hasRecipe: false, station: Forge);
            Assert.Equal(AutomationTab.Station, t.Current);
            t.Reset();
            t.Update(atStation: true, hasRecipe: false, station: "stashnetCampfire");
            Assert.Equal(AutomationTab.Stock, t.Current);
        }

        [Fact]
        public void PlayerSwitchesBetweenOurTabs()
        {
            var t = new AutomationTabState();
            t.OpenByPlayer(AutomationTab.Stock, atStation: true, station: Forge);
            t.OpenByPlayer(AutomationTab.Station, atStation: true, station: Forge);
            Assert.Equal(AutomationTab.Station, t.Current);
        }

        [Fact]
        public void AutoOpenedTabStepsAsideWhenRecipePicked()
        {
            var t = new AutomationTabState();
            t.Update(atStation: true, hasRecipe: false, station: Forge);
            Assert.False(t.RecipeSelected(hasRecipe: true)); // vanilla Ingredients shows
            Assert.False(t.Open);
            Assert.Equal(AutomationTab.None, t.Current);
        }

        [Fact]
        public void PlayerOpenedTabStaysWhenRecipePicked()
        {
            var t = new AutomationTabState();
            t.OpenByPlayer(AutomationTab.Stock, atStation: true, station: Forge);
            Assert.True(t.RecipeSelected(hasRecipe: true));
            Assert.Equal(AutomationTab.Stock, t.Current);
        }

        [Fact]
        public void RecipeClearedReopensAutomatically()
        {
            var t = new AutomationTabState();
            t.Update(atStation: true, hasRecipe: false, station: Forge);
            t.RecipeSelected(hasRecipe: true);
            t.Update(atStation: true, hasRecipe: true, station: Forge);
            Assert.False(t.Open);
            t.RecipeSelected(hasRecipe: false);
            t.Update(atStation: true, hasRecipe: false, station: Forge);
            Assert.True(t.Open);
        }

        [Fact]
        public void VanillaButtonWithoutRecipeKeepsTab()
        {
            var t = new AutomationTabState();
            t.Update(atStation: true, hasRecipe: false, station: Forge);
            Assert.False(t.VanillaTabPressed(hasRecipe: false));
            Assert.True(t.Open);
            t.OpenByPlayer(AutomationTab.Station, atStation: true, station: Forge);
            Assert.True(t.VanillaTabPressed(hasRecipe: true));
            Assert.False(t.Open);
        }

        [Fact]
        public void LeavingStationCloses()
        {
            var t = new AutomationTabState();
            t.OpenByPlayer(AutomationTab.Stock, atStation: true, station: Forge);
            t.Update(atStation: false, hasRecipe: false, station: null);
            Assert.False(t.Open);
        }

        [Fact]
        public void NotAtStationNeverOpens()
        {
            var t = new AutomationTabState();
            t.OpenByPlayer(AutomationTab.Stock, atStation: false, station: null);
            t.Update(atStation: false, hasRecipe: false, station: null);
            Assert.False(t.Open);
        }
    }
}
