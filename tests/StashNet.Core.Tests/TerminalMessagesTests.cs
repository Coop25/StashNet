using System;
using System.IO;
using System.Linq;
using StashNet.Core.Protocol;
using Xunit;

namespace StashNet.Core.Tests
{
    public class TerminalMessagesTests
    {
        private static TerminalRequest RoundTrip(TerminalRequest r)
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) TerminalCodec.Write(w, r);
                ms.Position = 0;
                using (var rd = new BinaryReader(ms)) return TerminalCodec.ReadRequest(rd);
            }
        }

        private static TerminalResponse RoundTrip(TerminalResponse r)
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) TerminalCodec.Write(w, r);
                ms.Position = 0;
                using (var rd = new BinaryReader(ms)) return TerminalCodec.ReadResponse(rd);
            }
        }

        [Theory]
        [InlineData(TerminalOp.List)]
        [InlineData(TerminalOp.Withdraw)]
        [InlineData(TerminalOp.Deposit)]
        [InlineData(TerminalOp.Return)]
        [InlineData(TerminalOp.DriveUnload)]
        [InlineData(TerminalOp.SetStationOutput)]
        public void Request_RoundTrips(TerminalOp op)
        {
            var r = new TerminalRequest { Op = op, X = -12, Y = 64, Z = 3000, RequestId = 42 };
            r.Items.Add(new ItemAmount("resourceWood", null, 250));
            r.Items.Add(new ItemAmount("gunPistol", "AAEC==", 1));

            var back = RoundTrip(r);

            Assert.Equal(op, back.Op);
            Assert.Equal((-12, 64, 3000, 42), (back.X, back.Y, back.Z, back.RequestId));
            Assert.Equal(r.Items, back.Items);
            Assert.Null(back.Items[0].Variant);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Request_Flag_RoundTrips(bool flag)
        {
            var back = RoundTrip(new TerminalRequest { Op = TerminalOp.SetStationOutput, Flag = flag });
            Assert.Equal(flag, back.Flag);
        }

        [Theory]
        [InlineData(StationOutputState.NotAStation)]
        [InlineData(StationOutputState.On)]
        [InlineData(StationOutputState.Off)]
        public void Response_StationOutput_RoundTrips(StationOutputState state)
        {
            var back = RoundTrip(new TerminalResponse { RequestId = 7, StationOutput = state });
            Assert.Equal(state, back.StationOutput);
        }

        [Fact]
        public void Request_EmptyItems_RoundTrips()
        {
            var back = RoundTrip(new TerminalRequest { Op = TerminalOp.List });
            Assert.Empty(back.Items);
        }

        [Fact]
        public void Response_RoundTrips()
        {
            var r = new TerminalResponse
            {
                RequestId = 7,
                HasListing = true,
                DistinctItems = 2,
                UsedStacks = 5,
                MaxStacks = 72,
                Drives = 3,
            };
            r.Listing.Add(new ItemAmount("resourceWood", null, 9000));
            r.Listing.Add(new ItemAmount("gunPistol", "v", 1));
            r.Give.Add(new ItemAmount("resourceWood", null, 500));
            r.ReturnToDrive.Add(new ItemAmount("resourceIron", null, 40));
            r.ReturnToDrive.Add(new ItemAmount("gunRifle", "mods", 1));
            r.Messages.Add(new TerminalMessage("stashnetSkippedBays", 2));
            r.Messages.Add(new TerminalMessage("stashnetNetworkFull", -1));

            var back = RoundTrip(r);

            Assert.Equal(7, back.RequestId);
            Assert.True(back.HasListing);
            Assert.Equal((2, 5, 72, 3), (back.DistinctItems, back.UsedStacks, back.MaxStacks, back.Drives));
            Assert.Equal(r.Listing, back.Listing);
            Assert.Equal(r.Give, back.Give);
            Assert.Equal(r.ReturnToDrive, back.ReturnToDrive);
            Assert.Equal(r.Messages, back.Messages);
        }

        [Fact]
        public void Response_WithoutListing_RoundTrips()
        {
            var back = RoundTrip(new TerminalResponse { RequestId = 1 });
            Assert.False(back.HasListing);
            Assert.Empty(back.Listing);
            Assert.Empty(back.Give);
            Assert.Empty(back.ReturnToDrive);
            Assert.Empty(back.Messages);
        }

        [Fact]
        public void OpAfterLast_Throws()
        {
            var bytes = Serialize(new TerminalRequest { Op = TerminalOp.List });
            bytes[0] = (byte)TerminalOp.SetStationOutput + 1;
            Assert.Throws<FormatException>(() => Read(bytes));
        }

        [Fact]
        public void BadOp_Throws()
        {
            var bytes = Serialize(new TerminalRequest { Op = TerminalOp.List });
            bytes[0] = 99; // op is the first byte
            Assert.Throws<FormatException>(() => Read(bytes));
        }

        [Fact]
        public void NonPositiveCount_Throws()
        {
            var r = new TerminalRequest { Op = TerminalOp.Deposit };
            r.Items.Add(new ItemAmount("wood", null, 0));
            Assert.Throws<FormatException>(() => Read(Serialize(r)));
        }

        [Fact]
        public void OversizedList_Throws()
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true))
                {
                    w.Write((byte)TerminalOp.Deposit);
                    w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                    w.Write(TerminalCodec.MaxListLength + 1);
                }
                Assert.Throws<FormatException>(() => Read(ms.ToArray()));
            }
        }

        [Fact]
        public void EmptyName_Throws()
        {
            var r = new TerminalRequest { Op = TerminalOp.Deposit };
            r.Items.Add(new ItemAmount("", null, 1));
            Assert.Throws<FormatException>(() => Read(Serialize(r)));
        }

        private static byte[] Serialize(TerminalRequest r)
        {
            using (var ms = new MemoryStream())
            {
                using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, true)) TerminalCodec.Write(w, r);
                return ms.ToArray();
            }
        }

        private static TerminalRequest Read(byte[] bytes)
        {
            using (var rd = new BinaryReader(new MemoryStream(bytes))) return TerminalCodec.ReadRequest(rd);
        }
    }
}
