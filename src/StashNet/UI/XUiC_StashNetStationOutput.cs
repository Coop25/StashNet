using StashNet.Crafting;

namespace StashNet.UI
{
    /// <summary>
    /// The on/off switch in a workstation's OUTPUT header (Mod/Config/XUi_InGame/windows.xml). Shown
    /// only while a StashNet station or the Crafting Terminal is open; green = finished items go into
    /// storage, grey = they stay in the station (owner, 2026-11-14: on by default).
    /// </summary>
    public class XUiC_StashNetStationOutput : XUiController
    {
        private bool shownActive, shownOn;

        public override void Init()
        {
            base.Init();
            foreach (string id in new[] { "btnOutputOn", "btnOutputOff" })
            {
                XUiController button = GetChildById(id);
                if (button != null) button.OnPress += (_sender, _mouseButton) => CraftingNetwork.ToggleOutput();
            }
        }

        public override void Update(float _dt)
        {
            base.Update(_dt);
            if (shownActive == CraftingNetwork.Active && shownOn == CraftingNetwork.OutputOn) return;
            shownActive = CraftingNetwork.Active;
            shownOn = CraftingNetwork.OutputOn;
            RefreshBindings();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            RefreshBindings();
        }

        public override bool GetBindingValueInternal(ref string value, string bindingName)
        {
            switch (bindingName)
            {
                case "stashnet_outputon":
                    value = CraftingNetwork.Active && CraftingNetwork.OutputOn ? "true" : "false";
                    return true;
                case "stashnet_outputoff":
                    value = CraftingNetwork.Active && !CraftingNetwork.OutputOn ? "true" : "false";
                    return true;
                case "stashnet_outputtip":
                    value = Localization.Get(CraftingNetwork.OutputOn ? "stashnetOutputOnTip" : "stashnetOutputOffTip");
                    return true;
                default:
                    return base.GetBindingValueInternal(ref value, bindingName);
            }
        }
    }
}
