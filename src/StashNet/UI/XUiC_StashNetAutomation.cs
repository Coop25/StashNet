using System;
using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Stations;
using StashNet.Crafting;
using StashNet.Stations;

namespace StashNet.UI
{
    /// <summary>
    /// The AUTOMATION tab in the recipe box (vanilla craftingInfoPanel, Mod/Config/XUi_InGame/windows.xml),
    /// next to Ingredients / Description. The tab button only shows at Network stations. While the tab is
    /// open (<see cref="TabOpen"/>), AutomationTabPatches hides the vanilla tab contents.
    /// Keep in stock: the selected recipe + amount → Keep/Update; one row per rule with a remove button.
    /// Fuel (fuel stations only): ◄ Manual / Auto / each fuel in storage ►, with what the mode does.
    /// The server holds the settings; this shows what the last reply said.
    /// </summary>
    public class XUiC_StashNetAutomation : XUiController
    {
        /// <summary>Our tab is showing (only ever true at a Network station).</summary>
        public static bool TabOpen { get; private set; }

        private XUiC_CraftingInfoWindow infoWindow;
        private XUiController tabButton;
        private XUiC_Counter amount;
        private XUiC_StashNetRuleRow[] rows = new XUiC_StashNetRuleRow[0];
        private bool broken;
        private int shownVersion = -1;
        private bool shownTab, shownStation;
        private Recipe shownRecipe;

        public override void Init()
        {
            // This lives inside the VANILLA recipe box, used by every crafting screen: an exception here
            // must never take that window down (v0.20.0 lesson). Lose the tab, never the crafting screen.
            try
            {
                base.Init();
                infoWindow = GetParentByType<XUiC_CraftingInfoWindow>();
                tabButton = GetChildById("btnAutomationTab");
                if (tabButton != null) tabButton.OnPress += (_s, _b) => OpenTab();
                // The vanilla tab buttons close ours. Looked up by id: the info window only fills its
                // button fields after its children (us) have run Init.
                foreach (string id in new[] { "ingredientsButton", "descriptionButton", "showunlocksButton" })
                {
                    XUiController b = infoWindow?.GetChildById(id);
                    if (b != null) b.OnPress += (_s, _b) => CloseTab();
                }
                rows = GetChildrenByType<XUiC_StashNetRuleRow>();
                amount = GetChildById("amount") as XUiC_Counter;
                if (amount != null)
                {
                    amount.MaxCount = StockRule.MaxTarget;
                    amount.Step = 10;
                    amount.Count = 100;
                }
                Hook("btnAddRule", AddRule);
                Hook("btnFuelPrev", () => CycleFuel(-1));
                Hook("btnFuelNext", () => CycleFuel(1));
            }
            catch (Exception e)
            {
                Log.Error($"[StashNet] automation tab failed to initialize, tab disabled: {e}");
                broken = true;
            }
        }

        private IEnumerable<XUiController> VanillaTabButtons()
        {
            if (infoWindow == null) yield break;
            if (infoWindow.ingredientsButton != null) yield return infoWindow.ingredientsButton;
            if (infoWindow.descriptionButton != null) yield return infoWindow.descriptionButton;
            if (infoWindow.unlockedByButton != null) yield return infoWindow.unlockedByButton;
        }

        private void Hook(string id, Action action)
        {
            XUiController c = GetChildById(id);
            if (c != null) c.OnPress += (_sender, _button) => action();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            if (TabOpen) RestoreVanillaSelection();
            TabOpen = false;
            shownVersion = -1;
            if (broken) return;
            amount?.ForceTextRefresh();
            Refresh();
        }

        public override void OnClose()
        {
            base.OnClose();
            if (TabOpen) RestoreVanillaSelection();
            TabOpen = false;
        }

        /// <summary>Re-highlight the vanilla tab that is really showing (we turned them all off).</summary>
        private void RestoreVanillaSelection()
        {
            SetSelected(tabButton, false);
            if (infoWindow?.ingredientsButton != null && infoWindow.descriptionButton != null && infoWindow.unlockedByButton != null)
                infoWindow.SetSelectedButtonByType(infoWindow.TabType);
        }

        private void OpenTab()
        {
            if (broken || !AtNetworkStation()) return;
            TabOpen = true;
            SelectTabButtons();
            Refresh();
        }

