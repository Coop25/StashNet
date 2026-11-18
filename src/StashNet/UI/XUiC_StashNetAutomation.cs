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
    /// next to Ingredients / Description. Only at Network stations, where the recipe box also shows with no
    /// recipe selected (AutomationTabPatches), and then this tab is open. While the tab is open
    /// (<see cref="TabOpen"/>), AutomationTabPatches hides the vanilla tab contents.
    /// Rows: keep the selected recipe in stock; keep a metal melted (forge); the list of both (6 per page);
    /// Fire Myself / Auto and Refill none / a stored fuel (fuel stations); Finished items to storage / stay here.
    /// The server holds the settings; this shows what the last reply said.
    /// </summary>
    public class XUiC_StashNetAutomation : XUiController
    {
        /// <summary>Our tab is showing (only ever true at a Network station).</summary>
        public static bool TabOpen => State.Open;

        private static readonly AutomationTabState State = new AutomationTabState();

        private const int RowsPerPage = 10;
        private const float LiveRefreshSeconds = 1f;
        private const string ChosenColor = "70,105,70,255", OtherColor = "48,48,48,255";

        private XUiC_CraftingInfoWindow infoWindow;
        private XUiController tabButton, stationTabButton;
        private XUiC_Counter amount, meltAmount;
        private XUiC_StashNetRuleRow[] rows = new XUiC_StashNetRuleRow[0];
        private XUiC_StashNetFuelChoice[] fuelCells = new XUiC_StashNetFuelChoice[0];
        private bool broken;
        private int shownVersion = -1;
        private bool shownTab, shownStation;
        private Recipe shownRecipe;
        private int page, metalIndex;
        private float liveTimer;

        public override void Init()
        {
            // This lives inside the VANILLA recipe box, used by every crafting screen: an exception here
            // must never take that window down (v0.20.0 lesson). Lose the tab, never the crafting screen.
            try
            {
                base.Init();
                infoWindow = GetParentByType<XUiC_CraftingInfoWindow>();
                tabButton = GetChildById("btnAutomationTab");
                if (tabButton != null) tabButton.OnPress += (_s, _b) => OpenTab(AutomationTab.Stock);
                stationTabButton = GetChildById("btnStationTab");
                if (stationTabButton != null) stationTabButton.OnPress += (_s, _b) => OpenTab(AutomationTab.Station);
                // The vanilla tab buttons close ours. Looked up by id: the info window only fills its
                // button fields after its children (us) have run Init.
                foreach (string id in new[] { "ingredientsButton", "descriptionButton", "showunlocksButton" })
                {
                    XUiController b = infoWindow?.GetChildById(id);
                    if (b != null) b.OnPress += (_s, _b) => CloseTab();
                }
                rows = GetChildrenByType<XUiC_StashNetRuleRow>();
                fuelCells = GetChildrenByType<XUiC_StashNetFuelChoice>();
                foreach (XUiC_StashNetFuelChoice cell in fuelCells) cell.Picked += name => CraftingNetwork.SendFuelMode(name);

                amount = GetChildById("amount") as XUiC_Counter;
                if (amount != null)
                {
                    amount.MaxCount = StockRule.MaxTarget;
                    amount.Step = 10;
                    amount.Count = 100;
                }
                meltAmount = GetChildById("meltAmount") as XUiC_Counter;
                if (meltAmount != null)
                {
                    meltAmount.MaxCount = 30000;
                    meltAmount.Step = 100;
                    meltAmount.Count = 1000;
                }
                Hook("btnAddRule", AddRule);
                Hook("btnAddMelt", AddMelt);
                Hook("btnMetalPrev", () => StepMetal(-1));
                Hook("btnMetalNext", () => StepMetal(1));
                Hook("btnPagePrev", () => StepPage(-1));
                Hook("btnPageNext", () => StepPage(1));
                Hook("btnFireMyself", () => CraftingNetwork.SendFuelMode(FuelModes.Manual));
                Hook("btnFireAuto", () => { if (CurrentFuelMode == FuelModes.Manual) CraftingNetwork.SendFuelMode(FuelModes.Auto); });
                Hook("btnRefillNone", () => CraftingNetwork.SendFuelMode(FuelModes.Auto));
                Hook("btnOutputOn", () => CraftingNetwork.SendOutput(true));
                Hook("btnOutputOff", () => CraftingNetwork.SendOutput(false));
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
            State.Reset();
            shownVersion = -1;
            page = 0;
            metalIndex = 0;
            if (broken) return;
            amount?.ForceTextRefresh();
            meltAmount?.ForceTextRefresh();
            Refresh();
        }

        public override void OnClose()
        {
            base.OnClose();
            if (TabOpen) RestoreVanillaSelection();
            State.Reset();
        }

        /// <summary>Re-highlight the vanilla tab that is really showing (we turned them all off).</summary>
        private void RestoreVanillaSelection()
        {
            SetOurTabsSelected(false);
            if (infoWindow?.ingredientsButton != null && infoWindow.descriptionButton != null && infoWindow.unlockedByButton != null)
                infoWindow.SetSelectedButtonByType(infoWindow.TabType);
        }

        private void OpenTab(AutomationTab tab)
        {
            if (broken) return;
            State.OpenByPlayer(tab, AtNetworkStation(), StationKey());
            if (TabOpen) SelectTabButtons();
            Refresh();
        }

        /// <summary>A vanilla tab button was pressed. With no recipe ours stays (theirs have nothing to show).</summary>
        private void CloseTab()
        {
            if (!TabOpen) return;
            if (!State.VanillaTabPressed(SelectedRecipe() != null))
            {
                SelectTabButtons();
                return;
            }
            SetOurTabsSelected(false);
            Refresh();
        }

        /// <summary>
        /// Vanilla selected or cleared a recipe (SetRecipe postfix; vanilla has just re-selected Ingredients).
        /// A tab that opened by itself hands back to Ingredients; one the player opened stays.
        /// </summary>
        public void RecipeChanged(bool hasRecipe)
        {
            if (State.RecipeSelected(hasRecipe)) SelectTabButtons();
            else SetOurTabsSelected(false);
            Refresh();
        }

        /// <summary>Ours selected, the vanilla ones not (vanilla re-selects Ingredients on every recipe change).</summary>
        public void SelectTabButtons()
        {
            foreach (XUiController b in VanillaTabButtons()) SetSelected(b, false);
            SetSelected(tabButton, State.Current == AutomationTab.Stock);
            SetSelected(stationTabButton, State.Current == AutomationTab.Station);
        }

        private void SetOurTabsSelected(bool selected)
        {
            SetSelected(tabButton, selected && State.Current == AutomationTab.Stock);
            SetSelected(stationTabButton, selected && State.Current == AutomationTab.Station);
        }

        /// <summary>The open station's block name: the last-used tab is remembered per kind of station.</summary>
        private string StationKey()
        {
            TileEntityWorkstation te = OpenStation();
            return te == null ? null : GameManager.Instance.World?.GetBlock(te.ToWorldPos()).Block?.GetBlockName();
        }

        private static void SetSelected(XUiController button, bool selected)
        {
            if (button?.ViewComponent is XUiV_Button v) v.Selected = selected;
        }

        /// <summary>
        /// At a Network station with no recipe selected, the recipe box takes the empty info panel's place
        /// so the tab can be used (AutomationTabPatches). The item info panel (a clicked backpack item)
        /// still wins, as in vanilla.
        /// </summary>
        public static bool OwnsEmptySlot(XUiC_CraftingInfoWindow window)
        {
            if (window?.ViewComponent == null || window.windowGroup == null || !window.windowGroup.isShowing) return false;
            XUiC_StashNetAutomation tab = window.GetChildByType<XUiC_StashNetAutomation>();
            return tab != null && !tab.broken && tab.AtNetworkStation() && tab.SelectedRecipe() == null;
        }

        /// <summary>After vanilla's Update: show the recipe box if the empty panel (or nothing) is in its place.</summary>
        public static void KeepRecipeBoxShown(XUiC_CraftingInfoWindow window)
        {
            if (!OwnsEmptySlot(window)) return;
            bool itemShown = window.itemInfoWindow?.ViewComponent?.IsVisible == true;
            if (!window.ViewComponent.IsVisible && !itemShown) window.ViewComponent.IsVisible = true;
        }

        public override void Update(float _dt)
        {
            base.Update(_dt);
            if (broken) return;
            bool station = AtNetworkStation();
            if (!station && !shownStation && !TabOpen) return; // backpack crafting / vanilla stations: nothing to watch
            Recipe selected = SelectedRecipe();
            bool was = TabOpen;
            State.Update(station, selected != null, StationKey());
            if (was && !TabOpen) RestoreVanillaSelection();
            if (!was && TabOpen) SelectTabButtons();
            liveTimer += _dt;
            bool live = TabOpen && liveTimer >= LiveRefreshSeconds; // melted tallies change while open
            if (!live && shownVersion == CraftingNetwork.Version && shownTab == TabOpen && shownStation == station && shownRecipe == selected) return;
            Refresh();
        }

        private void Refresh()
        {
            liveTimer = 0f;
            shownVersion = CraftingNetwork.Version;
            bool tabChanged = shownTab != TabOpen;
            shownTab = TabOpen;
            shownStation = AtNetworkStation();
            shownRecipe = SelectedRecipe();

            List<Entry> entries = Entries();
            int pages = Math.Max(1, (entries.Count + RowsPerPage - 1) / RowsPerPage);
            page = Math.Max(0, Math.Min(page, pages - 1));
            for (int i = 0; i < rows.Length; i++)
            {
                int k = page * RowsPerPage + i;
                Entry e = k < entries.Count ? entries[k] : null;
                rows[i].Show(e?.Text, e != null && e.Done, e?.Remove);
            }

            List<(string Name, int Count)> fuels = FuelChoices();
            for (int i = 0; i < fuelCells.Length; i++)
            {
                if (i < fuels.Count) fuelCells[i].Show(fuels[i].Name, fuels[i].Count, fuels[i].Name == CurrentFuelMode, RefillAmount());
                else fuelCells[i].Show(null, 0, false, 0);
            }
            RefreshBindingsSelfAndChildren(); // rows and cells have their own bindings
            // Its showingredients/... bindings follow TabOpen. Only when that changed: dirtying the recipe box
            // makes vanilla re-run its whole Update (and re-show the empty panel).
            if (infoWindow != null && tabChanged) infoWindow.IsDirty = true;
        }

        private sealed class Entry
        {
            public string Text;
            public bool Done;
            public Action Remove;
        }

        /// <summary>Stock rules, then melt targets.</summary>
        private List<Entry> Entries()
        {
            var list = new List<Entry>();
            StationSettings s = CraftingNetwork.Settings;
            if (s == null) return list;
            foreach (StockRule r in s.Rules)
            {
                int stored = Stored(r.Item);
                string item = r.Item;
                list.Add(new Entry
                {
                    Text = $"{ItemClass.GetItemClass(r.Item)?.GetLocalizedItemName() ?? r.Item}   {stored} / {r.Target}",
                    Done = stored >= r.Target,
                    Remove = () => CraftingNetwork.SendRemoveRule(item),
                });
            }
            foreach (MeltTarget m in s.Melts)
            {
                int tally = Tally(m.Metal);
                string metal = m.Metal;
                list.Add(new Entry
                {
                    Text = $"{string.Format(Localization.Get("stashnetMeltedRow"), MetalName(m.Metal))}   {tally} / {m.Target}",
                    Done = tally >= m.Target,
                    Remove = () => CraftingNetwork.SendRemoveMelt(metal),
                });
            }
            return list;
        }

        private void StepPage(int by)
        {
            page += by;
            Refresh();
        }

        /// <summary>The open station's workstation tile entity (client copy), or null.</summary>
        private TileEntityWorkstation OpenStation() =>
            (windowGroup?.Controller as XUiC_WorkstationWindowGroup)?.WorkstationData?.TileEntity;

        /// <summary>The recipe box belongs to an open Network station (not backpack crafting, not a vanilla station).</summary>
        private bool AtNetworkStation()
        {
            if (!CraftingNetwork.Active) return false;
            TileEntityWorkstation te = OpenStation();
            if (te == null) return false;
            return CraftingNetwork.IsCraftingTerminal(GameManager.Instance.World?.GetBlock(te.ToWorldPos()).Block);
        }

        private static int Stored(string item) => CraftingNetwork.Listing.Where(a => a.Name == item).Sum(a => a.Count);

        private Recipe SelectedRecipe() =>
            windowGroup?.Controller?.GetChildByType<XUiC_RecipeList>()?.SelectedEntry?.Recipe;

        // ------------------------------------------------------------ melt targets

        /// <summary>Metals the open station melts (its InputMaterials), empty when it doesn't smelt.</summary>
        private string[] Metals()
        {
            TileEntityWorkstation te = OpenStation();
            return te == null ? new string[0] : StationRules.MeltMetals(GameManager.Instance.World?.GetBlock(te.ToWorldPos()).Block);
        }

        private string CurrentMetal()
        {
            string[] metals = Metals();
            if (metals.Length == 0) return null;
            metalIndex = ((metalIndex % metals.Length) + metals.Length) % metals.Length;
            return metals[metalIndex];
        }

        private void StepMetal(int by)
        {
            metalIndex += by;
            string metal = CurrentMetal();
            MeltTarget existing = CraftingNetwork.Settings?.Melts.FirstOrDefault(m => m.Metal == metal);
            if (existing != null && meltAmount != null) meltAmount.Count = existing.Target;
            Refresh();
        }

        /// <summary>The forge window's own name for a metal ("lbl" + metal, e.g. glass = "Sand"), like vanilla XUiC_WorkstationMaterialInputWindow.</summary>
        private static string MetalName(string metal) =>
            Localization.Exists("lbl" + metal) ? Localization.Get("lbl" + metal) : (metal.Length > 0 ? char.ToUpperInvariant(metal[0]) + metal.Substring(1) : metal);

        /// <summary>The melted tally shown in the open forge's window (approximate; the window melts while open).</summary>
        private int Tally(string metal)
        {
            TileEntityWorkstation te = OpenStation();
            ItemStack[] slots = (windowGroup?.Controller as XUiC_WorkstationWindowGroup)?.inputWindow?.GetSlots() ?? te?.input;
            if (te?.materialNames == null || slots == null) return 0;
            int j = Array.FindIndex(te.materialNames, m => m.Equals(metal, StringComparison.OrdinalIgnoreCase));
            int slot = slots.Length - te.materialNames.Length + j;
            return j < 0 || slot < 0 || slot >= slots.Length || slots[slot] == null ? 0 : slots[slot].count;
        }

        private void AddMelt()
        {
            string metal = CurrentMetal();
            if (metal == null) return;
            int target = Math.Max(1, Math.Min(MeltTarget.MaxTarget, meltAmount?.Count ?? 1000));
            CraftingNetwork.SendMeltTarget(new MeltTarget { Metal = metal, Target = target });
        }

        // ------------------------------------------------------------ fuel

        private static string CurrentFuelMode => CraftingNetwork.Settings?.FuelMode ?? FuelModes.Manual;

        /// <summary>Up to 7 fuels: the chosen one first (even when none is stored), then the most stored.</summary>
        private List<(string Name, int Count)> FuelChoices()
        {
            var fuels = CraftingNetwork.Listing
                .Where(a => a.Variant == null && IsFuel(a.Name))
                .GroupBy(a => a.Name)
                .Select(g => (Name: g.Key, Count: g.Sum(a => a.Count)))
                .OrderByDescending(f => f.Count).ThenBy(f => f.Name, StringComparer.Ordinal)
                .ToList();
            string chosen = CurrentFuelMode;
            if (FuelModes.IsFuelItem(chosen))
            {
                int i = fuels.FindIndex(f => f.Name == chosen);
                var c = i >= 0 ? fuels[i] : (Name: chosen, Count: 0);
                if (i >= 0) fuels.RemoveAt(i);
                fuels.Insert(0, c);
            }
            return fuels.Take(fuelCells.Length).ToList();
        }

        private static bool IsFuel(string name)
        {
            ItemValue iv = ItemClass.GetItem(name);
            return iv != null && !iv.IsEmpty() && ItemClass.GetFuelValue(iv) > 0;
        }

        // ------------------------------------------------------------ keep in stock

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
            StationSettings s = CraftingNetwork.Settings;
            switch (bindingName)
            {
                case "stashnet_atstation":
                    value = !broken && AtNetworkStation() ? "true" : "false";
                    return true;
                case "stashnet_tabopen":
                    value = TabOpen ? "true" : "false";
                    return true;
                case "stashnet_stocktab":
                    value = State.Current == AutomationTab.Stock ? "true" : "false";
                    return true;
                case "stashnet_stationtab":
                    value = State.Current == AutomationTab.Station ? "true" : "false";
                    return true;
                case "stashnet_hasrecipe":
                    value = shownRecipe != null ? "true" : "false";
                    return true;
                case "stashnet_keeplabel":
                    value = shownRecipe == null
                        ? Localization.Get("stashnetPickRecipe")
                        : string.Format(Localization.Get("stashnetKeepInStock"), shownRecipe.GetOutputItemClass()?.GetLocalizedItemName() ?? shownRecipe.GetName());
                    return true;
                case "stashnet_addlabel":
                    bool exists = shownRecipe != null && s != null && s.Rules.Any(r => r.Item == shownRecipe.GetOutputItemClass()?.GetItemName());
                    value = Localization.Get(exists ? "stashnetUpdateRule" : "stashnetAddRule");
                    return true;
                case "stashnet_hasmelt":
                    value = !broken && AtNetworkStation() && !XUiM_Recipes.DisableSmelter && Metals().Length > 0 ? "true" : "false"; // the server ignores melt targets with Disable Smelter on
                    return true;
                case "stashnet_metal":
                    string metal = CurrentMetal();
                    value = metal == null ? "" : MetalName(metal);
                    return true;
                case "stashnet_meltaddlabel":
                    string cur = CurrentMetal();
                    value = Localization.Get(s != null && s.Melts.Any(m => m.Metal == cur) ? "stashnetUpdateRule" : "stashnetAddRule");
                    return true;
                case "stashnet_paging":
                    value = EntryCount() > RowsPerPage ? "true" : "false";
                    return true;
                case "stashnet_page":
                    value = $"{page + 1}/{Math.Max(1, (EntryCount() + RowsPerPage - 1) / RowsPerPage)}";
                    return true;
                case "stashnet_norules":
                    value = EntryCount() == 0 ? "true" : "false";
                    return true;
                case "stashnet_hasfuel":
                    value = CraftingNetwork.Active && CraftingNetwork.HasFuel ? "true" : "false";
                    return true;
                case "stashnet_showrefill":
                    value = CraftingNetwork.Active && CraftingNetwork.HasFuel && CurrentFuelMode != FuelModes.Manual ? "true" : "false";
                    return true;
                case "stashnet_sel_myself":
                    value = s != null && CurrentFuelMode == FuelModes.Manual ? ChosenColor : OtherColor;
                    return true;
                case "stashnet_sel_auto":
                    value = s != null && CurrentFuelMode != FuelModes.Manual ? ChosenColor : OtherColor;
                    return true;
                case "stashnet_sel_none":
                    value = CurrentFuelMode == FuelModes.Auto ? ChosenColor : OtherColor;
                    return true;
                case "stashnet_firetip":
                    string mode = CurrentFuelMode;
                    if (mode == FuelModes.Manual) value = string.Format(Localization.Get("stashnetFuelManualTip"), VanillaStationName());
                    else if (mode == FuelModes.Auto) value = Localization.Get("stashnetFuelAutoTip");
                    else value = string.Format(Localization.Get("stashnetFuelItemTip"), ItemClass.GetItemClass(mode)?.GetLocalizedItemName() ?? mode, RefillAmount());
                    return true;
                case "stashnet_outputtip":
                    value = Localization.Get(s != null && !s.OutputOn ? "stashnetOutputOffTip" : "stashnetOutputOnTip");
                    return true;
                case "stashnet_sel_outon":
                    value = s != null && s.OutputOn ? ChosenColor : OtherColor; // nothing lit until the server has answered
                    return true;
                case "stashnet_sel_outoff":
                    value = s != null && !s.OutputOn ? ChosenColor : OtherColor;
                    return true;
                default:
                    return base.GetBindingValueInternal(ref value, bindingName);
            }
        }

        private int RefillAmount()
        {
            TileEntityWorkstation te = OpenStation();
            return StationRules.FuelRefillAmount(te == null ? null : GameManager.Instance.World?.GetBlock(te.ToWorldPos()).Block);
        }

        private static int EntryCount() => (CraftingNetwork.Settings?.Rules.Count ?? 0) + (CraftingNetwork.Settings?.Melts.Count ?? 0);

        /// <summary>"Forge", "Campfire"...: the vanilla station this one copies (StashNetCategories), for "like a normal forge".</summary>
        private string VanillaStationName()
        {
            TileEntityWorkstation te = OpenStation();
            Block block = te == null ? null : GameManager.Instance.World?.GetBlock(te.ToWorldPos()).Block;
            string vanilla = block != null && block.Properties.Contains(StationCategoryPatch.Property)
                ? block.Properties.GetString(StationCategoryPatch.Property)
                : null;
            return vanilla == null ? "" : Localization.Get(vanilla).ToLowerInvariant();
        }
    }

    /// <summary>One list row: a stock rule ("Concrete Mix  312 / 500") or a melt target, and a remove button.</summary>
    public class XUiC_StashNetRuleRow : XUiController
    {
        private string text;
        private bool done;
        private Action remove;

        public override void Init()
        {
            base.Init();
            XUiController button = GetChildById("btnRemove");
            if (button != null) button.OnPress += (_sender, _button) => remove?.Invoke();
        }

        public void Show(string rowText, bool reached, Action onRemove)
        {
            text = rowText;
            done = reached;
            remove = onRemove;
        }

        public override bool GetBindingValueInternal(ref string value, string bindingName)
        {
            switch (bindingName)
            {
                case "stashnet_hasrule":
                    value = text != null ? "true" : "false";
                    return true;
                case "stashnet_rule":
                    value = text ?? "";
                    return true;
                case "stashnet_rulecolor":
                    value = text != null && done ? "150,220,150,255" : "210,210,210,255"; // green once reached
                    return true;
                default:
                    return base.GetBindingValueInternal(ref value, bindingName);
            }
        }
    }

    /// <summary>One fuel in the Refill row: icon, stored count, highlighted when chosen. Click = refill with it.</summary>
    public class XUiC_StashNetFuelChoice : XUiController
    {
        public event Action<string> Picked;

        private string fuel;
        private int count;
        private bool chosen;
        private int refill;

        public override void Init()
        {
            base.Init();
            XUiController button = GetChildById("btnFuel");
            if (button != null) button.OnPress += (_sender, _button) => { if (fuel != null) Picked?.Invoke(fuel); };
        }

        public void Show(string name, int stored, bool isChosen, int refillAmount)
        {
            fuel = name;
            count = stored;
            chosen = isChosen;
            refill = refillAmount;
        }

        public override bool GetBindingValueInternal(ref string value, string bindingName)
        {
            ItemClass ic = fuel == null ? null : ItemClass.GetItemClass(fuel);
            switch (bindingName)
            {
                case "stashnet_hasfuelitem":
                    value = ic != null ? "true" : "false";
                    return true;
                case "stashnet_fuelicon":
                    value = ic?.GetIconName() ?? "";
                    return true;
                case "stashnet_fueliconcolor":
                    UnityEngine.Color c = ic?.GetIconTint() ?? UnityEngine.Color.white;
                    value = $"{(int)(c.r * 255)},{(int)(c.g * 255)},{(int)(c.b * 255)},{(int)(c.a * 255)}";
                    return true;
                case "stashnet_fuelcount":
                    value = ic == null ? "" : count.ToString();
                    return true;
                case "stashnet_fuelname":
                    value = ic == null ? "" : string.Format(Localization.Get("stashnetRefillTip"), ic.GetLocalizedItemName(), count, refill);
                    return true;
                case "stashnet_fuelbg":
                    value = chosen ? "70,105,70,255" : "48,48,48,255";
                    return true;
                default:
                    return base.GetBindingValueInternal(ref value, bindingName);
            }
        }
    }
}
