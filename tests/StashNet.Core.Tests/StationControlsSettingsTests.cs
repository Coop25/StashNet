using System;
using StashNet.Core.Stations;
using Xunit;

namespace StashNet.Core.Tests
{
    public class StationControlsSettingsTests
    {
        private static MeltTarget Melt(string metal, int target = 2000, string owner = "Steam_1") =>
            new MeltTarget { Metal = metal, Target = target, Owner = owner };

        [Fact]
        public void OutputOffRoundTrips()
        {
            var s = new StationSettings { OutputOn = false };
            string text = s.Serialize();
            Assert.Contains("output off\n", text);
            Assert.False(StationSettings.Parse(text).OutputOn);
        }

        [Fact]
        public void OutputOnWritesNothing()
        {
            Assert.DoesNotContain("output", new StationSettings().Serialize());
        }

        [Fact]
        public void MeltRoundTripsAndStripsOwner()
        {
            var s = new StationSettings();
            Assert.True(s.SetMelt(Melt("iron")));

            StationSettings back = StationSettings.Parse(s.Serialize());
            Assert.Single(back.Melts);
            Assert.Equal("iron", back.Melts[0].Metal);
            Assert.Equal(2000, back.Melts[0].Target);
            Assert.Equal("Steam_1", back.Melts[0].Owner);

            string client = s.Serialize(includeOwners: false);
            Assert.Contains("melt|iron|2000|", client);
            Assert.DoesNotContain("Steam_1", client);
        }

        [Fact]
        public void SeventhMeltRefused()
        {
            var s = new StationSettings();
            foreach (string m in new[] { "iron", "brass", "lead", "glass", "stone", "clay" }) Assert.True(s.SetMelt(Melt(m)));
            Assert.False(s.SetMelt(Melt("gold")));
            Assert.Equal(StationSettings.MaxMelts, s.Melts.Count);
        }

        [Fact]
        public void SetMeltReplacesSameMetal()
        {
            var s = new StationSettings();
            s.SetMelt(Melt("iron", 100));
            s.SetMelt(Melt("iron", 300));
            Assert.Single(s.Melts);
            Assert.Equal(300, s.Melts[0].Target);
            Assert.True(s.RemoveMelt("iron"));
            Assert.Empty(s.Melts);
        }

        [Fact]
        public void MeltBadTargetThrows()
        {
            Assert.Throws<FormatException>(() => StationSettings.Parse("melt|iron|0|\n"));
            Assert.Throws<FormatException>(() => StationSettings.Parse("melt|iron|abc|\n"));
            Assert.Throws<FormatException>(() => StationSettings.Parse("melt|ir on|5|\n"));
        }

        [Fact]
        public void OldFileLoadsWithDefaults()
        {
            StationSettings s = StationSettings.Parse("fuel Auto\n");
            Assert.True(s.OutputOn);
            Assert.Empty(s.Melts);
        }

        [Fact]
        public void IsDefaultFalseWhenOutputOffOrMelt()
        {
            Assert.False(new StationSettings { OutputOn = false }.IsDefault);
            var s = new StationSettings();
            s.SetMelt(Melt("clay"));
            Assert.False(s.IsDefault);
            Assert.True(new StationSettings().IsDefault);
        }

        [Fact]
        public void CloneCopiesOutputAndMelts()
        {
            var s = new StationSettings { OutputOn = false };
            s.SetMelt(Melt("lead", 50));
            StationSettings c = s.Clone();
            c.Melts[0].Target = 99;
            Assert.False(c.OutputOn);
            Assert.Equal(50, s.Melts[0].Target);
        }
    }
}
