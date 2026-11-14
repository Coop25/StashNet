namespace StashNet.UI
{
    /// <summary>
    /// "Take" / "Take stack" buttons in the vanilla item info panel while a Storage Terminal cell is
    /// selected (v0.19.0). Take uses the amount in the terminal's footer box.
    /// </summary>
    public class StashNetTakeAction : BaseItemActionEntry
    {
        private readonly bool stack;

        public StashNetTakeAction(XUiController cell, bool stack)
            : base(cell, stack ? "stashnetTakeStack" : "lblContextActionTake", "ui_game_symbol_hand")
        {
            this.stack = stack;
        }

        public override void OnActivated()
        {
            XUiC_StashNetTerminal.Current?.TakeSelected(stack);
        }
    }
}
