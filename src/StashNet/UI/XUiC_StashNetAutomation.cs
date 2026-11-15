using System;
using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Protocol;
using StashNet.Core.Stations;
using StashNet.Crafting;
using StashNet.Stations;

namespace StashNet.UI
{
    /// <summary>
    /// The AUTOMATION panel under a Network station's output (Mod/Config/XUi_InGame/windows.xml,
    /// added to every station window group in xui.xml). Hidden at vanilla stations.
    /// Fuel button (fuel stations only): Manual → Auto → each fuel in storage → Manual.
    /// Keep in stock: the selected recipe + amount → Add/Update; one row per rule with a remove button.
    /// The server holds the settings; this panel shows what the last reply said.
    /// </summary>
    public class XUiC_StashNetAutomation : XUiController
    {
        private XUiC_Counter amount;
        private XUiC_StashNetRuleRow[] rows;
        private int shownVersion = -1;
        private bool shownActive;
        private Recipe shownRecipe;

        public override void Init()
        {
            base.Init();
            rows = GetChildrenByType<XUiC_StashNetRuleRow>();
            amount = GetChildById("amount") as XUiC_Counter;
            if (amount != null)
            {
                amount.MaxCount = StockRule.MaxTarget;
                amount.Step = 10;
                amount.Count = 100;
            }
            Hook("btnFuel", CycleFuel);
            Hook("btnAddRule", AddRule);
        }

        private void Hook(string id, Action action)
        {
            XUiController c = GetChildById(id);
            if (c != null) c.OnPress += (_sender, _button) => action();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            shownVersion = -1;
            amount?.ForceTextRefresh();
            Refresh();
        }

        public override void Update(float _dt)
        {
            base.Update(_dt);
            Recipe selected = SelectedRecipe();
            if (shownVersion == CraftingNetwork.Version && shownActive == CraftingNetwork.Active && shownRecipe == selected) return;
            Refresh();
        }

        private void Refresh()
        {
            shownVersion = CraftingNetwork.Version;
            shownActive = CraftingNetwork.Active;
            shownRecipe = SelectedRecipe();
            if (viewComponent != null) viewComponent.IsVisible = CraftingNetwork.Active;

            List<StockRule> rules = CraftingNetwork.Settings?.Rules ?? new List<StockRule>();
            for (int i = 0; i < rows.Length; i++)
            {
                StockRule r = i < rules.Count ? rules[i] : null;
                rows[i].Show(r, r == null ? 0 : Stored(r.Item));
            }
            RefreshBindingsSelfAndChildren(); // rows have their own bindings
        }

        private static int Stored(string item) => CraftingNetwork.Listing.Where(a => a.Name == item).Sum(a => a.Count);

        private Recipe SelectedRecipe() =>
            windowGroup?.Controller?.GetChildByType<XUiC_RecipeList>()?.SelectedEntry?.Recipe;

        private void CycleFuel()
        {
            if (!CraftingNetwork.Active || !CraftingNetwork.HasFuel) return;
            IEnumerable<string> fuels = CraftingNetwork.Listing
                .Where(a => a.Variant == null && IsFuel(a.Name))
                .Select(a => a.Name);
            string current = CraftingNetwork.Settings?.FuelMode ?? FuelModes.Manual;
            CraftingNetwork.SendFuelMode(FuelModes.Next(current, fuels));
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
                case "stashnet_hasfuel":
                    value = CraftingNetwork.Active && CraftingNetwork.HasFuel ? "true" : "false";
                    return true;
                case "stashnet_fuel":
                    value = string.Format(Localization.Get("stashnetFuelLabel"), FuelName(CraftingNetwork.Settings?.FuelMode ?? FuelModes.Manual));
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

        private static string FuelName(string mode)
        {
            if (mode == FuelModes.Manual) return Localization.Get("stashnetFuelManual");
            if (mode == FuelModes.Auto) return Localization.Get("stashnetFuelAuto");
            return ItemClass.GetItemClass(mode)?.GetLocalizedItemName() ?? mode;
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
                    value = rule == null ? "" : $"{ItemClass.GetItemClass(rule.Item)?.GetLocalizedItemName() ?? rule.Item}  {stored} / {rule.Target}";
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
