namespace StashNet.Core.Stations
{
    /// <summary>
    /// When the Automation tab in the recipe box is open. At a Network station with no recipe selected it
    /// opens by itself; picking a recipe then hands back to vanilla's Ingredients. When the player opened
    /// it themselves it stays open across recipe changes (they pick the recipe to keep in stock while on it).
    /// Vanilla tab buttons close it, except with no recipe (they would have nothing to show).
    /// </summary>
    public sealed class AutomationTabState
    {
        private bool auto;

        public bool Open { get; private set; }

        /// <summary>Every frame: leaving the station closes; no recipe at a station opens (automatically).</summary>
        public void Update(bool atStation, bool hasRecipe)
        {
            if (!atStation)
            {
                Reset();
                return;
            }
            if (!hasRecipe && !Open)
            {
                Open = true;
                auto = true;
            }
        }

        public void OpenByPlayer(bool atStation)
        {
            if (!atStation) return;
            Open = true;
            auto = false;
        }

        /// <summary>A recipe was selected or cleared. Returns whether the tab is still open.</summary>
        public bool RecipeSelected(bool hasRecipe)
        {
            if (Open && auto && hasRecipe) Reset();
            return Open;
        }

        /// <summary>Ingredients / Description pressed. Returns true when the tab is closed afterwards.</summary>
        public bool VanillaTabPressed(bool hasRecipe)
        {
            if (Open && !hasRecipe) return false;
            Reset();
            return true;
        }

        public void Reset()
        {
            Open = false;
            auto = false;
        }
    }
}
