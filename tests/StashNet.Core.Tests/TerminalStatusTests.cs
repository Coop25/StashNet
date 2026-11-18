using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class TerminalStatusTests
    {
        const string Loading = "Connecting to storage...";

        [Fact]
        public void FirstListingClearsTheLoadingText()
        {
            // The first answer is a background listing with nothing to say: "Connecting..." must not stay forever.
            Assert.Equal("", TerminalStatus.Next(Loading, Loading, hasListing: true, ownAction: false, messages: ""));
        }

        [Fact]
        public void OwnActionAlwaysSetsTheMessage()
        {
            Assert.Equal("", TerminalStatus.Next("Backpack full", Loading, hasListing: true, ownAction: true, messages: ""));
            Assert.Equal("Done", TerminalStatus.Next("", Loading, hasListing: false, ownAction: true, messages: "Done"));
        }

        [Fact]
        public void BackgroundRefreshKeepsAnEarlierMessage()
        {
            Assert.Equal("Backpack full", TerminalStatus.Next("Backpack full", Loading, hasListing: true, ownAction: false, messages: ""));
        }

        [Fact]
        public void BackgroundRefreshWithAMessageShowsIt()
        {
            Assert.Equal("No Core in reach", TerminalStatus.Next(Loading, Loading, hasListing: false, ownAction: false, messages: "No Core in reach"));
        }

        [Fact]
        public void LoadingStaysUntilAListingArrives()
        {
            Assert.Equal(Loading, TerminalStatus.Next(Loading, Loading, hasListing: false, ownAction: false, messages: ""));
        }
    }
}
