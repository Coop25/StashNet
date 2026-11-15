using HarmonyLib;

namespace StashNet.UI
{
    /// <summary>
    /// While the AUTOMATION tab is open (Network stations only), the vanilla recipe box hides its own
    /// tab contents (Ingredients / Description / Unlocks), so ours shows in their place.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.GetBindingValueInternal))]
    public static class AutomationTabBindingPatch
    {
        public static void Postfix(string bindingName, ref string value, ref bool __result)
        {
            if (!XUiC_StashNetAutomation.TabOpen) return;
            if (bindingName == "showingredients" || bindingName == "showdescription" || bindingName == "showunlockedby")
            {
                value = "false";
                __result = true;
            }
        }
    }

    /// <summary>
    /// Selecting another recipe makes vanilla switch back to Ingredients. With our tab open, keep it open
    /// (you pick the recipe to keep in stock while on it) and keep its button the selected one.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.SetRecipe))]
    public static class AutomationTabRecipePatch
    {
        public static void Postfix(XUiC_CraftingInfoWindow __instance)
        {
            if (!XUiC_StashNetAutomation.TabOpen) return;
            __instance.GetChildByType<XUiC_StashNetAutomation>()?.SelectTabButtons();
        }
    }
}