        private void CloseTab()
        {
            if (!TabOpen) return;
            TabOpen = false;
            SetSelected(tabButton, false);
            Refresh();
        }

        /// <summary>Ours selected, the vanilla ones not (vanilla re-selects Ingredients on every recipe change).</summary>
        public void SelectTabButtons()
        {
            foreach (XUiController b in VanillaTabButtons()) SetSelected(b, false);
            SetSelected(tabButton, true);
        }

        private static void SetSelected(XUiController button, bool selected)
        {
            if (button?.ViewComponent is XUiV_Button v) v.Selected = selected;
        }

        public override void Update(float _dt)
        {
            base.Update(_dt);
            if (broken) return;
            bool station = AtNetworkStation();
            if (!station && TabOpen)
            {
                RestoreVanillaSelection();
                CloseTab();
            }
            if (!station && !shownStation) return; // backpack crafting / vanilla stations: nothing to watch
            Recipe selected = SelectedRecipe();
            if (shownVersion == CraftingNetwork.Version && shownTab == TabOpen && shownStation == station && shownRecipe == selected) return;
            Refresh();
        }

        private void Refresh()
        {
            shownVersion = CraftingNetwork.Version;
            shownTab = TabOpen;
            shownStation = AtNetworkStation();
            shownRecipe = SelectedRecipe();

            List<StockRule> rules = CraftingNetwork.Settings?.Rules ?? new List<StockRule>();
            for (int i = 0; i < rows.Length; i++)
            {
                StockRule r = i < rules.Count ? rules[i] : null;
                rows[i].Show(r, r == null ? 0 : Stored(r.Item));
            }
            RefreshBindingsSelfAndChildren(); // rows have their own bindings
            if (infoWindow != null) infoWindow.IsDirty = true; // its showingredients/... bindings follow TabOpen
        }

        /// <summary>The recipe box belongs to an open Network station (not backpack crafting, not a vanilla station).</summary>
        private bool AtNetworkStation()
        {
            if (!CraftingNetwork.Active) return false;
            TileEntityWorkstation te = (windowGroup?.Controller as XUiC_WorkstationWindowGroup)?.WorkstationData?.TileEntity;
            if (te == null) return false;
            return CraftingNetwork.IsCraftingTerminal(GameManager.Instance.World?.GetBlock(te.ToWorldPos()).Block);
        }

        private static int Stored(string item) => CraftingNetwork.Listing.Where(a => a.Name == item).Sum(a => a.Count);

        private Recipe SelectedRecipe() =>
            windowGroup?.Controller?.GetChildByType<XUiC_RecipeList>()?.SelectedEntry?.Recipe;

        private void CycleFuel(int direction)
        {
            if (!CraftingNetwork.Active || !CraftingNetwork.HasFuel) return;
            List<string> fuels = CraftingNetwork.Listing
                .Where(a => a.Variant == null && IsFuel(a.Name))
                .Select(a => a.Name)
                .ToList();
            string current = CraftingNetwork.Settings?.FuelMode ?? FuelModes.Manual;
            CraftingNetwork.SendFuelMode(direction < 0 ? FuelModes.Previous(current, fuels) : FuelModes.Next(current, fuels));
        }

        private static bool IsFuel(string name)
        {
            ItemValue iv = ItemClass.GetItem(name);
            return iv != null && !iv.IsEmpty() && ItemClass.GetFuelValue(iv) > 0;
        }

        private void AddRule()
        {
            Recipe recipe = SelectedRecipe();
            EntityPlayerLocal player = xui?.playerUI?.entityPlayer;
            if (recipe == null)
            {
                if (player != null) GameManager.ShowTooltip(player, Localization.Get("stashnetPickRecipe"), string.Empty, "ui_denied");
                return;
            }
            int target = Math.Max(1, Math.Min(StockRule.MaxTarget, amount?.Count ?? 100));
            StockRule rule = StockRuleCapture.FromRecipe(xui, recipe, target);
            if (rule == null)
            {
                if (player != null) GameManager.ShowTooltip(player, Localization.Get("stashnetRuleCantAuto"), string.Empty, "ui_denied");
                return;
            }
            CraftingNetwork.SendStockRule(rule);
        }

