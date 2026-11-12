using System;
using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Network;
using StashNet.Drives;
using StashNet.Network;

namespace StashNet.UI
{
    /// <summary>
    /// The Storage Terminal window (Mod/Config/XUi_InGame/windows.xml "stashnetTerminalWindow").
    /// Shows every item in the network, a page at a time. Click = withdraw a stack,
    /// right-click = withdraw one, shift-click in the backpack = deposit, button = deposit all.
    /// </summary>
    public class XUiC_StashNetTerminal : XUiController
    {
        public const string WindowGroupId = "stashnetTerminal";

        private static Vector3i pendingPos;

        private XUiC_StashNetEntry[] entries = new XUiC_StashNetEntry[0];
        private readonly List<NetworkItem> items = new List<NetworkItem>();
        private Vector3i terminalPos;
        private int page;
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
            foreach (XUiC_StashNetEntry e in entries) e.Owner = this;
            Hook("btnPrev", () => ChangePage(-1));
            Hook("btnNext", () => ChangePage(1));
            Hook("btnDepositAll", DepositAll);
        }

        public override void OnOpen()
        {
            base.OnOpen();
            terminalPos = pendingPos;
            Current = this;
            page = 0;
            status = "";
            hoverName = "";
            Reload();
        }

        public override void OnClose()
        {
            base.OnClose();
            if (Current == this) Current = null;
        }

        public void Withdraw(NetworkItem item, bool single)
        {
            if (item == null) return;
            int count = single ? 1 : StackSize(item);
            TerminalOps.Withdraw(xui.playerUI.entityPlayer, terminalPos, item.Name, item.Variant, count, out string message);
            status = message ?? "";
            Reload();
        }

        /// <summary>Called by the shift-click patch for a backpack/toolbelt slot.</summary>
        public void DepositFromSlot(XUiC_ItemStack slot)
        {
            ItemStack stack = slot.ItemStack;
            int accepted = TerminalOps.DepositStack(xui.playerUI.entityPlayer, terminalPos, stack, out string message);
            if (accepted > 0)
            {
                ItemStack rest = stack.Clone();
                rest.count -= accepted;
                slot.ItemStack = rest.count > 0 ? rest : ItemStack.Empty.Clone();
                slot.HandleSlotChangeEvent();
            }
            status = message ?? "";
            Reload();
        }

        public void SetHover(NetworkItem item)
        {
            hoverName = item == null ? "" : DisplayName(item);
            RefreshBindings();
        }

        public override bool GetBindingValueInternal(ref string value, string bindingName)
        {
            switch (bindingName)
            {
                case "stashnet_usage": value = usage; return true;
                case "stashnet_status": value = status; return true;
                case "stashnet_hover": value = hoverName; return true;
                case "stashnet_page": value = $"{page + 1}/{PageCount}"; return true;
                default: return base.GetBindingValueInternal(ref value, bindingName);
            }
        }

        private int PageCount => Math.Max(1, (items.Count + entries.Length - 1) / Math.Max(1, entries.Length));

        private void DepositAll()
        {
            int moved = TerminalOps.DepositBackpack(xui.playerUI.entityPlayer, terminalPos, out string message);
            status = message ?? string.Format(Localization.Get("stashnetDeposited"), moved);
            Reload();
        }

        private void ChangePage(int delta)
        {
            page = Math.Max(0, Math.Min(PageCount - 1, page + delta));
            ShowPage();
        }

        private void Reload()
        {
            items.Clear();
            if (StashNetNetwork.TryOpen(xui.playerUI.entityPlayer.world, terminalPos, out StashNetNetwork net, out string problem))
            {
                items.AddRange(net.Storage.Aggregate().OrderBy(DisplayName, StringComparer.CurrentCultureIgnoreCase));
                usage = string.Format(Localization.Get("stashnetUsage"), net.Storage.UsedTypes, net.Storage.MaxTypes, net.Storage.UsedItems, net.Storage.MaxItems, net.Storage.Drives.Count);
                if (net.BayCount == 0) problem = Localization.Get("stashnetNoBays");
                else if (net.Storage.Drives.Count == 0) problem = Localization.Get("stashnetNoDrives");
                if (net.SkippedBays > 0) problem = Join(problem, string.Format(Localization.Get("stashnetSkippedBays"), net.SkippedBays));
                if (net.SkippedDrives > 0) problem = Join(problem, string.Format(Localization.Get("stashnetSkippedDrives"), net.SkippedDrives));
            }
            else
            {
                usage = "";
            }
            if (!string.IsNullOrEmpty(problem)) status = Join(status, problem);
            page = Math.Min(page, PageCount - 1);
            ShowPage();
        }

        private void ShowPage()
        {
            int start = page * entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                int idx = start + i;
                entries[i].SetItem(idx < items.Count ? items[idx] : null);
            }
            RefreshBindingsSelfAndChildren();
        }

        private void Hook(string id, Action action)
        {
            XUiController c = GetChildById(id);
            if (c != null) c.OnPress += (_sender, _button) => action();
        }

        private static int StackSize(NetworkItem item)
        {
            ItemClass ic = ItemClass.GetItemClass(item.Name);
            return Math.Max(1, ic?.Stacknumber.Value ?? 1);
        }

        internal static string DisplayName(NetworkItem item)
        {
            ItemClass ic = ItemClass.GetItemClass(item.Name);
            return ic != null ? ic.GetLocalizedItemName() : item.Name;
        }

        private static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : a + "  " + b;
    }
}
