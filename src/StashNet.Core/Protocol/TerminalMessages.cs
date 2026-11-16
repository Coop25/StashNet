using System;
using System.Collections.Generic;
using System.IO;

namespace StashNet.Core.Protocol
{
    /// <summary>What a terminal asks the server to do.</summary>
    public enum TerminalOp : byte
    {
        /// <summary>Send me the current listing.</summary>
        List = 1,
        /// <summary>Take these items out of storage and give them to me (several for a combined terminal cell).</summary>
        Withdraw = 2,
        /// <summary>I already removed these from my backpack; store them, give back what doesn't fit.</summary>
        Deposit = 3,
        /// <summary>Overflow I couldn't fit; store it, or drop it at my feet. Never sent back again.</summary>
        Return = 4,
        /// <summary>I emptied a drive in my backpack; store its contents, send leftovers back in ReturnToDrive.</summary>
        DriveUnload = 5,
        /// <summary>Network station: set the fuel mode to Text (Manual, Auto or a fuel item name).</summary>
        SetFuelMode = 6,
        /// <summary>Network station: add or replace the stock rule in Text (StockRule line).</summary>
        SetStockRule = 7,
        /// <summary>Network station: remove the stock rule for the item named in Text.</summary>
        RemoveStockRule = 8,
        /// <summary>Network station: output switch, Text "on" (finished items to storage) or "off" (stay in the station).</summary>
        SetOutput = 9,
        /// <summary>Forge: add or replace the melt target in Text (MeltTarget line).</summary>
        SetMeltTarget = 10,
        /// <summary>Forge: remove the melt target for the metal named in Text.</summary>
        RemoveMeltTarget = 11,
    }

    /// <summary>An item (by name + variant, see ItemCodec) and an amount.</summary>
    public readonly struct ItemAmount : IEquatable<ItemAmount>
    {
        public ItemAmount(string name, string variant, int count)
        {
            Name = name;
            Variant = variant;
            Count = count;
        }

        public string Name { get; }
        public string Variant { get; }
        public int Count { get; }

        public bool Equals(ItemAmount o) => Name == o.Name && Variant == o.Variant && Count == o.Count;
        public override bool Equals(object obj) => obj is ItemAmount o && Equals(o);
        public override int GetHashCode() => ((Name?.GetHashCode() ?? 0) * 397) ^ (Variant?.GetHashCode() ?? 0) ^ Count;
        public override string ToString() => $"{Count} x {Name}{(Variant == null ? "" : " [variant]")}";
    }

    /// <summary>A localization key plus an optional number argument (-1 = none).</summary>
    public readonly struct TerminalMessage : IEquatable<TerminalMessage>
    {
        public TerminalMessage(string key, int arg)
        {
            Key = key;
            Arg = arg;
        }

        public string Key { get; }
        public int Arg { get; }

        public bool Equals(TerminalMessage o) => Key == o.Key && Arg == o.Arg;
        public override bool Equals(object obj) => obj is TerminalMessage o && Equals(o);
        public override int GetHashCode() => (Key?.GetHashCode() ?? 0) ^ Arg;
    }

    public sealed class TerminalRequest
    {
        public TerminalOp Op;
        public int X, Y, Z;
        public int RequestId;
        /// <summary>Withdraw: the items to take (count = how many of each). Deposit/Return: the items. List: empty.</summary>
        public List<ItemAmount> Items = new List<ItemAmount>();
        /// <summary>Station settings ops: the mode, rule line, item name, "on"/"off", melt line or metal. Empty otherwise.</summary>
        public string Text = "";
    }

    public sealed class TerminalResponse
    {
        public int RequestId;
        public bool HasListing;
        public List<ItemAmount> Listing = new List<ItemAmount>();
        public int DistinctItems, UsedStacks, MaxStacks, Drives;
        public List<TerminalMessage> Messages = new List<TerminalMessage>();
        /// <summary>Items the client must put into its backpack (withdrawals, deposit leftovers).</summary>
        public List<ItemAmount> Give = new List<ItemAmount>();
        /// <summary>DriveUnload leftovers: the client puts these back onto the drive it emptied.</summary>
        public List<ItemAmount> ReturnToDrive = new List<ItemAmount>();
        /// <summary>The station's settings (StationSettings text) when the request came from a Network station, else null.</summary>
        public string Station;
    }

