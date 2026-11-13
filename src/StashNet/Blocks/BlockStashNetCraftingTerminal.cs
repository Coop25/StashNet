using StashNet.Skins;

namespace StashNet.Blocks
{
    /// <summary>
    /// Crafting Terminal. XML: Class="StashNet.Blocks.StashNetCraftingTerminal, StashNet". Still a vanilla
    /// workstation in every way (the crafting hooks find it by its StashNetCraftingTerminal property, not by
    /// class); this class only lets the salvage look (and sn skin dump) reach its model.
    /// </summary>
    public class BlockStashNetCraftingTerminal : BlockWorkstation
    {
        public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
        {
            base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
            SkinApplier.Apply(this, _ebcd);
        }
    }
}
