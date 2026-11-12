using StashNet.Blocks;
using StashNet.Core.Drives;

namespace StashNet.Drives
{
    /// <summary>Reads and writes a drive's contents on the drive item itself (item metadata).</summary>
    public static class DriveItem
    {
        public const string MetadataKey = "StashNetDrive";
        public const string PropMaxStacks = "StashNetMaxStacks";

        public static bool TryGetCapacity(ItemValue drive, out DriveCapacity capacity)
        {
            capacity = default;
            ItemClass ic = drive?.ItemClass;
            if (ic == null || !DriveRules.IsDrive(drive)) return false;
            if (!ic.Properties.Contains(PropMaxStacks)) return false;
            capacity = new DriveCapacity(ic.Properties.GetInt(PropMaxStacks));
            return true;
        }

        /// <summary>
        /// Loads contents. On failure, the caller must not modify or save the drive: unreadable
        /// data is left exactly as it is.
        /// </summary>
        public static bool TryLoad(ItemValue drive, out DriveContents contents, out string error)
        {
            contents = null;
            error = null;
            if (!DriveRules.IsDrive(drive))
            {
                error = "That isn't a drive.";
                return false;
            }
            drive.TryGetMetadata(MetadataKey, out string text);
            try
            {
                contents = DriveSerializer.Deserialize(text);
                return true;
            }
            catch (DriveFormatException e)
            {
                error = "This drive's data can't be read (" + e.Message + "). It has been left untouched.";
                Log.Warning("[StashNet] " + error);
                return false;
            }
        }

        /// <summary>
        /// Lock stamp written by v0.5.0 only. Drives are no longer locked to networks (owner's
        /// decision, 2026-11-12); leftover stamps are ignored and cleared on the next save.
        /// </summary>
        public const string LegacyStampKey = "StashNetLock";

        public static bool HasStamp(ItemValue drive) => drive != null && drive.HasMetadata(LegacyStampKey);

        public static void ClearStamp(ItemValue drive) => drive.RemoveMetaData(LegacyStampKey);

        public static void Save(ItemValue drive, DriveContents contents)
        {
            if (contents.TypeCount == 0)
            {
                drive.RemoveMetaData(MetadataKey);
                return;
            }
            drive.SetMetadata(MetadataKey, DriveSerializer.Serialize(contents));
        }
    }
}