    /// <summary>Binary encoding for terminal messages. Reading validates everything and throws FormatException on bad data.</summary>
    public static class TerminalCodec
    {
        public const int MaxListLength = 10000;
        private const int MaxStringLength = 64 * 1024;

        public static void Write(BinaryWriter w, TerminalRequest r)
        {
            w.Write((byte)r.Op);
            w.Write(r.X);
            w.Write(r.Y);
            w.Write(r.Z);
            w.Write(r.RequestId);
            WriteItems(w, r.Items);
            w.Write(r.Text ?? "");
        }

        public static TerminalRequest ReadRequest(BinaryReader rd)
        {
            byte op = rd.ReadByte();
            if (op < (byte)TerminalOp.List || op > (byte)TerminalOp.RemoveMeltTarget) throw new FormatException($"Unknown terminal op {op}.");
            return new TerminalRequest
            {
                Op = (TerminalOp)op,
                X = rd.ReadInt32(),
                Y = rd.ReadInt32(),
                Z = rd.ReadInt32(),
                RequestId = rd.ReadInt32(),
                Items = ReadItems(rd),
                Text = ReadString(rd),
            };
        }

        public static void Write(BinaryWriter w, TerminalResponse r)
        {
            w.Write(r.RequestId);
            w.Write(r.HasListing);
            if (r.HasListing)
            {
                WriteItems(w, r.Listing);
                w.Write(r.DistinctItems);
                w.Write(r.UsedStacks);
                w.Write(r.MaxStacks);
                w.Write(r.Drives);
            }
            w.Write(r.Messages.Count);
            foreach (TerminalMessage m in r.Messages)
            {
                w.Write(m.Key ?? "");
                w.Write(m.Arg);
            }
            WriteItems(w, r.Give);
            WriteItems(w, r.ReturnToDrive);
            w.Write(r.Station != null);
            if (r.Station != null) w.Write(r.Station);
        }

        public static TerminalResponse ReadResponse(BinaryReader rd)
        {
            var r = new TerminalResponse { RequestId = rd.ReadInt32(), HasListing = rd.ReadBoolean() };
            if (r.HasListing)
            {
                r.Listing = ReadItems(rd);
                r.DistinctItems = rd.ReadInt32();
                r.UsedStacks = rd.ReadInt32();
                r.MaxStacks = rd.ReadInt32();
                r.Drives = rd.ReadInt32();
            }
            int messages = ReadLength(rd);
            for (int i = 0; i < messages; i++) r.Messages.Add(new TerminalMessage(ReadString(rd), rd.ReadInt32()));
            r.Give = ReadItems(rd);
            r.ReturnToDrive = ReadItems(rd);
            r.Station = rd.ReadBoolean() ? ReadString(rd) : null;
            return r;
        }

        private static void WriteItems(BinaryWriter w, List<ItemAmount> items)
        {
            w.Write(items.Count);
            foreach (ItemAmount a in items)
            {
                w.Write(a.Name ?? "");
                w.Write(a.Variant != null);
                if (a.Variant != null) w.Write(a.Variant);
                w.Write(a.Count);
            }
        }

        private static List<ItemAmount> ReadItems(BinaryReader rd)
        {
            int n = ReadLength(rd);
            var items = new List<ItemAmount>(n);
            for (int i = 0; i < n; i++)
            {
                string name = ReadString(rd);
                if (name.Length == 0) throw new FormatException("Empty item name.");
                string variant = rd.ReadBoolean() ? ReadString(rd) : null;
                int count = rd.ReadInt32();
                if (count <= 0) throw new FormatException($"Bad count {count} for {name}.");
                items.Add(new ItemAmount(name, variant, count));
            }
            return items;
        }

        private static int ReadLength(BinaryReader rd)
        {
            int n = rd.ReadInt32();
            if (n < 0 || n > MaxListLength) throw new FormatException($"Bad list length {n}.");
            return n;
        }

        private static string ReadString(BinaryReader rd)
        {
            string s = rd.ReadString();
            if (s.Length > MaxStringLength) throw new FormatException("String too long.");
            return s;
        }
    }
}
