using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class NetworkStatusTests
    {
        private static TerminalMessage M(string key, int arg) => new TerminalMessage(key, arg);

        [Fact]
        public void NoBaysAnywhere_SaysNoBays() =>
            Assert.Equal(new[] { M("stashnetNoBays", -1) }, NetworkStatus.Messages(0, 0, 0, 0, 0));

        /// <summary>Review finding (M8): bays in reach that belong to a nearer Core must not read as "no bays".</summary>
        [Fact]
        public void BaysBelongToNearerCore_SaysSo() =>
            Assert.Equal(new[] { M("stashnetBaysOtherCore", 3) }, NetworkStatus.Messages(0, 3, 0, 0, 0));

        [Fact]
        public void BaysButNoDrives_SaysNoDrives() =>
            Assert.Equal(new[] { M("stashnetNoDrives", -1) }, NetworkStatus.Messages(2, 0, 0, 0, 0));

        [Fact]
        public void OwnBaysWithDrives_NoNoteAboutOtherCores() =>
            Assert.Empty(NetworkStatus.Messages(2, 3, 5, 0, 0));

        [Fact]
        public void SkippedBaysAndDrives_Listed() =>
            Assert.Equal(new[] { M("stashnetSkippedBays", 1), M("stashnetSkippedDrives", 2) }, NetworkStatus.Messages(2, 0, 5, 1, 2));
    }
}
