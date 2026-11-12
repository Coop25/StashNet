namespace StashNet.Blocks
{
    /// <summary>What counts as a drive, and what counts as a Drive Bay.</summary>
    public static class DriveRules
    {
        /// <summary>Item tag on every drive tier (see Mod/Config/items.xml).</summary>
        public const string DriveTag = "stashnetDrive";

        private static FastTags<TagGroup.Global> driveTags;
        private static bool driveTagsInit;

        public static bool IsDrive(ItemValue itemValue)
        {
            ItemClass itemClass = itemValue?.ItemClass;
            if (itemClass == null) return false;
            if (!driveTagsInit)
            {
                driveTags = FastTags<TagGroup.Global>.Parse(DriveTag);
                driveTagsInit = true;
            }
            return itemClass.HasAnyTags(driveTags);
        }

        public static bool IsDriveBay(TEFeatureStorage storage)
        {
            return storage?.Parent?.GetFeature<TEFeatureStashNetDriveBay>() != null;
        }

        /// <summary>
        /// True when the local player currently has a Drive Bay open. Also requires the looting
        /// window to be open: xui.LootContainer may keep pointing at the last container after it
        /// closes, and that must not affect vehicle storage or other windows.
        /// </summary>
        public static bool OpenContainerIsDriveBay()
        {
            LocalPlayerUI ui = LocalPlayerUI.GetUIForPrimaryPlayer();
            if (ui == null || ui.xui == null || ui.windowManager == null) return false;
            return ui.windowManager.IsWindowOpen(XUiC_LootWindowGroup.ID) && IsDriveBay(ui.xui.LootContainer);
        }
    }
}
