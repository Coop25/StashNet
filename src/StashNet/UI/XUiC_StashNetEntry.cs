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

        public override void Init()
        {
            base.Init();
            XUiController button = GetChildById("clickable");
            if (button == null) return;
            // Holding a picked-up stack: click deposits it (right-click deposits one), on any cell.
            // Otherwise (owner, 2026-11-14): click selects the cell and shows it in the info panel;
            // clicking the selected cell again takes one; shift-click takes a stack; right-click takes one.
            button.OnPress += (_sender, _mouseButton) =>
            {
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

        public void SetItem(ItemGroup newGroup)
        {
            group = newGroup;
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
