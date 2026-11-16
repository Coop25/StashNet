using System.Collections.Generic;

namespace StashNet.Core.Stations
{
    /// <summary>Our two tabs in the recipe box: Stock (keep in stock, keep melted) and Station (output, fire, refill).</summary>
    public enum AutomationTab
    {
        None,
        Stock,
        Station,
    }

    /// <summary>
    /// Which of our recipe-box tabs is open. At a Network station with no recipe selected one opens by itself:
    /// the tab last used at that kind of station (by block name, this game run), else Stock. Picking a recipe
    /// then hands back to vanilla's Ingredients. A tab the player opened stays open across recipe changes.
    /// Vanilla tab buttons close ours, except with no recipe (they would have nothing to show).
    /// </summary>
    public sealed class AutomationTabState
    {
        private readonly Dictionary<string, AutomationTab> lastUsed = new Dictionary<string, AutomationTab>();
        private bool auto;

        public AutomationTab Current { get; private set; }

        public bool Open => Current != AutomationTab.None;

        /// <summary>Every frame: leaving the station closes; no recipe at a station opens a tab (automatically).</summary>
        public void Update(bool atStation, bool hasRecipe, string station)
        {
            if (!atStation)
            {
                Reset();
                return;
            }
            if (!hasRecipe && !Open)
            {
                Current = station != null && lastUsed.TryGetValue(station, out AutomationTab t) ? t : AutomationTab.Stock;
                auto = true;
            }
        }

        public void OpenByPlayer(AutomationTab tab, bool atStation, string station)
        {
            if (!atStation || tab == AutomationTab.None) return;
            Current = tab;
            auto = false;
            if (station != null) lastUsed[station] = tab;
        }

        /// <summary>A recipe was selected or cleared. Returns whether one of our tabs is still open.</summary>
        public bool RecipeSelected(bool hasRecipe)
        {
            if (Open && auto && hasRecipe) Reset();
            return Open;
        }

        /// <summary>Ingredients / Description pressed. Returns true when our tab is closed afterwards.</summary>
        public bool VanillaTabPressed(bool hasRecipe)
        {
            if (Open && !hasRecipe) return false;
            Reset();
            return true;
        }

        /// <summary>Closes our tab (window closed). The last-used memory stays.</summary>
        public void Reset()
        {
            Current = AutomationTab.None;
            auto = false;
        }
    }
}
