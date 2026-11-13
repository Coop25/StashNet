using System.Linq;
using StashNet.Core.Drives;
using Xunit;

namespace StashNet.Core.Tests
{
    public class DriveSerializerTests
    {
        private static readonly DriveCapacity Big = new DriveCapacity(100000);
        private static readonly System.Func<string, int> One = _ => 1;

        [Fact]
        public void RoundTrip_PreservesEntriesAndOrder()
        {
            var d = new DriveContents();
            d.TryInsert(Big, One, "resourceWood", null, 250);
            d.TryInsert(Big, One, "gunPistol", "gunPistol,modGunScope|AAECAw==", 1);
            d.TryInsert(Big, One, "weird", "tab\there\nnewline\\backslash ünïcödé", 3);
            d.TryInsert(Big, One, "resourceIron", null, 7);
            d.TryInsert(Big, One, "dashVariant", "-", 2);
            d.TryInsert(Big, One, "emptyVariant", "", 4);

            var back = DriveSerializer.Deserialize(DriveSerializer.Serialize(d));

            Assert.Equal(
                d.Entries.Select(e => (e.Name, e.Variant, e.Count)),
                back.Entries.Select(e => (e.Name, e.Variant, e.Count)));
        }

        [Fact]
        public void Serialize_StartsWithHeader()
        {
            Assert.StartsWith("SND1", DriveSerializer.Serialize(new DriveContents()));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void NullOrEmpty_GivesEmptyDrive(string text)
        {
            Assert.Equal(0, DriveSerializer.Deserialize(text).TypeCount);
        }

        [Fact]
        public void EmptyDrive_RoundTrips()
        {
            var back = DriveSerializer.Deserialize(DriveSerializer.Serialize(new DriveContents()));
            Assert.Equal(0, back.TypeCount);
        }

        [Fact]
        public void BadHeader_Throws()
        {
            Assert.Throws<DriveFormatException>(() => DriveSerializer.Deserialize("XYZ9\nresourceWood\t5\t-"));
        }

        [Theory]
        [InlineData("SND1\nresourceWood\tabc\t-")]
        [InlineData("SND1\nresourceWood\t-3\t-")]
        [InlineData("SND1\nresourceWood\t0\t-")]
        public void BadCount_Throws(string text)
        {
            Assert.Throws<DriveFormatException>(() => DriveSerializer.Deserialize(text));
        }

        [Theory]
        [InlineData("SND1\nresourceWood\t5")]
        [InlineData("SND1\nresourceWood")]
        [InlineData("SND1\n\t5\t-")]
        public void TruncatedOrMissingName_Throws(string text)
        {
            Assert.Throws<DriveFormatException>(() => DriveSerializer.Deserialize(text));
        }

        [Fact]
        public void DuplicateEntries_AreMerged()
        {
            var d = DriveSerializer.Deserialize("SND1\nresourceWood\t5\t-\nresourceWood\t7\t-");
            Assert.Equal(1, d.TypeCount);
            Assert.Equal(12, d.CountOf("resourceWood", null));
        }

        // M8 scrap guard: only a drive that is certainly empty may be scrapped.
        [Fact]
        public void HoldsItems_Null_False() => Assert.False(DriveSerializer.HoldsItems(null));

        [Fact]
        public void HoldsItems_Empty_False() => Assert.False(DriveSerializer.HoldsItems(""));

        [Fact]
        public void HoldsItems_HeaderOnly_False() =>
            Assert.False(DriveSerializer.HoldsItems(DriveSerializer.Serialize(new DriveContents())));

        [Fact]
        public void HoldsItems_WithItem_True()
        {
            var d = new DriveContents();
            d.TryInsert(Big, One, "resourceWood", null, 5);
            Assert.True(DriveSerializer.HoldsItems(DriveSerializer.Serialize(d)));
        }

        /// <summary>Unreadable data might be someone's items: never treat it as empty.</summary>
        [Fact]
        public void HoldsItems_Unreadable_True() => Assert.True(DriveSerializer.HoldsItems("not a drive"));
    }
}
