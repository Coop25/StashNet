using StashNet.Skins;

namespace StashNet.Blocks
{
    /// <summary>
    /// Every Drive Bay tier. XML: Class="StashNet.Blocks.StashNetDriveBay, StashNet" (the game adds the "Block"
    /// prefix). A normal composite block (storage, lock, our drive-bay marker); this class only dresses the
    /// model in the salvage look. Saves store the tile entity, not the block class, so switching existing bays
    /// from the vanilla CompositeTileEntity class is save-safe.
    /// </summary>
    public class BlockStashNetDriveBay : BlockCompositeTileEntity
    {
        public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _bed)
        {
            base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _bed);
            SkinApplier.Apply(this, _bed);
        }
    }
}
