using System.Collections.Generic;
using StashNet.Core.Drives;
using StashNet.Drives;

namespace StashNet.UI
{
    /// <summary>
    /// Slots redraw often and reading a drive parses its text, so fills are cached by the drive's data
    /// string (same text + same tier = same fill). Client-side display only.
    /// </summary>
    public static class DriveFillCache
    {
        private const int MaxEntries = 256;
        private static readonly Dictionary<(string, int), DriveFill> cache = new Dictionary<(string, int), DriveFill>();

        public static DriveFill Get(ItemValue drive)
        {
            if (drive == null || !DriveItem.TryGetCapacity(drive, out DriveCapacity capacity)) return DriveFill.Empty;
            drive.TryGetMetadata(DriveItem.MetadataKey, out string text);
            if (string.IsNullOrEmpty(text)) return DriveFill.Empty;

            var key = (text, capacity.MaxStacks);
            if (cache.TryGetValue(key, out DriveFill fill)) return fill;
            if (cache.Count >= MaxEntries) cache.Clear();
            fill = DriveFill.FromText(text, capacity.MaxStacks, ItemCodec.StackSizeOf);
            cache[key] = fill;
            return fill;
        }
    }
}