        public override bool GetBindingValueInternal(ref string value, string bindingName)
        {
            switch (bindingName)
            {
                case "stashnet_atstation":
                    value = !broken && AtNetworkStation() ? "true" : "false";
                    return true;
                case "stashnet_tabopen":
                    value = TabOpen ? "true" : "false";
                    return true;
                case "stashnet_hasfuel":
                    value = CraftingNetwork.Active && CraftingNetwork.HasFuel ? "true" : "false";
                    return true;
                case "stashnet_fuelmode":
                    value = FuelName(CurrentFuelMode);
                    return true;
                case "stashnet_fueltip":
                    value = FuelTip(CurrentFuelMode);
                    return true;
                case "stashnet_keeplabel":
                    value = shownRecipe == null
                        ? Localization.Get("stashnetPickRecipe")
                        : string.Format(Localization.Get("stashnetKeepInStock"), shownRecipe.GetOutputItemClass()?.GetLocalizedItemName() ?? shownRecipe.GetName());
                    return true;
                case "stashnet_addlabel":
                    bool exists = shownRecipe != null && CraftingNetwork.Settings != null
                        && CraftingNetwork.Settings.Rules.Any(r => r.Item == shownRecipe.GetOutputItemClass()?.GetItemName());
                    value = Localization.Get(exists ? "stashnetUpdateRule" : "stashnetAddRule");
                    return true;
                case "stashnet_norules":
                    value = (CraftingNetwork.Settings?.Rules.Count ?? 0) == 0 ? "true" : "false";
                    return true;
                default:
                    return base.GetBindingValueInternal(ref value, bindingName);
            }
        }

        private static string CurrentFuelMode => CraftingNetwork.Settings?.FuelMode ?? FuelModes.Manual;

        private static string FuelName(string mode)
        {
            if (mode == FuelModes.Manual) return Localization.Get("stashnetFuelManual");
            if (mode == FuelModes.Auto) return Localization.Get("stashnetFuelAuto");
            return ItemClass.GetItemClass(mode)?.GetLocalizedItemName() ?? mode;
        }

        private string FuelTip(string mode)
        {
            if (mode == FuelModes.Manual) return string.Format(Localization.Get("stashnetFuelManualTip"), VanillaStationName());
            if (mode == FuelModes.Auto) return Localization.Get("stashnetFuelAutoTip");
            return string.Format(Localization.Get("stashnetFuelItemTip"), FuelName(mode));
        }

        /// <summary>"Forge", "Campfire"...: the vanilla station this one copies (StashNetCategories), for "like a normal forge".</summary>
        private string VanillaStationName()
        {
            TileEntityWorkstation te = (windowGroup?.Controller as XUiC_WorkstationWindowGroup)?.WorkstationData?.TileEntity;
            Block block = te == null ? null : GameManager.Instance.World?.GetBlock(te.ToWorldPos()).Block;
            string vanilla = block != null && block.Properties.Contains(StationCategoryPatch.Property)
                ? block.Properties.GetString(StationCategoryPatch.Property)
                : null;
            return vanilla == null ? "" : Localization.Get(vanilla).ToLowerInvariant();
        }
    }

    /// <summary>One stock rule row: "Concrete Mix  312 / 500" and a remove button.</summary>
    public class XUiC_StashNetRuleRow : XUiController
    {
        private StockRule rule;
        private int stored;

        public override void Init()
        {
            base.Init();
            XUiController remove = GetChildById("btnRemove");
            if (remove != null) remove.OnPress += (_sender, _button) => { if (rule != null) CraftingNetwork.SendRemoveRule(rule.Item); };
        }

        public void Show(StockRule r, int storedNow)
        {
            rule = r;
            stored = storedNow;
        }

        public override bool GetBindingValueInternal(ref string value, string bindingName)
        {
            switch (bindingName)
            {
                case "stashnet_hasrule":
                    value = rule != null ? "true" : "false";
                    return true;
                case "stashnet_rule":
                    value = rule == null ? "" : $"{ItemClass.GetItemClass(rule.Item)?.GetLocalizedItemName() ?? rule.Item}   {stored} / {rule.Target}";
                    return true;
                case "stashnet_rulecolor":
                    value = rule != null && stored >= rule.Target ? "150,220,150,255" : "210,210,210,255"; // green once stocked
                    return true;
                default:
                    return base.GetBindingValueInternal(ref value, bindingName);
            }
        }
    }
}
