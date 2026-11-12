using System;
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
