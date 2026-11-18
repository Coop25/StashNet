using System;
using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class FavoritesFileTests
    {
        [Fact]
        public void LoadsOnceAndSavesEachToggle()
        {
            int reads = 0;
            string saved = null;
            var file = new FavoritesFile(() => { reads++; return "resourceWood\n"; }, text => saved = text);
            Assert.True(file.Current.Contains("resourceWood"));
            Assert.True(file.Toggle("ammo9mmBulletBall"));
            Assert.Equal(1, reads);
            Assert.Contains("ammo9mmBulletBall", saved);
            Assert.Contains("resourceWood", saved);
        }

        [Fact]
        public void MissingFileMeansNoPins()
        {
            var file = new FavoritesFile(() => null, _ => { });
            Assert.False(file.Current.Contains("resourceWood"));
        }

        [Fact]
        public void UnreadableFileIsNeverOverwritten()
        {
            // A locked file (antivirus, cloud sync) must not be replaced by just the one new pin.
            bool wrote = false;
            var file = new FavoritesFile(() => throw new UnauthorizedAccessException("locked"), _ => wrote = true);
            Assert.True(file.Toggle("resourceWood"));   // the pin still works this session
            Assert.True(file.Current.Contains("resourceWood"));
            Assert.False(wrote);
            Assert.NotNull(file.Problem);
        }

        [Fact]
        public void SaveFailureKeepsThePinInMemory()
        {
            var file = new FavoritesFile(() => "", _ => throw new System.IO.IOException("disk full"));
            Assert.True(file.Toggle("resourceWood"));
            Assert.True(file.Current.Contains("resourceWood"));
            Assert.NotNull(file.Problem);
        }
    }
}
