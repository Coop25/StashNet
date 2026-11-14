using System;
using System.Collections.Generic;
using HarmonyLib;

namespace StashNet.Crafting
{
    // Crafting Terminal hooks (Milestone 6). Every hook is a no-op unless a StashNet Crafting
    // Terminal's workbench window is open (CraftingNetwork.Active), so plain workbenches and every
    // other inventory use are untouched.

    /// <summary>Start/stop tracking the network when a workstation window opens/closes; refresh while open.</summary>
    [HarmonyPatch(typeof(XUiC_WorkstationWindowGroup))]
    public static class WorkstationWindowPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(XUiC_WorkstationWindowGroup.OnOpen))]
        public static void OnOpen(XUiC_WorkstationWindowGroup __instance)
        {
            TileEntityWorkstation te = __instance.WorkstationData?.TileEntity;
            if (te == null) return;
            Vector3i pos = te.ToWorldPos();
            if (CraftingNetwork.IsCraftingTerminal(GameManager.Instance.World?.GetBlock(pos).Block))
            {
                CraftingNetwork.Open(pos, __instance);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(XUiC_WorkstationWindowGroup.OnClose))]
        public static void OnClose()
        {
            if (CraftingNetwork.Active) CraftingNetwork.Close();
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(XUiC_WorkstationWindowGroup.Update))]
        public static void Update(float _dt)
        {
            if (CraftingNetwork.Active) CraftingNetwork.Tick(_dt);
        }
    }

    /// <summary>Ingredient counts shown in the recipe info ("have/need") include network stock.</summary>
    [HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.GetItemCount), new[] { typeof(ItemValue) })]
    public static class GetItemCountByValuePatch
    {
        public static void Postfix(ItemValue _itemValue, ref int __result)
        {
            if (CraftingNetwork.CountsStock) __result += CraftingNetwork.Count(_itemValue);
        }
    }

    [HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.GetItemCount), new[] { typeof(int) })]
    public static class GetItemCountByIdPatch
    {
        public static void Postfix(int _itemId, ref int __result)
        {
            if (CraftingNetwork.CountsStock) __result += CraftingNetwork.Count(new ItemValue(_itemId));
        }
    }

    /// <summary>Craft-count limits and quality-ingredient scans see network stock. (Returns a fresh list, so appending is safe.)</summary>
    [HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.GetAllItemStacks))]
    public static class GetAllItemStacksPatch
    {
        public static void Postfix(List<ItemStack> __result)
        {
            if (CraftingNetwork.Active && __result != null) __result.AddRange(CraftingNetwork.Stacks());
        }
    }

    /// <summary>
    /// "Do I have the ingredients?" includes network stock. On an actual Craft click, the shortfall
    /// is pulled into the backpack first, so vanilla then takes everything from the backpack.
    /// </summary>
    [HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.HasItems))]
    public static class HasItemsPatch
    {
        public static void Postfix(XUiM_PlayerInventory __instance, IList<ItemStack> _itemStacks, int _multiplier, ref bool __result)
        {
            if (!CraftingNetwork.Active || _itemStacks == null) return;

            if (CraftingNetwork.InCraftClick)
            {
                if (__result) return; // backpack already has everything
                __result = CraftingNetwork.TryFetchShortfall(__instance, _itemStacks, _multiplier);
                if (!__result && WithNetwork(__instance, _itemStacks, _multiplier))
                {
                    // Remote client: the pull is on its way. Ask for another click.
                    EntityPlayerLocal player = GameManager.Instance.World?.GetPrimaryPlayer();
                    if (player != null) GameManager.ShowTooltip(player, Localization.Get("stashnetFetchingIngredients"), string.Empty, "ui_denied");
                }
                return;
            }

            if (!__result) __result = WithNetwork(__instance, _itemStacks, _multiplier);
        }

        private static bool WithNetwork(XUiM_PlayerInventory inv, IList<ItemStack> needs, int multiplier)
        {
            foreach (ItemStack need in needs)
            {
                int have = CraftingNetwork.RawCount(inv, need.itemValue) + CraftingNetwork.Count(need.itemValue);
                if (have < need.count * multiplier) return false;
            }
            return true;
        }
    }

    /// <summary>The recipe list sorts "craftable now" first; include network stock in that check.</summary>
    [HarmonyPatch(typeof(XUiC_RecipeList), nameof(XUiC_RecipeList.BuildRecipeInfosList))]
    public static class BuildRecipeInfosListPatch
    {
        public static void Prefix(List<ItemStack> _items)
        {
            if (CraftingNetwork.Active && _items != null) _items.AddRange(CraftingNetwork.Stacks());
        }
    }

    /// <summary>Marks the window during which vanilla handles a Craft click (so HasItems may pull ingredients).</summary>
    [HarmonyPatch(typeof(ItemActionEntryCraft), nameof(ItemActionEntryCraft.OnActivated))]
    public static class CraftClickPatch
    {
        public static void Prefix() => CraftingNetwork.InCraftClick = true;

        public static void Finalizer() => CraftingNetwork.InCraftClick = false;
    }

    /// <summary>Cancelling a queued craft at a Crafting Terminal: mark the refund window.</summary>
    [HarmonyPatch(typeof(XUiC_RecipeStack), nameof(XUiC_RecipeStack.HandleOnPress))]
    public static class CancelCraftPatch
    {
        public static void Prefix() => CraftingNetwork.InCancel = CraftingNetwork.Active;

        public static void Finalizer() => CraftingNetwork.InCancel = false;
    }

    /// <summary>
    /// During a cancel, vanilla refunds each ingredient with AddItem(stack, playSound). Send it to
    /// the network instead (owner request, 2026-11-12). Anything the network can't take comes back
    /// to the backpack, then the ground - same as vanilla's fallback.
    /// </summary>
    [HarmonyPatch(typeof(XUiM_PlayerInventory), nameof(XUiM_PlayerInventory.AddItem), new[] { typeof(ItemStack), typeof(bool) })]
    public static class CancelRefundToNetworkPatch
    {
        public static bool Prefix(ItemStack _itemStack, ref bool __result)
        {
            if (!CraftingNetwork.InCancel || !CraftingNetwork.TryRefundToNetwork(_itemStack)) return true;
            __result = true;
            return false;
        }
    }

    /// <summary>
    /// SERVER: crafts that finish while nobody has the Crafting Terminal open go into the block's
    /// own output slots. Move them into the network. Never while the station is open/locked by a
    /// player (their screen holds its own copy of the output and writes it back on close).
    /// </summary>
    [HarmonyPatch(typeof(TileEntityWorkstation), nameof(TileEntityWorkstation.UpdateTick))]
    public static class WorkstationOutputToNetworkPatch
    {
        public static void Postfix(TileEntityWorkstation __instance, World world) =>
            CraftingOutputServer.TryMoveOutput(world, __instance);
    }

    /// <summary>
    /// Ingredients pulled from the network land in the backpack before vanilla takes them, so a
    /// craft count whose shortfall doesn't fit in the backpack can't start. Cap the craft-count
    /// max (and the "max" button) to what actually fits. Owner's choice, 2026-11-12.
    /// </summary>
    [HarmonyPatch(typeof(XUiC_RecipeCraftCount), nameof(XUiC_RecipeCraftCount.calcMaxCraftable))]
    public static class CraftCountCapPatch
    {
        public static void Postfix(XUiC_RecipeCraftCount __instance, ref int __result)
        {
            if (!CraftingNetwork.Active || __result <= 1) return;
            Recipe recipe = __instance.recipe;
            EntityPlayerLocal player = __instance.xui?.playerUI?.entityPlayer;
            if (recipe == null || player == null) return;
            __result = CraftingNetwork.MaxCraftsThatFit(__instance.xui.PlayerInventory, player, recipe, __result);
        }
    }
}
