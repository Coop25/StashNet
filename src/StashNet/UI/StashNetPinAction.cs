namespace StashNet.UI
{
    /// <summary>
    /// "Pin" / "Unpin" button in the vanilla item info panel while a Storage Terminal cell is selected (2026-11-18).
    /// Pinned items are listed first and marked with a star; pins are this player's own (FavoritesStore).
    /// </summary>
    public class StashNetPinAction : BaseItemActionEntry
    {
        public StashNetPinAction(XUiController cell, bool pinned)
            : base(cell, pinned ? "stashnetUnpin" : "stashnetPin", "ui_game_symbol_star")
        {
        }

        public override void OnActivated()
        {
            XUiC_StashNetTerminal.Current?.TogglePinSelected();
        }
    }
}
