using System;
using System.Collections.Generic;
using System.Linq;
using StashNet.Blocks;
using StashNet.Core.Drives;
using StashNet.Core.Protocol;
using StashNet.Drives;
using StashNet.Network;

namespace StashNet.UI
{
    /// <summary>
    /// The Storage Terminal window (Mod/Config/XUi_InGame/windows.xml "stashnetTerminalWindow").
    /// Renders the last listing the server sent; every action is a request via TerminalClient.
    /// Click = withdraw one, shift-click = a stack, right-click = one; look-alike items (same but for
    /// the hidden seed, no quality) share a cell. Search filters by name; sort = count (default) or name.
    /// </summary>
    public class XUiC_StashNetTerminal : XUiController
    {
        public const string WindowGroupId = "stashnetTerminal";
        private const float RefreshInterval = 2f;

        private static Vector3i pendingPos;
        /// <summary>Remembered while the game runs. Default: highest count first.</summary>
        private static bool sortByName;
        /// <summary>Selected category tab, remembered while the game runs. "" = All.</summary>
        private static string category = "";

        private XUiC_CategoryList categories;
        private static List<CraftingCategoryDisplayEntry> allCategories;
        private string shownCategoriesKey;
        private bool listingLoaded;
        private readonly Dictionary<string, string[]> groupsByName = new Dictionary<string, string[]>();

        private XUiC_StashNetEntry[] entries = new XUiC_StashNetEntry[0];
        private readonly List<ItemAmount> listing = new List<ItemAmount>();
        private readonly List<ItemGroup> shown = new List<ItemGroup>();
        private XUiC_TextInput searchInput;
        private string search = "";
        private Vector3i terminalPos;
        private int page;
        private float refreshTimer;
        private int lastActionRequestId = -1;
        private bool isOpen;
        private string usage = "";
        private string status = "";
        private string hoverName = "";

        /// <summary>The terminal window currently open for the local player, if any.</summary>
        public static XUiC_StashNetTerminal Current { get; private set; }

        public static void Open(EntityPlayerLocal player, Vector3i terminalPos)
        {
            pendingPos = terminalPos;
            player.PlayerUI.windowManager.Open(WindowGroupId, true);
        }

        public override void Init()
        {
            base.Init();
            entries = GetChildrenByType<XUiC_StashNetEntry>();
            foreach (XUiC_StashNetEntry e in entries)
            {
                e.Owner = this;
                XUiController button = e.GetChildById("clickable");
                if (button != null) button.OnScroll += (_sender, delta) => Scroll(delta);
            }
            // Not on the grid itself: a grid collider would tie with the cell buttons and could steal their clicks.
            Hook("btnPrev", () => ChangePage(-1));
            Hook("btnNext", () => ChangePage(1));
            Hook("btnSort", ToggleSort);
            categories = GetChildByType<XUiC_CategoryList>();
            if (categories != null) categories.CategoryChanged += entry =>
            {
                category = entry?.CategoryName ?? "";
                page = 0;
                Render();
            };
            searchInput = GetChildById("searchInput") as XUiC_TextInput;
            if (searchInput != null) searchInput.OnChangeHandler += (_sender, text, _fromCode) =>
            {
                search = text ?? "";
                page = 0;
                Render();
            };
        }

        public override void OnOpen()
        {
            base.OnOpen();
            terminalPos = pendingPos;
            Current = this;
            isOpen = true;
            // Vanilla menus (inventory tabs, the loot/workstation header) set this. Without it the
            // drag-and-drop window puts a dragged item straight back every frame, so dragging fails.
            if (xui.DragAndDropWindow != null) xui.DragAndDropWindow.InMenu = true;
            // Repair (info panel of a backpack item) may use stored repair kits while this window is open.
            Crafting.CraftingNetwork.OpenForRepair(terminalPos);
            page = 0;
            listing.Clear();
            listingLoaded = false;
            shownCategoriesKey = null;
            usage = "";
            status = Localization.Get("stashnetLoading");
            hoverName = "";
            SetupCategories();
            Render();
            RequestListing();
            RefreshBackpackButtons();
        }

