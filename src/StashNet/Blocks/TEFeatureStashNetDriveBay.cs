using System;
using System.Collections.Generic;
using StashNet.Network;

namespace StashNet.Blocks
{
    /// <summary>
    /// Drive Bay feature (Mod/Config/blocks.xml, listed FIRST so its prompt wins). The game finds it
    /// by class name, so the name must not change and it needs a parameterless constructor.
    ///
    /// Milestone 4: the bay has no lock of its own - it defers to the network's Core:
    ///  - "Search" (open the bay) is only allowed if the local player may use the network.
    ///  - "Network code" opens the Core's keypad when the network is locked for this player.
    ///  - The bay's own vanilla lock can't be locked/coded any more ("unlock" stays, to clear
    ///    locks set before M4).
    /// </summary>
    public class TEFeatureStashNetDriveBay : TEFeatureAbs
    {
        private const string CmdNetworkCode = "netcode";

        public override void CopyFromInternal(TileEntityComposite _other)
        {
        }

        /// <summary>
        /// SERVER: tells the shared network summary when this bay's drives change outside StashNet
        /// (cheap slot-reference compare every 2 s, nothing decoded; free when no station uses a summary).
        /// </summary>
        /// <summary>SERVER: a removed bay takes its drives out of the network; its summary must be rebuilt.</summary>
        public override void OnRemove(World _world)
        {
            base.OnRemove(_world);
            if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) NetworkSummaryCache.BayRemoved(Parent.ToWorldPos());
        }

        public override void UpdateTick(World _world)
        {
            base.UpdateTick(_world);
            NetworkSummaryCache.CheckBay(_world, Parent);
            EjectNonDrives();
        }

        private float nextEjectCheck;

        /// <summary>
        /// SERVER: drives only, enforced here too (players' games already refuse other items, see
        /// Patches/DriveBayOnlyDrivesPatches.cs; this catches a modified client). Anything else is moved into
        /// a loot bag beside the bay, never deleted, and only while nobody has the bay open: an open bay's
        /// player sends their own copy of the slots back, so changing it then would duplicate the item.
        /// </summary>
        private void EjectNonDrives()
        {
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer) return;
            float now = UnityEngine.Time.time;
            if (now < nextEjectCheck) return;
            nextEjectCheck = now + 2f;

            TEFeatureStorage storage = Parent.GetFeature<TEFeatureStorage>();
            if (storage == null || StashNetNetwork.BayIsOpen(storage)) return;
            List<ItemStack> wrong = null;
            for (int i = 0; i < storage.ItemGrid.Length; i++)
            {
                ItemStack s = storage.ItemGrid[i];
                if (s == null || s.IsEmpty() || DriveRules.IsDrive(s.itemValue)) continue;
                (wrong = wrong ?? new List<ItemStack>()).Add(s.Clone());
                storage.UpdateSlot(i, ItemStack.Empty.Clone());
            }
            if (wrong == null) return;
            storage.SetModified();
            Vector3i pos = Parent.ToWorldPos();
            GameManager.Instance.DropContentInLootContainerServer(-1, "DroppedLootContainer", pos.ToVector3() + new UnityEngine.Vector3(0.5f, 1f, 0.5f), wrong.ToArray());
            Log.Out($"[StashNet] Drive Bay at {pos} held {wrong.Count} non-drive stack(s); moved them to a loot bag beside it.");
        }

        public override void InitBlockActivationCommands(Action<BlockActivationCommand, TileEntityComposite.EBlockCommandOrder, TileEntityFeatureData> _addCallback)
        {
            base.InitBlockActivationCommands(_addCallback);
            _addCallback(new BlockActivationCommand(CmdNetworkCode, "keypad", _enabled: false), TileEntityComposite.EBlockCommandOrder.First, FeatureData);
        }

        public override bool AllowBlockActivationCommand(ITileEntityFeature _module, ReadOnlySpan<char> _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _entityFocusing)
        {
            if (!base.AllowBlockActivationCommand(_module, _commandName, _world, _blockPos, _blockValue, _entityFocusing)) return false;

            if (Equals(_module) && CommandIs(_commandName, CmdNetworkCode)) return LockedForMe();
            if (_module is TEFeatureStorage && CommandIs(_commandName, "Search")) return !LockedForMe();
            if (_module is TEFeatureLockable && (CommandIs(_commandName, "lock") || CommandIs(_commandName, "keypad"))) return false;
            // Owner (2026-11-13): an empty bay can be picked up (vanilla pickup rule: inside your own land claim).
            // Picking up a bay with drives in it would destroy them, so "Take" only shows when it's empty.
            if (_module is TEFeaturePickup && CommandIs(_commandName, "Take")) return !LockedForMe() && IsEmpty(Parent);
            return true;
        }

        /// <summary>True if the bay's storage has no items (also used by the pickup guard patch).</summary>
        public static bool IsEmpty(TileEntityComposite bay)
        {
            TEFeatureStorage storage = bay?.GetFeature<TEFeatureStorage>();
            if (storage?.ItemGrid == null) return true;
            for (int i = 0; i < storage.ItemGrid.Length; i++)
                if (storage.ItemGrid[i] != null && !storage.ItemGrid[i].IsEmpty()) return false;
            return true;
        }

        public override bool OnBlockActivated(ReadOnlySpan<char> _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
        {
            if (CommandIs(_commandName, CmdNetworkCode))
            {
                CoreLookupCache.LockedForLocalPlayer(GameManager.Instance.World, ToWorldPos(), out TEFeatureLockable coreLock);
                LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(_player);
                if (coreLock != null && ui != null) XUiC_KeypadWindow.Open(ui, coreLock);
                return true;
            }
            return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
        }

        public override string GetActivationText(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityAlive _entityFocusing, string _activateHotkeyMarkup, string _focusedTileEntityName)
        {
            if (!LockedForMe()) return null; // fall through to the storage's normal prompt
            return string.Format(Localization.Get("stashnetBayLocked"), _activateHotkeyMarkup, _focusedTileEntityName);
        }

        private bool LockedForMe() => CoreLookupCache.LockedForLocalPlayer(GameManager.Instance.World, ToWorldPos(), out _);
    }
}
