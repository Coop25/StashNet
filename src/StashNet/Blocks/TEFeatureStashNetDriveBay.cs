namespace StashNet.Blocks
{
    /// <summary>
    /// Marker feature on the Drive Bay's composite tile entity. Referenced by class name from
    /// Mod/Config/blocks.xml (property class="TEFeatureStashNetDriveBay"); the game finds it by
    /// reflection, so the name must not change and it needs a parameterless constructor.
    /// Later milestones hang network behavior off this feature.
    /// </summary>
    public class TEFeatureStashNetDriveBay : TEFeatureAbs
    {
        public override void CopyFromInternal(TileEntityComposite _other)
        {
        }
    }
}
