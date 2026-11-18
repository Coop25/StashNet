using System.Collections.Generic;
using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class FavoritesTests
    {
        [Fact]
        public void ToggleAddsThenRemoves()
        {
            var f = new Favorites();
            Assert.False(f.Contains("ammo9mmBulletBall"));
            Assert.True(f.Toggle("ammo9mmBulletBall"));
            Assert.True(f.Contains("ammo9mmBulletBall"));
            Assert.False(f.Toggle("ammo9mmBulletBall"));
            Assert.False(f.Contains("ammo9mmBulletBall"));
        }

        [Fact]
        public void SerializeParseRoundTrip()
        {
            var f = new Favorites();
            f.Toggle("resourceWood");
            f.Toggle("ammo9mmBulletBall");
            Favorites back = Favorites.Parse(f.Serialize());
            Assert.True(back.Contains("resourceWood"));
            Assert.True(back.Contains("ammo9mmBulletBall"));
            Assert.False(back.Contains("resourceRockSmall"));
        }

        [Fact]
        public void ParseIgnoresBlankAndBadLines()
        {
            Assert.False(Favorites.Parse(null).Contains(""));
            Favorites f = Favorites.Parse("\n   \n# a comment\n  resourceWood  \r\n\r\n");
            Assert.True(f.Contains("resourceWood"));
            Assert.False(f.Contains("# a comment"));
            Assert.False(f.Contains(""));
        }

        [Fact]
        public void PinnedFirstKeepsOrder()
        {
            var f = new Favorites();
            f.Toggle("c");
            f.Toggle("a");
            List<string> order = Favorites.PinnedFirst(new[] { "a", "b", "c", "d" }, s => s, f);
            Assert.Equal(new List<string> { "a", "c", "b", "d" }, order);
        }
    }
}
