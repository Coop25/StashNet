using System;

namespace StashNet.Core.Drives
{
    /// <summary>Drive data couldn't be read. Callers must refuse the drive and never overwrite its data.</summary>
    public sealed class DriveFormatException : Exception
    {
        public DriveFormatException(string message) : base(message)
        {
        }
    }
}
