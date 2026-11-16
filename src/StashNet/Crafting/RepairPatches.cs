using System;
using HarmonyLib;
using UnityEngine;

namespace StashNet.Crafting
{
    // Repair with kits from storage (owner, 2026-11-14): while a Storage Terminal, a StashNet station
    // or the Crafting Terminal is open, the Repair button counts stored repair kits. On Repair, the
    // missing kits are pulled into the backpack first and vanilla then repairs from the backpack as
    // usual. Vanilla never sees network stock while it actually repairs (SuppressStock), so a repair
    // can never consume kits that aren't really in the backpack.

    /// <summary>"Can I repair?" includes stored kits.</summary>
    [HarmonyPatch(typeof(ItemActionEntryRepair), nameof(ItemActionEntryRepair.RefreshEnabled))]
    public static class RepairRefreshPatch
    {
        public static void Prefix() => CraftingNetwork.InRepairCheck = true;

        public static void Finalizer() => CraftingNetwork.InRepairCheck = false;
    }

    [HarmonyPatch(typeof(ItemActionEntryRepair), nameof(ItemActionEntryRepair.OnActivated))]
    public static class RepairClickPatch
    {
        // A joined player's kit fetch in flight: further clicks wait this long (or until the kits
        // arrive, which makes the shortfall 0) instead of withdrawing another batch.
        private const float PendingSeconds = 3f;
        private static int pendingKit = -1;
        private static float pendingUntil;

        public static bool Prefix(ItemActionEntryRepair __instance)
        {
            CraftingNetwork.SuppressStock = true;
            if (!(CraftingNetwork.Active || CraftingNetwork.RepairMode)) return true;

            XUi xui = __instance.ItemController?.xui;
            ItemValue item = RepairedItem(__instance);
            ItemClass itemClass = item?.ItemClass;
            if (xui == null || itemClass?.RepairTools == null || itemClass.RepairTools.Length == 0) return true;
            ItemClass kitClass = ItemClass.GetItemClass(itemClass.RepairTools[0].Value);
            if (kitClass == null || kitClass.RepairAmount.Value <= 0) return true;

            // Same formula as vanilla: kits needed to repair fully.
            int needed = Convert.ToInt32(Math.Ceiling((float)Mathf.CeilToInt(item.UseTimes) / kitClass.RepairAmount.Value));
            var kit = new ItemValue(kitClass.Id);
            int carried = CraftingNetwork.RawCount(xui.PlayerInventory, kit);
            int want = Math.Min(needed - carried, CraftingNetwork.Stock(kit));
            if (want <= 0) return true; // enough in the backpack, or storage has none: plain vanilla

            EntityPlayerLocal player = xui.playerUI?.entityPlayer;
            bool server = SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer;
            // Joined player clicking again before the kits arrive: don't ask for another batch.
            if (!server && kit.type == pendingKit && Time.unscaledTime < pendingUntil)
            {
                if (player != null) GameManager.ShowTooltip(player, Localization.Get("stashnetFetchingRepair"), string.Empty, "ui_denied");
                return false;
            }

            if (CraftingNetwork.TryFetchForRepair(xui.PlayerInventory, kit, want)) return true;
            // Host/single-player: the withdraw already happened. If the backpack couldn't hold all of
            // it, repair with what is really carried now instead of asking for another click forever.
            if (server) return true;

            // Joined player: the kits are on their way. Ask for another click; nothing was repaired.
            pendingKit = kit.type;
            pendingUntil = Time.unscaledTime + PendingSeconds;
            if (player != null) GameManager.ShowTooltip(player, Localization.Get("stashnetFetchingRepair"), string.Empty, "ui_denied");
            return false;
        }

        public static void Finalizer() => CraftingNetwork.SuppressStock = false;

        private static ItemValue RepairedItem(ItemActionEntryRepair entry)
        {
            if (entry.ItemController is XUiC_EquipmentStack equipment) return equipment.ItemStack.IsEmpty() ? null : equipment.ItemStack.itemValue;
            if (entry.ItemController is IItemStackController stack) return stack.ItemStack.IsEmpty() ? null : stack.ItemStack.itemValue;
            return null;
        }
    }
}
