using HarmonyLib;
using StashNet.Blocks;
using StashNet.Core.Drives;
using StashNet.UI;

namespace StashNet.Patches
{
    /// <summary>
    /// The item info panel puts a live line above a drive's description, e.g.
    /// "142 / 216 stacks used (66%) · 37 different items" (2026-11-14).
    /// </summary>
    [HarmonyPatch(typeof(XUiC_ItemInfoWindow), nameof(XUiC_ItemInfoWindow.GetBindingValueInternal))]
    public static class DriveInfoDescriptionPatch
    {
        public static void Postfix(XUiC_ItemInfoWindow __instance, ref string value, string bindingName)
        {
            if (bindingName != "itemdescription") return;
            ItemStack stack = __instance.itemStack;
            if (stack == null || stack.IsEmpty() || !DriveRules.IsDrive(stack.itemValue)) return;

            DriveFill fill = DriveFillCache.Get(stack.itemValue);
            string line;
            switch (fill.Band)
            {
                case FillBand.Empty:
                    line = Localization.Get("stashnetDriveFillEmpty");
                    break;
                case FillBand.Unreadable:
                    line = Localization.Get("stashnetDriveFillUnreadable");
                    break;
                default:
                    line = string.Format(Localization.Get("stashnetDriveFillLine"), fill.Used, fill.Max, fill.PercentText, fill.Types);
                    break;
            }
            value = line + "\n\n" + value;
        }
    }
}
