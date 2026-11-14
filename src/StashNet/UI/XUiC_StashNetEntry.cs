using StashNet.Core.Protocol;
using StashNet.Drives;

namespace StashNet.UI
{
    /// <summary>One cell in the terminal grid: icon + count. Child button "clickable" handles clicks.</summary>
    public class XUiC_StashNetEntry : XUiController
    {
        private readonly CachedStringFormatterXuiRgbaColor colorFormatter = new CachedStringFormatterXuiRgbaColor();
        private ItemAmount? item;
        private ItemValue display;

        public XUiC_StashNetTerminal Owner { get; set; }

        public override void Init()
        {
            base.Init();
            XUiController button = GetChildById("clickable");
            if (button == null) return;
            // Holding a picked-up stack: click deposits it (right-click deposits one), on any cell.
            // Otherwise click takes one item, shift-click a full stack (owner, 2026-11-14); right-click takes one.
            button.OnPress += (_sender, _mouseButton) =>
            {
                if (Owner == null || Owner.TryDepositHeld(false)) return;
                if (item.HasValue) Owner.Withdraw(item.Value, !InputUtils.ShiftKeyPressed);
            };
            button.OnRightPress += (_sender, _mouseButton) =>
            {
                if (Owner == null || Owner.TryDepositHeld(true)) return;
                if (item.HasValue) Owner.Withdraw(item.Value, true);
            };
            button.OnHover += (_sender, isOver) => Owner?.SetHover(isOver ? item : null);
        }

        public void SetItem(ItemAmount? newItem)
        {
            item = newItem;
            display = null;
            if (item.HasValue) ItemCodec.TryDecode(item.Value.Name, item.Value.Variant, out display, out _);
        }

        public override bool GetBindingValueInternal(ref string value, string bindingName)
        {
            switch (bindingName)
            {
                case "stashnet_hasitem":
                    value = item.HasValue ? "true" : "false";
                    return true;
                case "stashnet_icon":
                    value = display?.ItemClass?.GetIconName() ?? "";
                    return true;
                case "stashnet_iconcolor":
                    value = display?.ItemClass != null ? colorFormatter.Format(display.ItemClass.GetIconTint(display)) : "255,255,255,255";
                    return true;
                case "stashnet_count":
                    value = item.HasValue ? FormatCount(item.Value.Count) : "";
                    return true;
                // Quality/durability bar, same rules and colors as vanilla item slots.
                case "stashnet_hasdurability":
                    value = HasDurability ? "true" : "false";
                    return true;
                case "stashnet_nodurability":
                    value = item.HasValue && !HasDurability ? "true" : "false";
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

        private bool HasDurability => item.HasValue && (display?.ItemClass?.ShowQualityBar ?? false);

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