        /// <summary>
        /// Tabs: "All", then the game's own item categories in the order the backpack filter uses
        /// (ui_display.xml trader_category_display), so new or modded categories show up too. Only
        /// categories that hold at least one stored item get a tab (owner, 2026-11-14); they're packed
        /// left in that order and rebuilt whenever the set changes.
        /// </summary>
        private void SetupCategories()
        {
            if (categories == null) return;
            if (allCategories == null)
            {
                allCategories = new List<CraftingCategoryDisplayEntry>();
                foreach (CraftingCategoryDisplayEntry c in UIDisplayInfoManager.Current.GetTraderCategoryDisplayList())
                {
                    if (c != null) allCategories.Add(c);
                }
            }

            var visible = new List<CraftingCategoryDisplayEntry>();
            foreach (CraftingCategoryDisplayEntry c in allCategories)
            {
                if (listing.Any(i => InCategory(i.Name, c.Name))) visible.Add(c);
            }
            string key = string.Join("|", visible.Select(c => c.Name));
            if (key != shownCategoriesKey)
            {
                shownCategoriesKey = key;
                int slots = categories.MaxCategories;
                int n = 0;
                if (slots > 0) categories.SetCategoryEntry(n++, "", "ui_game_symbol_loot_sack", Localization.Get("lblAll"));
                foreach (CraftingCategoryDisplayEntry c in visible)
                {
                    if (n >= slots) break;
                    categories.SetCategoryEntry(n++, c.Name, c.Icon, c.DisplayName);
                }
                for (int i = n; i < slots; i++) categories.SetCategoryEmpty(i);
            }

            XUiC_CategoryEntry selected = null;
            foreach (XUiC_CategoryEntry e in categories.CategoryButtons)
            {
                if (e.SpriteName != "" && e.CategoryName == category) { selected = e; break; }
            }
            if (selected == null)
            {
                selected = categories.GetCategoryByIndex(0);
                // Forget a remembered tab only once we know the network really has none of it.
                if (listingLoaded && category.Length > 0) { category = ""; page = 0; }
            }
            if (categories.CurrentCategory != selected) categories.CurrentCategory = selected;
        }

        /// <summary>Same rule as the backpack filter: the item's (or block's) group names. "" = All.</summary>
        private bool InCategory(string itemName, string categoryName)
        {
            if (categoryName.Length == 0) return true;
            if (!groupsByName.TryGetValue(itemName, out string[] groups))
            {
                ItemClass ic = ItemClass.GetItemClass(itemName);
                groups = ic == null ? new string[0] : ic.IsBlock() ? (Block.list[ic.Id]?.GroupNames ?? new string[0]) : (ic.Groups ?? new string[0]);
                groupsByName[itemName] = groups;
            }
            foreach (string g in groups)
            {
                if (g != null && g.EqualsCaseInsensitive(categoryName)) return true;
            }
            return false;
        }

        public override void OnClose()
        {
            base.OnClose();
            isOpen = false;
            if (Current == this) Current = null;
            if (xui.DragAndDropWindow != null) xui.DragAndDropWindow.InMenu = false;
            Crafting.CraftingNetwork.CloseRepair();
            RefreshBackpackButtons();
        }

        /// <summary>The backpack's move buttons are enabled only while there's somewhere to move to (see BackpackMovePatches).</summary>
        private void RefreshBackpackButtons()
        {
            xui.FindWindowGroupByName("backpack")?.GetChildByType<XUiC_BackpackWindow>()?.RefreshBindingsSelfAndChildren();
        }

        /// <summary>Mouse wheel over the item grid: wheel up = previous page, down = next.</summary>
        public void Scroll(float delta)
        {
            if (delta > 0f) ChangePage(-1);
            else if (delta < 0f) ChangePage(1);
        }

        public override void Update(float _dt)
        {
            base.Update(_dt);
            if (!isOpen) return;
            refreshTimer += _dt;
            if (refreshTimer >= RefreshInterval) RequestListing();
        }

        /// <summary>Called by TerminalClient when the server answers.</summary>
        public void OnResponse(TerminalResponse response)
        {
            if (response.HasListing)
            {
                listing.Clear();
                listing.AddRange(response.Listing);
                listingLoaded = true;
                SetupCategories();
                usage = string.Format(Localization.Get("stashnetUsage"), response.DistinctItems, response.UsedStacks, response.MaxStacks, response.Drives);
            }
            // The reply to the user's own action always sets the message; background refreshes
            // only replace it when they have something to say.
            string messages = FormatMessages(response.Messages);
            if (response.RequestId == lastActionRequestId || !string.IsNullOrEmpty(messages)) status = messages;
            Render();
        }

        /// <summary>Takes 1 (single) or one stack from a cell, split across the cell's stored members in order.</summary>
        public void Withdraw(ItemGroup group, bool single)
        {
            int count = single ? 1 : Math.Min(group.Count, StackSize(group.Name));
            TerminalRequest request = NewRequest(TerminalOp.Withdraw);
            request.Items.AddRange(ListingGroups.Split(group.Members, count));
            if (request.Items.Count > 0) SendAction(request);
        }

