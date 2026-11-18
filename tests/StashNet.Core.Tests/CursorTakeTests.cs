using System.Collections.Generic;
using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class CursorTakeTests
    {
        static readonly ItemAmount Pick = new ItemAmount("toolPickaxeIron", "v1", 1);
        static readonly ItemAmount Wood = new ItemAmount("resourceWood", null, 500);
        static readonly ItemAmount Stone = new ItemAmount("resourceRockSmall", null, 30);

        [Fact]
        public void FirstEntryGoesOnCursor()
        {
            var (cursor, bag) = CursorTake.Split(new List<ItemAmount> { Pick }, cursorFree: true);
            Assert.Equal(Pick, cursor);
            Assert.Empty(bag);
        }

        [Fact]
        public void OnlyFirstEntryGoesOnCursor()
        {
            var (cursor, bag) = CursorTake.Split(new List<ItemAmount> { Wood, Pick, Stone }, cursorFree: true);
            Assert.Equal(Wood, cursor);
            Assert.Equal(new List<ItemAmount> { Pick, Stone }, bag);
        }

        [Fact]
        public void CursorTakenGoesAllToBag()
        {
            // The player picked something else up before the server answered: nothing may be overwritten.
            var (cursor, bag) = CursorTake.Split(new List<ItemAmount> { Wood, Pick }, cursorFree: false);
            Assert.Null(cursor);
            Assert.Equal(new List<ItemAmount> { Wood, Pick }, bag);
        }

        [Fact]
        public void EmptyGiveGivesNothing()
        {
            var (cursor, bag) = CursorTake.Split(new List<ItemAmount>(), cursorFree: true);
            Assert.Null(cursor);
            Assert.Empty(bag);
        }

        [Fact]
        public void ZeroCountEntriesSkipped()
        {
            var none = new ItemAmount("resourceWood", null, 0);
            var (cursor, bag) = CursorTake.Split(new List<ItemAmount> { none, Pick }, cursorFree: true);
            Assert.Equal(Pick, cursor);
            Assert.Empty(bag);
        }
    }
}
