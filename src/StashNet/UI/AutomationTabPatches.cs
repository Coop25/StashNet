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
    /// At a Network station with no recipe selected, vanilla's Update shows the backpack's empty info panel
    /// in the recipe box's place (info panels are mutually exclusive). The prefix points vanilla's
    /// emptyInfoWindow at the recipe box itself for that Update, so vanilla "shows" what is already showing
    /// and nothing is switched off and on (that would drop text-box focus). The postfix brings the recipe box
    /// in when the window opens. Restored in the finalizer.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.Update))]
    public static class AutomationRecipeBoxShownPatch
    {
        public static void Prefix(XUiC_CraftingInfoWindow __instance, out XUiC_InfoWindow __state)
        {
            __state = null;
            if (!Crafting.CraftingNetwork.Active) return;
            try
            {
                if (!XUiC_StashNetAutomation.OwnsEmptySlot(__instance) || __instance.ViewComponent?.IsVisible != true) return;
                __state = __instance.emptyInfoWindow ?? __instance; // non-null marks "swapped"
                __instance.emptyInfoWindow = __instance;
            }
            catch (System.Exception e)
            {
                Log.Warning($"[StashNet] automation tab: {e.Message}");
            }
        }

        public static void Postfix(XUiC_CraftingInfoWindow __instance)
        {
            if (!Crafting.CraftingNetwork.Active) return;
            try
            {
                XUiC_StashNetAutomation.KeepRecipeBoxShown(__instance);
            }
            catch (System.Exception e)
            {
                Log.Warning($"[StashNet] automation tab: {e.Message}");
            }
        }

        public static void Finalizer(XUiC_CraftingInfoWindow __instance, XUiC_InfoWindow __state)
        {
            if (__state == null) return;
            __instance.emptyInfoWindow = __state == __instance ? null : __state;
        }
    }

    /// <summary>
    /// Selecting another recipe makes vanilla switch back to Ingredients. A tab the player opened stays open
    /// (they pick the recipe to keep in stock while on it); one that opened by itself (no recipe) hands back.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_CraftingInfoWindow), nameof(XUiC_CraftingInfoWindow.SetRecipe))]
    public static class AutomationTabRecipePatch
    {
        public static void Postfix(XUiC_CraftingInfoWindow __instance, XUiC_RecipeEntry _recipeEntry)
        {
            if (!XUiC_StashNetAutomation.TabOpen) return;
            __instance.GetChildByType<XUiC_StashNetAutomation>()?.RecipeChanged(_recipeEntry?.Recipe != null);
        }
    }
}
