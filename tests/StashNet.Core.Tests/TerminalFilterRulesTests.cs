using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class TerminalFilterRulesTests
    {
        [Fact]
        public void TypingSwitchesToAll()
        {
            Assert.True(TerminalFilterRules.SearchSwitchesToAll("iron", fromCode: false));
        }

        [Fact]
        public void ClearingOrCodeChangesKeepTheTab()
        {
            Assert.False(TerminalFilterRules.SearchSwitchesToAll("", fromCode: false));
            Assert.False(TerminalFilterRules.SearchSwitchesToAll(null, fromCode: false));
            Assert.False(TerminalFilterRules.SearchSwitchesToAll("iron", fromCode: true));
        }

        [Fact]
        public void PickingAnotherTabClearsSearch()
        {
            Assert.True(TerminalFilterRules.TabClearsSearch("Tools", "iron"));
        }

        [Fact]
        public void AllTabOrEmptySearchLeavesSearch()
        {
            Assert.False(TerminalFilterRules.TabClearsSearch("", "iron"));
            Assert.False(TerminalFilterRules.TabClearsSearch(null, "iron"));
            Assert.False(TerminalFilterRules.TabClearsSearch("Tools", ""));
        }
    }
}
