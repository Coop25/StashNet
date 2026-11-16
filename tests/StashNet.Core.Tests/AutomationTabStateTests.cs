using StashNet.Core.Stations;
using Xunit;

namespace StashNet.Core.Tests
{
    public class AutomationTabStateTests
    {
        [Fact]
        public void NoRecipeAtStationOpensTab()
        {
            var t = new AutomationTabState();
            t.Update(atStation: true, hasRecipe: false);
            Assert.True(t.Open);
        }

        [Fact]
        public void AutoOpenedTabStepsAsideWhenRecipePicked()
        {
            var t = new AutomationTabState();
            t.Update(atStation: true, hasRecipe: false);
            Assert.False(t.RecipeSelected(hasRecipe: true)); // vanilla Ingredients shows
            Assert.False(t.Open);
        }

        [Fact]
        public void PlayerOpenedTabStaysWhenRecipePicked()
        {
            var t = new AutomationTabState();
            t.OpenByPlayer(atStation: true);
            Assert.True(t.RecipeSelected(hasRecipe: true));
            Assert.True(t.Open);
        }

        [Fact]
        public void RecipeClearedReopensAutomatically()
        {
            var t = new AutomationTabState();
            t.Update(atStation: true, hasRecipe: false);
            t.RecipeSelected(hasRecipe: true);
            t.RecipeSelected(hasRecipe: false);
            t.Update(atStation: true, hasRecipe: false);
            Assert.True(t.Open);
        }

        [Fact]
        public void VanillaButtonWithoutRecipeKeepsTab()
        {
            var t = new AutomationTabState();
            t.Update(atStation: true, hasRecipe: false);
            Assert.False(t.VanillaTabPressed(hasRecipe: false));
            Assert.True(t.Open);
            t.OpenByPlayer(atStation: true);
            Assert.True(t.VanillaTabPressed(hasRecipe: true));
            Assert.False(t.Open);
        }

        [Fact]
        public void LeavingStationCloses()
        {
            var t = new AutomationTabState();
            t.OpenByPlayer(atStation: true);
            t.Update(atStation: false, hasRecipe: false);
            Assert.False(t.Open);
        }

        [Fact]
        public void NotAtStationNeverOpens()
        {
            var t = new AutomationTabState();
            t.OpenByPlayer(atStation: false);
            t.Update(atStation: false, hasRecipe: false);
            Assert.False(t.Open);
        }
    }
}
