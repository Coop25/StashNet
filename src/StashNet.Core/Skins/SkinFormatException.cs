using System;

namespace StashNet.Core.Skins
{
    /// <summary>A skin file (atlas index or layouts) can't be read at all. Callers turn skins off and keep going.</summary>
    public sealed class SkinFormatException : Exception
    {
        public SkinFormatException(string message) : base(message)
        {
        }
    }
}
