using StashNet.Core.Protocol;
using StashNet.Drives;

namespace StashNet.UI
{
    /// <summary>
    /// One cell in the terminal grid: a group of look-alike stored items shown as one pile (icon +
    /// total count). Child button "clickable" handles clicks. The corner symbol (read/known, modded,
    /// boosted, block type) comes from the game's own helper so it matches backpack slots.
    /// </summary>
    public class XUiC_StashNetEntry : XUiController
    {
        private readonly CachedStringFormatterXuiRgbaColor colorFormatter = new CachedStringFormatterXuiRgbaColor();
        private ItemGroup group;
        private ItemValue display;
        private ItemStack displayStack = ItemStack.Empty.Clone();

        public XUiC_StashNetTerminal Owner { get; set; }

        private XUiController button;
        private readonly DragGesture drag = new DragGesture();
        /// <summary>Which item this cell shows (name + group key), made once per listing so Update allocates nothing.</summary>
        private string dragKey;

        /// <summary>
        /// Drag out (owner, 2026-11-18), like picking up a backpack stack: press on this cell and move the mouse with the
        /// left button held, and one stack (a tool: one item) is taken onto the cursor. Rules in DragGesture.
        /// </summary>
        public override void Update(float _dt)
        {
            base.Update(_dt);
            if (Owner == null || button?.ViewComponent == null || xui?.playerUI?.CursorController == null) return;
            CursorControllerAbs cursor = xui.playerUI.CursorController;
            UnityEngine.Vector2 pos = cursor.GetScreenPosition();
            XUiC_DragAndDropWindow held = xui.DragAndDropWindow;
            bool canTake = group != null && !InputUtils.ShiftKeyPressed && held != null && held.IsEmpty();
            if (drag.Frame(
                    cursor.GetMouseButtonDown(UICamera.MouseButton.LeftButton),
                    cursor.GetMouseButton(UICamera.MouseButton.LeftButton),
                    cursor.GetMouseButtonUp(UICamera.MouseButton.LeftButton),
                    button.ViewComponent.UiTransformIsHovered,
                    pos.x, pos.y, dragKey, canTake))
            {
                Owner.WithdrawToCursor(group);
            }
        }

        public override void Init()
        {
            base.Init();
            button = GetChildById("clickable");
            if (button == null) return;
            // Holding a picked-up stack: click deposits it (right-click deposits one), on any cell.
            // Otherwise (owner, 2026-11-14): click selects the cell and shows it in the info panel;
            // clicking the selected cell again takes one; shift-click takes a stack; right-click takes one.
            // Press and drag (2026-11-18): see Update.
            button.OnPress += (_sender, _mouseButton) =>
            {
                // The release that ends a drag-out: the stack is (or will be) on the cursor; don't store it right
                // back, select or take more.
                if (drag.ConsumeClick()) return;
                if (Owner == null || Owner.TryDepositHeld(false) || group == null) return;
                if (InputUtils.ShiftKeyPressed) Owner.Withdraw(group, false);
                else if (Owner.ShowingSelected(group)) Owner.Withdraw(group, true);
                else Owner.Select(this, group);
            };
            button.OnRightPress += (_sender, _mouseButton) =>
            {
                if (Owner == null || Owner.TryDepositHeld(true)) return;
                if (group != null) Owner.Withdraw(group, true);
            };
            button.OnHover += (_sender, isOver) => Owner?.SetHover(isOver && group != null ? group.Display : (ItemAmount?)null);
        }

        /// <summary>Does this cell show that group (same item and group key)?</summary>
        public bool Shows(ItemGroup other) =>
            group != null && other != null && group.Name == other.Name &&
            ItemCodec.GroupKey(group.Name, group.Display.Variant) == ItemCodec.GroupKey(other.Name, other.Display.Variant);