        /// <summary>
        /// Clicking the item grid while holding a picked-up stack on the cursor deposits it (owner,
        /// 2026-11-14): left click = the whole held stack, right click = one item. Returns false when
        /// nothing is held, so the click falls through to a normal withdraw. Leftovers the network
        /// can't take come back to the backpack, as with shift-click deposits.
        /// </summary>
        public bool TryDepositHeld(bool single)
        {
            XUiC_DragAndDropWindow drag = xui.DragAndDropWindow;
            if (drag == null || drag.IsEmpty()) return false;
            ItemStack held = drag.CurrentStack;
            if (DriveRules.IsDrive(held.itemValue))
            {
                status = Localization.Get("stashnetDropDrive");
                RefreshBindings();
                return true;
            }
            if (!Depositable(held))
            {
                status = Localization.Get("stashnetCantStore");
                RefreshBindings();
                return true;
            }
            int count = single ? 1 : held.count;
            ItemAmount amount = ToAmount(held);
            amount = new ItemAmount(amount.Name, amount.Variant, count);
            drag.SetCurrentStack(held.count > count ? new ItemStack(held.itemValue.Clone(), held.count - count) : ItemStack.Empty.Clone(), false);
            Send(TerminalOp.Deposit, amount);
            return true;
        }

        /// <summary>Shift-click on a backpack/toolbelt slot: remove it from the slot, then send it.</summary>
        public void DepositFromSlot(XUiC_ItemStack slot)
        {
            ItemStack stack = slot.ItemStack;
            if (stack != null && !stack.IsEmpty() && DriveRules.IsDrive(stack.itemValue))
            {
                UnloadDrive(slot);
                return;
            }
            if (!Depositable(stack))
            {
                status = Localization.Get("stashnetCantStore");
                RefreshBindings();
                return;
            }
            ItemAmount amount = ToAmount(stack);
            slot.ItemStack = ItemStack.Empty.Clone();
            slot.HandleSlotChangeEvent();
            Send(TerminalOp.Deposit, amount);
        }

        /// <summary>
        /// Shift-click on a drive: empty it in place (tagged with a one-time token), send its contents
        /// to the network, and the server's leftovers come back onto this same drive (TerminalClient).
        /// </summary>
        private void UnloadDrive(XUiC_ItemStack slot)
        {
            ItemStack stack = slot.ItemStack;
            if (!DriveItem.TryLoad(stack.itemValue, out DriveContents contents, out string error))
            {
                status = error;
                RefreshBindings();
                return;
            }
            if (contents.TypeCount == 0)
            {
                status = Localization.Get("stashnetDriveEmpty");
                RefreshBindings();
                return;
            }

            TerminalRequest request = NewRequest(TerminalOp.DriveUnload);
            foreach (DriveEntry e in contents.Entries) request.Items.Add(new ItemAmount(e.Name, e.Variant, e.Count));

            string token = Guid.NewGuid().ToString("N");
            ItemValue emptied = stack.itemValue.Clone();
            DriveItem.Save(emptied, new DriveContents());
            emptied.SetMetadata(DriveItem.PendingKey, token);
            slot.ItemStack = new ItemStack(emptied, stack.count);
            slot.HandleSlotChangeEvent();

            refreshTimer = 0f;
            status = "";
            lastActionRequestId = TerminalClient.NextRequestId;
            TerminalClient.SendDriveUnload(request, token);
        }

        public void SetHover(ItemAmount? item)
        {
            hoverName = item.HasValue ? HoverText(item.Value) : "";
            RefreshBindings();
        }

        /// <summary>Name, plus tier and durability for tools/weapons/armor, e.g. "Steel Pickaxe (T6, 50%)".</summary>
        private static string HoverText(ItemAmount item)
        {
            string name = DisplayName(item.Name);
            if (item.Variant == null || !ItemCodec.TryDecode(item.Name, item.Variant, out ItemValue iv, out _)) return name;
            if (iv.HasQuality && (iv.ItemClass?.ShowQualityBar ?? false))
            {
                int percent = (int)Math.Round(XUiC_StashNetEntry.DurabilityFill(iv) * 100f);
                return $"{name} (T{iv.Quality}, {percent}%)";
            }
            return name;
        }

        public override bool GetBindingValueInternal(ref string value, string bindingName)
        {
            switch (bindingName)
            {
                case "stashnet_usage": value = usage; return true;
                case "stashnet_status": value = status; return true;
                case "stashnet_hover": value = hoverName; return true;
                case "stashnet_page": value = $"{page + 1}/{PageCount}"; return true;
                case "stashnet_sort": value = Localization.Get(sortByName ? "stashnetSortName" : "stashnetSortCount"); return true;
                default: return base.GetBindingValueInternal(ref value, bindingName);
            }
        }

