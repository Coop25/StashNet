using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class TerminalPagingTests
    {
        [Fact]
        public void PageOfIndex()
        {
            Assert.Equal(0, TerminalPaging.PageOf(0, 88));
            Assert.Equal(0, TerminalPaging.PageOf(87, 88));
            Assert.Equal(1, TerminalPaging.PageOf(88, 88));
            Assert.Equal(2, TerminalPaging.PageOf(200, 88));
        }

        [Fact]
        public void NotShownKeepsThePage()
        {
            Assert.Null(TerminalPaging.PageOf(-1, 88));
            Assert.Null(TerminalPaging.PageOf(5, 0));
        }
    }
}
