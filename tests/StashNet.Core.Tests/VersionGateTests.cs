using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class VersionGateTests
    {
        [Fact]
        public void SameVersionIsCompatible() => Assert.True(VersionGate.Compatible("1.0.0", "1.0.0"));

        [Fact]
        public void SurroundingSpaceIgnored() => Assert.True(VersionGate.Compatible(" 1.0.0", "1.0.0 "));

        [Fact]
        public void AnyDifferenceIsIncompatible()
        {
            Assert.False(VersionGate.Compatible("1.0.0", "1.0.1"));
            Assert.False(VersionGate.Compatible("1.0.0", "0.23.4"));
        }

        [Fact]
        public void UnknownVersionIsIncompatible()
        {
            Assert.False(VersionGate.Compatible(null, "1.0.0"));
            Assert.False(VersionGate.Compatible("1.0.0", null));
            Assert.False(VersionGate.Compatible("", ""));
        }

        [Fact]
        public void KickReasonNamesBothVersions()
        {
            string text = VersionGate.KickReason("1.0.0", "0.23.4");
            Assert.Contains("1.0.0", text);
            Assert.Contains("0.23.4", text);
        }

        [Fact]
        public void KickReasonForAClientThatNeverSaidItsVersion() =>
            Assert.Contains("older", VersionGate.KickReason("1.0.0", null));

        [Fact]
        public void HelloTimeoutOnlyAfterTheDeadline()
        {
            Assert.False(VersionGate.TimedOut(sentAt: 100f, now: 100f + VersionGate.HelloTimeoutSeconds - 1f));
            Assert.True(VersionGate.TimedOut(sentAt: 100f, now: 100f + VersionGate.HelloTimeoutSeconds));
        }
    }
}
