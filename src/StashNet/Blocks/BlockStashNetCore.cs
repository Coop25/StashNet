using StashNet.Network;

namespace StashNet.Blocks
{
    /// <summary>
    /// Every Storage Core tier. XML: Class="StashNet.Blocks.StashNetCore, StashNet" (the game inserts
    /// the "Block" prefix). A normal composite block (lock, heat, pickup features), plus the owner's
    /// spacing rule (2026-11-13): a Core can't be placed within 2 x the largest Core reach of any other
    /// Core, so networks never overlap even when fully upgraded. Upgrading in place isn't a placement,
    /// so it's unaffected. Skipped in the prefab editor, like vanilla's trader-area rule.
    /// </summary>
    public class BlockStashNetCore : BlockCompositeTileEntity
    {
        private static float nextTooltip;

        public override bool CanPlaceBlockAt(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, bool _bOmitCollideCheck = false)
        {
            if (!base.CanPlaceBlockAt(_world, _blockPos, _blockValue, _bOmitCollideCheck)) return false;
            if (GameManager.Instance.IsEditMode() || !(_world is World world)) return true;
            if (!StashNetNetwork.CoreTooClose(world, _blockPos, out int distance, out int minGap)) return true;

            TellPlayer(world, distance, minGap);
            return false;
        }

        /// <summary>The game already plays its "can't build" sound; say why, at most once a second.</summary>
        private static void TellPlayer(World world, int distance, int minGap)
        {
            EntityPlayerLocal player = world.GetPrimaryPlayer();
            float now = UnityEngine.Time.time;
            if (player == null || now < nextTooltip) return;
            nextTooltip = now + 1f;
            GameManager.ShowTooltip(player, string.Format(Localization.Get("stashnetCoreTooClose"), distance, minGap), string.Empty);
        }
    }
}
