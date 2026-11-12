using StashNet.Core.Drives;
using Xunit;

namespace StashNet.Core.Tests
{
    public class DriveContentsTests
    {
        private static readonly DriveCapacity Basic = new DriveCapacity(15, 1500);

        [Fact]
        public void Insert_IntoEmpty_AcceptsAll()
        {
            var d = new DriveContents();
            Assert.Equal(100, d.TryInsert(Basic, "resourceWood", null, 100));
            Assert.Equal(1, d.TypeCount);
            Assert.Equal(100, d.TotalCount);
            Assert.Equal(100, d.CountOf("resourceWood", null));
        }

        [Fact]
        public void Insert_SameTypeTwice_MergesIntoOneEntry()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, "resourceWood", null, 100);
            d.TryInsert(Basic, "resourceWood", null, 50);
            Assert.Equal(1, d.TypeCount);
            Assert.Equal(150, d.CountOf("resourceWood", null));
        }

        [Fact]
        public void Insert_RespectsMaxItems()
        {
            var d = new DriveContents();
            Assert.Equal(1400, d.TryInsert(Basic, "resourceWood", null, 1400));
            Assert.Equal(100, d.TryInsert(Basic, "resourceRockSmall", null, 200));
            Assert.Equal(1500, d.TotalCount);
            Assert.Equal(0, d.TryInsert(Basic, "resourceWood", null, 1));
        }

        [Fact]
        public void Insert_NewType_WhenTypesFull_ReturnsZero()
        {
            var cap = new DriveCapacity(2, 1500);
            var d = new DriveContents();
            d.TryInsert(cap, "a", null, 1);
            d.TryInsert(cap, "b", null, 1);
            Assert.Equal(0, d.TryInsert(cap, "c", null, 5));
            Assert.Equal(2, d.TypeCount);
            Assert.Equal(0, d.CountOf("c", null));
        }

        [Fact]
        public void Insert_ExistingType_WhenTypesFull_StillAccepted()
        {
            var cap = new DriveCapacity(2, 1500);
            var d = new DriveContents();
            d.TryInsert(cap, "a", null, 1);
            d.TryInsert(cap, "b", null, 1);
            Assert.Equal(10, d.TryInsert(cap, "a", null, 10));
            Assert.Equal(11, d.CountOf("a", null));
        }

        [Fact]
        public void Insert_SameNameDifferentVariant_IsSeparateType()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, "gunPistol", null, 1);
            d.TryInsert(Basic, "gunPistol", "v1", 1);
            d.TryInsert(Basic, "gunPistol", "v2", 1);
            d.TryInsert(Basic, "gunPistol", "v1", 1);
            Assert.Equal(3, d.TypeCount);
            Assert.Equal(2, d.CountOf("gunPistol", "v1"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Insert_ZeroOrNegative_ReturnsZero(int count)
        {
            var d = new DriveContents();
            Assert.Equal(0, d.TryInsert(Basic, "resourceWood", null, count));
            Assert.Equal(0, d.TypeCount);
        }

        [Fact]
        public void Extract_Partial_KeepsEntry()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, "resourceWood", null, 100);
            Assert.Equal(30, d.Extract("resourceWood", null, 30));
            Assert.Equal(70, d.CountOf("resourceWood", null));
            Assert.Equal(1, d.TypeCount);
        }

        [Fact]
        public void Extract_All_RemovesEntry()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, "resourceWood", null, 100);
            Assert.Equal(100, d.Extract("resourceWood", null, 100));
            Assert.Equal(0, d.TypeCount);
            Assert.Equal(0, d.TotalCount);
        }

        [Fact]
        public void Extract_MoreThanStored_ReturnsStored()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, "resourceWood", null, 10);
            Assert.Equal(10, d.Extract("resourceWood", null, 50));
            Assert.Equal(0, d.TypeCount);
        }

        [Fact]
        public void Extract_Missing_ReturnsZero()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, "resourceWood", null, 10);
            Assert.Equal(0, d.Extract("resourceIron", null, 5));
            Assert.Equal(0, d.Extract("resourceWood", "variant", 5));
            Assert.Equal(10, d.TotalCount);
        }

        [Fact]
        public void OverCapacity_ExistingItemsKept_InsertRefused()
        {
            var d = new DriveContents();
            d.TryInsert(Basic, "a", null, 1000);
            d.TryInsert(Basic, "b", null, 500);
            var smaller = new DriveCapacity(1, 100);
            Assert.Equal(0, d.TryInsert(smaller, "a", null, 1));
            Assert.Equal(0, d.TryInsert(smaller, "c", null, 1));
            Assert.Equal(2, d.TypeCount);
            Assert.Equal(1500, d.TotalCount);
        }
    }
}
