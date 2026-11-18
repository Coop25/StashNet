using StashNet.Core.Protocol;
using StashNet.Core.Skins;
using Xunit;

namespace StashNet.Core.Tests
{
    public class TerminalScreenRefsTests
    {
        private static readonly Pos3 A = new Pos3(1, 2, 3);
        private static readonly Pos3 B = new Pos3(-4, 5, 6);

        [Fact]
        public void TwoTvsOneCoreFreedOnlyAfterBoth()
        {
            var refs = new TerminalScreenRefs<string>();
            refs.Add("tv1", A);
            refs.Add("tv2", A);
            Assert.Equal(2, refs.TvsOf(A).Count);

            Assert.Null(refs.Remove("tv1"));
            Assert.Single(refs.TvsOf(A));
            Assert.Equal(A, refs.Remove("tv2"));
            Assert.Empty(refs.TvsOf(A));
        }

        [Fact]
        public void MovingTvFreesOldCoreIfLast()
        {
            var refs = new TerminalScreenRefs<string>();
            refs.Add("tv1", A);
            refs.Add("tv2", A);
            refs.Add("tv3", null);

            Assert.Null(refs.Move("tv1", B));      // tv2 still on A
            Assert.Equal(A, refs.Move("tv2", B));  // last TV of A left
            Assert.Null(refs.Move("tv3", B));      // came from no core
            Assert.Equal(3, refs.TvsOf(B).Count);
            Assert.Null(refs.Move("tv1", null));   // tv1 leaves B, others stay
            Assert.Equal(2, refs.TvsOf(B).Count);
            Assert.Null(refs.Move("tv2", B));      // same core: nothing freed
            Assert.Equal(B, refs.CoreOf("tv2"));
        }

        [Fact]
        public void RemoveUnknownIsNull()
        {
            var refs = new TerminalScreenRefs<string>();
            Assert.Null(refs.Remove("nobody"));
            refs.Add("tv1", null);
            Assert.Null(refs.Remove("tv1"));
            Assert.Null(refs.Remove("tv1"));
            Assert.False(refs.Contains("tv1"));
        }

        [Fact]
        public void AddingAKnownTvMovesIt()
        {
            var refs = new TerminalScreenRefs<string>();
            refs.Add("tv1", A);
            Assert.Equal(A, refs.Add("tv1", B));
            Assert.Empty(refs.TvsOf(A));
            Assert.Single(refs.TvsOf(B));
        }
    }
}
