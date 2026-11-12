using StashNet.UI;

namespace StashNet.Blocks
{
    /// <summary>
    /// Storage Terminal block. XML: Class="StashNet.Blocks.StashNetTerminal, StashNet"
    /// (the game inserts the "Block" prefix before the class name).
    /// E = open the terminal screen; hold E = radial with "pick up".
    /// </summary>
    public class BlockStashNetTerminal : Block
    {
        private const float PickupDelay = 1f;

        private readonly BlockActivationCommand[] commands =
        {
            new BlockActivationCommand("open", "search", _enabled: true),
            new BlockActivationCommand("take", "hand", _enabled: true),
        };

        public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
        {
            return _blockValue.Block.GetLocalizedBlockName() + "\n" + Localization.Get("stashnetUseTerminal");
        }

        public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing) => true;

        public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing) => commands;

        public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
        {
            switch (_commandName)
            {
                case "open":
                    return OnBlockActivated(_world, _blockPos, _blockValue, _player);
                case "take":
                    takeItemWithTimer(_blockPos, _blockValue, _player, PickupDelay);
                    return true;
                default:
                    return false;
            }
        }

        public override bool OnBlockActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
        {
            if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            {
                // Milestone 3a: single-player / host only. Server-side networking comes in 3b.
                GameManager.ShowTooltip(_player, Localization.Get("stashnetMpUnsupported"), string.Empty, "ui_denied");
                return true;
            }
            XUiC_StashNetTerminal.Open(_player, _blockPos);
            return true;
        }
    }
}
