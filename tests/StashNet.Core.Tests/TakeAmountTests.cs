using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class TakeAmountTests
    {
        [Theory]
        [InlineData(37, 500, 37)]
        [InlineData(600, 500, 500)]
        [InlineData(0, 500, 1)]
        [InlineData(-3, 500, 1)]
        [InlineData(5, 0, 0)]
        [InlineData(1, 1, 1)]
        public void Clamp(int requested, int stored, int expected)
        {
            Assert.Equal(expected, TakeAmount.Clamp(requested, stored));
        }

        [Theory]
        [InlineData(500, 64, 64)]
        [InlineData(20, 64, 20)]
        [InlineData(6, 1, 1)]
        [InlineData(0, 64, 0)]
        public void Stack(int stored, int stackSize, int expected)
        {
            Assert.Equal(expected, TakeAmount.Stack(stored, stackSize));
        }
    }
}