        public void SetItem(ItemGroup newGroup)
        {
            group = newGroup;
            dragKey = group == null ? null : group.Name + "\u0001" + ItemCodec.GroupKey(group.Name, group.Display.Variant);
            display = null;
            displayStack = ItemStack.Empty.Clone();
            if (group == null) return;
            ItemAmount shown = group.Display;
            if (ItemCodec.TryDecode(shown.Name, shown.Variant, out display, out _)) displayStack = new ItemStack(display, shown.Count);
        }

        private bool HasItem => group != null;

        public override bool GetBindingValueInternal(ref string value, string bindingName)
        {
            switch (bindingName)
            {
                case "stashnet_selected":
                    value = HasItem && Owner != null && Owner.IsSelected(group) ? "true" : "false";
                    return true;
                case "stashnet_hasitem":
                    value = HasItem ? "true" : "false";
                    return true;
                case "stashnet_icon":
                    value = display?.ItemClass?.GetIconName() ?? "";
                    return true;
                case "stashnet_iconcolor":
                    value = display?.ItemClass != null ? colorFormatter.Format(display.ItemClass.GetIconTint(display)) : "255,255,255,255";
                    return true;
                case "stashnet_count":
                    value = HasItem ? FormatCount(group.Count) : "";
                    return true;
                case "stashnet_pinned":
                    value = HasItem && XUiC_StashNetTerminal.IsPinned(group.Name) ? "true" : "false";
                    return true;
                // Corner symbol, same rules and colors as backpack slots.
                case "stashnet_hastypeicon":
                    value = HasTypeIcon ? "true" : "false";
                    return true;
                case "stashnet_typeicon":
                    value = HasTypeIcon ? XUiBindingHelper.GetItemTypeIconValue(xui, displayStack) : "";
                    return true;
                case "stashnet_typeicontint":
                    value = HasTypeIcon ? colorFormatter.Format(XUiBindingHelper.GetItemTypeIconTintColor(xui, displayStack)) : "255,255,255,255";
                    return true;
                // Quality/durability bar, same rules and colors as vanilla item slots.
                case "stashnet_hasdurability":
                    value = HasDurability ? "true" : "false";
                    return true;
                case "stashnet_nodurability":
                    value = HasItem && !HasDurability ? "true" : "false";
                    return true;
                // Red behind the bar when max durability is permanently lost (vanilla "haspermadurability").
                case "stashnet_durabilitybackcolor":
                    value = HasDurability && EntityPlayerLocal.PermaDegrationOn && display.MaxDurabilityModifier < 1f ? "185,0,0,255" : "64,64,64,255";
                    return true;
                case "stashnet_durabilitycolor":
                    value = colorFormatter.Format(QualityInfo.GetQualityColor(display?.Quality ?? 0));
                    return true;
                case "stashnet_durabilityfill":
                    value = DurabilityFill(display).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                default:
                    return base.GetBindingValueInternal(ref value, bindingName);
            }
        }

        private bool HasDurability => HasItem && (display?.ItemClass?.ShowQualityBar ?? false);

        private bool HasTypeIcon => HasItem && display != null && xui?.playerUI?.entityPlayer != null
            && XUiBindingHelper.GetHasItemTypeIconValue(displayStack) == bool.TrueString;

        /// <summary>Fraction of durability left (vanilla formula); 1 for items without wear.</summary>
        internal static float DurabilityFill(ItemValue iv)
        {
            if (iv == null || iv.MaxUseTimes == 0 || iv.MaxUseTimesUI == 0) return 1f;
            float fill = (iv.MaxUseTimes - iv.UseTimes) / iv.MaxUseTimesUI;
            return fill < 0f ? 0f : fill > 1f ? 1f : fill;
        }

        private static string FormatCount(int n)
        {
            if (n < 10000) return n.ToString();
            if (n < 1000000) return (n / 1000f).ToString("0.#") + "k";
            return (n / 1000000f).ToString("0.#") + "M";
        }
    }
}
