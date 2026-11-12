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
            button.OnPress += (_sender, _mouseButton) => { if (item.HasValue) Owner?.Withdraw(item.Value, false); };
            button.OnRightPress += (_sender, _mouseButton) => { if (item.HasValue) Owner?.Withdraw(item.Value, true); };
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
                default:
                    return base.GetBindingValueInternal(ref value, bindingName);
            }
        }

        private static string FormatCount(int n)
        {
            if (n < 10000) return n.ToString();
            if (n < 1000000) return (n / 1000f).ToString("0.#") + "k";
            return (n / 1000000f).ToString("0.#") + "M";
        }
    }
}
