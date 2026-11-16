using StashNet.Core.Network;
using Xunit;

namespace StashNet.Core.Tests
{
    public class BaySlotPrintTests
    {
        private static BaySlotPrint P(int type, int count, string data) => new BaySlotPrint(type, count, data);

        [Fact]
        public void SameValuesMatchEvenWithNewStrings()
        {
            var seen = new[] { P(5, 1, "SND1 a"), P(0, 0, null) };
            var now = new[] { P(5, 1, new string("SND1 a".ToCharArray())), P(0, 0, null) };
            Assert.True(BaySlotPrint.Same(seen, now));
            Assert.Same(now[0].Data, seen[0].Data); // adopted, so the next compare is by reference
        }

        [Fact]
        public void DriveAddedRemovedOrChangedIsNoticed()
        {
            Assert.False(BaySlotPrint.Same(new[] { P(0, 0, null) }, new[] { P(5, 1, "x") }));
            Assert.False(BaySlotPrint.Same(new[] { P(5, 1, "x") }, new[] { P(0, 0, null) }));
            Assert.False(BaySlotPrint.Same(new[] { P(5, 1, "x") }, new[] { P(5, 1, "y") }));
            Assert.False(BaySlotPrint.Same(new[] { P(5, 1, "x") }, new[] { P(5, 1, "x"), P(0, 0, null) }));
        }
    }

    public class OutputMoveTests
    {
        [Fact]
        public void SkipsOnlyWhenNoFreeStacksAndNothingToTopUp()
        {
            Assert.True(OutputMove.CanSkip(freeStacks: 0, storedOfOutputItems: new[] { 0, 0 }));
            Assert.False(OutputMove.CanSkip(freeStacks: 0, storedOfOutputItems: new[] { 0, 37 }));
            Assert.False(OutputMove.CanSkip(freeStacks: 3, storedOfOutputItems: new int[0]));
        }
    }
}
