using System;
using System.Globalization;

namespace StashNet.Core.Drives
{
    /// <summary>How full a drive looks in the UI: Low below 75%, High from 75%, Full when used >= max.</summary>
    public enum FillBand { Empty, Low, High, Full, Unreadable }

    /// <summary>
    /// What a drive's fill bar and percentage show (2026-11-14). Display only: computed from the drive
    /// text, never stored.
    /// </summary>
    public readonly struct DriveFill
    {
        private DriveFill(int used, int max, int types, FillBand band, float fraction, string percentText)
        {
            Used = used;
            Max = max;
            Types = types;
            Band = band;
            Fraction = fraction;
            PercentText = percentText;
        }

        public int Used { get; }
        public int Max { get; }
        public int Types { get; }
        public FillBand Band { get; }

        /// <summary>Bar fill, 0..1 (an over-full drive shows a full bar).</summary>
        public float Fraction { get; }

        /// <summary>"" when empty, "?" when unreadable, otherwise e.g. "66%" (at least "1%", can exceed 100%).</summary>
        public string PercentText { get; }

        public static readonly DriveFill Empty = new DriveFill(0, 0, 0, FillBand.Empty, 0f, "");

        public static DriveFill FromText(string driveText, int maxStacks, Func<string, int> stackSizeOf)
        {
            if (string.IsNullOrEmpty(driveText)) return Empty;
            DriveContents contents;
            try
            {
                contents = DriveSerializer.Deserialize(driveText);
            }
            catch (DriveFormatException)
            {
                return new DriveFill(0, maxStacks, 0, FillBand.Unreadable, 0f, "?");
            }
            if (contents.TypeCount == 0) return Empty;

            int used = contents.UsedStacks(stackSizeOf);
            int max = Math.Max(1, maxStacks);
            int percent = Math.Max(1, (int)((long)used * 100 / max));
            FillBand band = used >= max ? FillBand.Full : percent >= 75 ? FillBand.High : FillBand.Low;
            float fraction = Math.Min(1f, (float)used / max);
            return new DriveFill(used, maxStacks, contents.TypeCount, band, fraction,
                percent.ToString(CultureInfo.InvariantCulture) + "%");
        }
    }
}
