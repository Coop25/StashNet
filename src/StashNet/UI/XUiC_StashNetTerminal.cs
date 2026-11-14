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
    /// Click = withdraw a stack, right-click = one, shift-click in backpack = deposit,
    /// header button = deposit all. Search filters by name; sort = count (default) or name.
    /// </summary>
    public class XUiC_StashNetTerminal : XUiController
    {
        public const string WindowGroupId = "stashnetTerminal";
        private const float RefreshInterval = 2f;

        private static Vector3i pendingPos;
        /// <summary>Remembered while the game runs. Default: highest count first.</summary>
        private static bool sortByName;

        private XUiC_StashNetEntry[] entries = new XUiC_StashNetEntry[0];
        private readonly List<ItemAmount> listing = new List<ItemAmount>();
        private readonly List<ItemAmount> shown = new List<ItemAmount>();
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
            XUiController grid = GetChildById("entries");
            if (grid != null) grid.OnScroll += (_sender, delta) => Scroll(delta);
            Hook("btnPrev", () => ChangePage(-1));
            Hook("btnNext", () => ChangePage(1));
            Hook("btnDepositAll", DepositAll);
            Hook("btnSort", ToggleSort);
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
            page = 0;
            listing.Clear();
            usage = "";
            status = Localization.Get("stashnetLoading");
            hoverName = "";
            Render();
            RequestListing();
            RefreshBackpackButtons();
        }

        public override void OnClose()
        {
            base.OnClose();
            isOpen = false;
            if (Current == this) Current = null;
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
                usage = string.Format(Localization.Get("stashnetUsage"), response.DistinctItems, response.UsedStacks, response.MaxStacks, response.Drives);
            }
            // The reply to the user's own action always sets the message; background refreshes
            // only replace it when they have something to say.
            string messages = FormatMessages(response.Messages);
            if (response.RequestId == lastActionRequestId || !string.IsNullOrEmpty(messages)) status = messages;
            Render();
        }

        public void Withdraw(ItemAmount item, bool single)
        {
            int count = single ? 1 : Math.Min(item.Count, StackSize(item.Name));
            Send(TerminalOp.Withdraw, new ItemAmount(item.Name, item.Variant, count));
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

        /// <summary>Header button, and the backpack's "move all" button while a terminal is open.</summary>
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
            IEnumerable<ItemAmount> items = listing;
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
                entries[i].SetItem(idx < shown.Count ? shown[idx] : (ItemAmount?)null);
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