        private int PageCount => Math.Max(1, (shown.Count + entries.Length - 1) / Math.Max(1, entries.Length));

        /// <summary>The backpack's "move all" button while a terminal is open (the header button was removed, owner 2026-11-14).</summary>
        public void DepositAll() => DepositBackpack(false);

        /// <summary>The backpack's "fill and smart" button: only items the network already holds.</summary>
        public void DepositMatching() => DepositBackpack(true);

        private void DepositBackpack(bool onlyMatching)
        {
            Bag bag = xui.playerUI.entityPlayer.bag;
            PackedBoolArray locked = bag.LockedSlots;
            HashSet<string> stored = onlyMatching ? new HashSet<string>(listing.Select(i => i.Name)) : null;
            var request = NewRequest(TerminalOp.Deposit);
            for (int i = 0; i < bag.SlotCount; i++)
            {
                ItemStack stack = bag.ItemGrid[i];
                if (locked != null && i < locked.Length && locked[i]) continue;
                if (!Depositable(stack)) continue;
                if (stored != null && !stored.Contains(ItemCodec.NameOf(stack.itemValue))) continue;
                request.Items.Add(ToAmount(stack));
                bag.SetSlot(i, ItemStack.Empty.Clone());
            }
            if (request.Items.Count == 0)
            {
                status = Localization.Get("stashnetNothingDeposited");
                RefreshBindings();
                return;
            }
            SendAction(request);
        }

        private void ToggleSort()
        {
            sortByName = !sortByName;
            page = 0;
            Render();
        }

        private void ChangePage(int delta)
        {
            page = Math.Max(0, Math.Min(PageCount - 1, page + delta));
            Render();
        }

        private void RequestListing()
        {
            refreshTimer = 0f;
            TerminalClient.Send(NewRequest(TerminalOp.List));
        }

        private void Send(TerminalOp op, ItemAmount item)
        {
            TerminalRequest request = NewRequest(op);
            request.Items.Add(item);
            SendAction(request);
        }

        private void SendAction(TerminalRequest request)
        {
            refreshTimer = 0f;
            status = "";
            // TerminalClient assigns the id; in single-player the reply arrives inside Send, so
            // predict it to recognise the reply either way.
            lastActionRequestId = TerminalClient.NextRequestId;
            TerminalClient.Send(request);
        }

        private TerminalRequest NewRequest(TerminalOp op) =>
            new TerminalRequest { Op = op, X = terminalPos.x, Y = terminalPos.y, Z = terminalPos.z };

        private void Render()
        {
            IEnumerable<ItemGroup> items = ListingGroups.Fold(listing, i => ItemCodec.GroupKey(i.Name, i.Variant)).Where(i => InCategory(i.Name, category));
            if (search.Length > 0)
            {
                items = items.Where(i => DisplayName(i.Name).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0);
            }
            items = sortByName
                ? items.OrderBy(i => DisplayName(i.Name), StringComparer.CurrentCultureIgnoreCase).ThenByDescending(i => i.Count)
                : items.OrderByDescending(i => i.Count).ThenBy(i => DisplayName(i.Name), StringComparer.CurrentCultureIgnoreCase);
            shown.Clear();
            shown.AddRange(items);

            page = Math.Min(page, PageCount - 1);
            int start = page * entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                int idx = start + i;
                entries[i].SetItem(idx < shown.Count ? shown[idx] : null);
            }
            RefreshBindingsSelfAndChildren();
        }

        private void Hook(string id, Action action)
        {
            XUiController c = GetChildById(id);
            if (c != null) c.OnPress += (_sender, _button) => action();
        }

        private static bool Depositable(ItemStack stack) =>
            stack != null && !stack.IsEmpty() && !DriveRules.IsDrive(stack.itemValue) && ItemCodec.CanStore(stack.itemValue);

        private static ItemAmount ToAmount(ItemStack stack) =>
            new ItemAmount(ItemCodec.NameOf(stack.itemValue), ItemCodec.EncodeVariant(stack.itemValue), stack.count);

        private static int StackSize(string name) => ItemCodec.StackSizeOf(name);

        internal static string DisplayName(string itemName)
        {
            ItemClass ic = ItemClass.GetItemClass(itemName);
            return ic != null ? ic.GetLocalizedItemName() : itemName;
        }

        private static string FormatMessages(List<TerminalMessage> messages)
        {
            var parts = new List<string>();
            foreach (TerminalMessage m in messages)
            {
                string text = Localization.Get(m.Key);
                parts.Add(m.Arg >= 0 ? string.Format(text, m.Arg) : text);
            }
            return string.Join("  ", parts.Distinct().ToArray());
        }
    }
}
