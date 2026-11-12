using StashNet.Core.Drives;
using Xunit;

namespace StashNet.Core.Tests
{
    public class DriveStampTests
    {
        private const string Hash = "1A2B3C4D";

        [Fact]
        public void Format_And_Parse_RoundTrip()
        {
            string s = DriveStamp.Format(-10, 64, 2500, Hash);
            Assert.True(DriveStamp.TryParse(s, out int x, out int y, out int z, out string hash));
            Assert.Equal((-10, 64, 2500, Hash), (x, y, z, hash));
        }

        [Theory]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData("1,2|ABC")]
        [InlineData("1,2,x|ABC")]
        [InlineData("1,2,3|")]
        public void Parse_Garbage_Fails(string s)
        {
            Assert.False(DriveStamp.TryParse(s, out _, out _, out _, out _));
        }

        [Fact]
        public void NoStamp_NoPassword_ReadableUnchanged()
        {
            var d = DriveStamp.Decide(null, 1, 2, 3, null);
            Assert.True(d.Readable);
            Assert.False(d.Changed);
        }

        [Fact]
        public void NoStamp_WithPassword_ReadableAndStamped()
        {
            var d = DriveStamp.Decide(null, 1, 2, 3, Hash);
            Assert.True(d.Readable);
            Assert.True(d.Changed);
            Assert.Equal(DriveStamp.Format(1, 2, 3, Hash), d.NewStamp);
        }

        [Fact]
        public void SameCore_PasswordChanged_ReadableAndRestamped()
        {
            var d = DriveStamp.Decide(DriveStamp.Format(1, 2, 3, "OLDHASH1"), 1, 2, 3, Hash);
            Assert.True(d.Readable);
            Assert.True(d.Changed);
            Assert.Equal(DriveStamp.Format(1, 2, 3, Hash), d.NewStamp);
        }

        [Fact]
        public void SameCore_PasswordRemoved_ReadableAndCleared()
        {
            var d = DriveStamp.Decide(DriveStamp.Format(1, 2, 3, Hash), 1, 2, 3, null);
            Assert.True(d.Readable);
            Assert.True(d.Changed);
            Assert.Null(d.NewStamp);
        }

        [Fact]
        public void OtherCore_SameHash_ReadableAndRestampedToThisCore()
        {
            var d = DriveStamp.Decide(DriveStamp.Format(9, 9, 9, Hash), 1, 2, 3, Hash);
            Assert.True(d.Readable);
            Assert.True(d.Changed);
            Assert.Equal(DriveStamp.Format(1, 2, 3, Hash), d.NewStamp);
        }

        [Fact]
        public void AlreadyCorrect_ReadableUnchanged()
        {
            string s = DriveStamp.Format(1, 2, 3, Hash);
            var d = DriveStamp.Decide(s, 1, 2, 3, Hash);
            Assert.True(d.Readable);
            Assert.False(d.Changed);
        }

        [Fact]
        public void OtherCore_DifferentHash_Unreadable()
        {
            var d = DriveStamp.Decide(DriveStamp.Format(9, 9, 9, "THEIRS01"), 1, 2, 3, Hash);
            Assert.False(d.Readable);
            Assert.False(d.Changed);
        }

        [Fact]
        public void OtherCore_NetworkHasNoPassword_Unreadable()
        {
            var d = DriveStamp.Decide(DriveStamp.Format(9, 9, 9, "THEIRS01"), 1, 2, 3, null);
            Assert.False(d.Readable);
        }

        [Fact]
        public void UnparseableStamp_UnreadableAndUntouched()
        {
            var d = DriveStamp.Decide("corrupt!", 1, 2, 3, Hash);
            Assert.False(d.Readable);
            Assert.False(d.Changed);
        }
    }
}
