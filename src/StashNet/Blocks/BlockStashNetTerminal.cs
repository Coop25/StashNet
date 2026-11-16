using StashNet.Network;
using StashNet.UI;

namespace StashNet.Blocks
{
    /// <summary>
    /// Storage Terminal block. XML: Class="StashNet.Blocks.StashNetTerminal, StashNet"
    /// (the game inserts the "Block" prefix before the class name).
    /// E = open the terminal (or, if the network is locked for you, the Core's keypad);
    /// hold E = radial with "network code" and "pick up". The server re-checks access on every request.
    /// </summary>
    public class BlockStashNetTerminal : Block
    {
        private const float PickupDelay = 1f;

        public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
        {
            base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
            Skins.SkinApplier.Apply(this, _ebcd);
        }

        public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            string key = CoreLookupCache.LockedForLocalPlayer(GameManager.Instance.World, _blockPos, out _) ? "stashnetTerminalLocked" : "stashnetUseTerminal";
            return _blockValue.Block.GetLocalizedBlockName() + "\n" + Localization.Get(key);
        }

        public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing) => true;

        public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            bool locked = CoreLookupCache.LockedForLocalPlayer(GameManager.Instance.World, _blockPos, out _);
            return new[]
            {
                new BlockActivationCommand("open", "search", _enabled: true),
                new BlockActivationCommand("netcode", "keypad", _enabled: locked),
                new BlockActivationCommand("take", "hand", _enabled: InMyLandClaim(_world, _blockPos)),
            };
        }

        public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
        {
            switch (_commandName)
            {
                case "open":
                    return OnBlockActivated(_world, _blockPos, _blockValue, _player);
                case "netcode":
                    OpenCoreKeypad(_player, _blockPos);
                    return true;
                case "take":
                    if (!InMyLandClaim(_world, _blockPos)) return false;
                    takeItemWithTimer(_blockPos, _blockValue, _player, PickupDelay);
                    return true;
                default:
                    return false;
            }
        }

        public override bool OnBlockActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
        {
            if (!VersionCheck.ClientMayUse(out string versionProblem))
            {
                GameManager.ShowTooltip(_player, versionProblem, string.Empty, "ui_denied");
                return true;
            }
            if (CoreLookupCache.LockedForLocalPlayer(GameManager.Instance.World, _blockPos, out _))
            {
                GameManager.ShowTooltip(_player, Localization.Get("stashnetNetworkLocked"), string.Empty, "ui_denied");
                OpenCoreKeypad(_player, _blockPos);
                return true;
            }
            XUiC_StashNetTerminal.Open(_player, _blockPos);
            return true;
        }

        /// <summary>Owner (2026-11-13): like every StashNet block (vanilla workbench rule), pick up only inside your own land claim.</summary>
        private static bool InMyLandClaim(WorldBase world, Vector3i pos) =>
            world.IsMyLandProtectedBlock(pos, world.GetGameManager().GetPersistentLocalPlayer());

        private static void OpenCoreKeypad(EntityPlayerLocal player, Vector3i terminalPos)
        {
            CoreLookupCache.LockedForLocalPlayer(GameManager.Instance.World, terminalPos, out TEFeatureLockable coreLock);
            LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(player);
            if (coreLock != null && ui != null) XUiC_KeypadWindow.Open(ui, coreLock);
        }
    }
}
