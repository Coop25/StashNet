using System.Globalization;
using HarmonyLib;
using StashNet.Blocks;
using StashNet.Core.Drives;
using StashNet.UI;

namespace StashNet.Patches
{
    /// <summary>
    /// Feeds the drive fill bar and centered percent added to the vanilla item_stack template
    /// (Mod/Config/XUi_InGame/templates.xml). Reads only the slot's own item stack, so it works in
    /// any slot: backpack, toolbelt, Drive Bays, loot bags.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_ItemStack), nameof(XUiC_ItemStack.GetBindingValueInternal))]
    public static class DriveSlotFillPatch
    {
        public static void Postfix(XUiC_ItemStack __instance, ref string _value, string _bindingName, ref bool __result)
        {
            if (_bindingName == null || !_bindingName.StartsWith("stashnet", System.StringComparison.Ordinal)) return;

            ItemStack stack = __instance.ItemStack;
            DriveFill fill = stack != null && !stack.IsEmpty() && DriveRules.IsDrive(stack.itemValue)
                ? DriveFillCache.Get(stack.itemValue)
                : DriveFill.Empty;
            bool shown = fill.Band == FillBand.Low || fill.Band == FillBand.High || fill.Band == FillBand.Full;

            switch (_bindingName)
            {
                case "stashnethasfill":
                    _value = shown ? "true" : "false";
                    break;
                case "stashnetfill":
                    _value = fill.Fraction.ToString("0.###", CultureInfo.InvariantCulture);
                    break;
                case "stashnetfillcolor":
                    _value = fill.Band == FillBand.Full ? "200,30,30,255" : fill.Band == FillBand.High ? "230,190,0,255" : "0,170,0,255";
                    break;
                case "stashnetfilltext":
                    _value = fill.PercentText;
                    break;
                default:
                    return;
            }
            __result = true;
        }
    }
}
