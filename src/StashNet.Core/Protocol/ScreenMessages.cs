using System;
using System.IO;

namespace StashNet.Core.Protocol
{
    /// <summary>A block position (game-free).</summary>
    public readonly struct Pos3 : IEquatable<Pos3>
    {
        public Pos3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public int X { get; }
        public int Y { get; }
        public int Z { get; }

        public bool Equals(Pos3 o) => X == o.X && Y == o.Y && Z == o.Z;
        public override bool Equals(object obj) => obj is Pos3 o && Equals(o);
        public override int GetHashCode() => (((X * 397) ^ Y) * 397) ^ Z;
        public override string ToString() => $"{X},{Y},{Z}";
    }

    /// <summary>What a Storage Terminal's screen shows for one network.</summary>
    public sealed class ScreenView : IEquatable<ScreenView>
    {
        public static readonly ScreenView NoSignal = new ScreenView(false, false, false, 0, 0, 0, 0, 0);

        public ScreenView(bool hasCore, bool shut, bool locked, int usedStacks, int maxStacks, int drives, int fullDrives, int heatSegments)
        {
            HasCore = hasCore;
            Shut = shut;
            Locked = locked;
            UsedStacks = usedStacks;
            MaxStacks = maxStacks;
            Drives = drives;
            FullDrives = fullDrives;
            HeatSegments = Math.Max(0, Math.Min(6, heatSegments));
        }

        public bool HasCore { get; }
        public bool Shut { get; }
        public bool Locked { get; }
        public int UsedStacks { get; }
        public int MaxStacks { get; }
        public int Drives { get; }
        public int FullDrives { get; }
        /// <summary>0 to 6.</summary>
        public int HeatSegments { get; }

        /// <summary>The viewer is locked out of a network that exists.</summary>
        public static ScreenView ShutOut() => new ScreenView(true, true, true, 0, 0, 0, 0, 0);

        public static string HeatWord(int segments) => segments <= 2 ? "CALM" : segments <= 4 ? "WARM" : "HOT";

        public static int HeatSegmentsOf(float heat, float cap)
        {
            if (cap <= 0f || heat <= 0f) return 0;
            float f = Math.Min(1f, heat / cap);
            return (int)Math.Round(f * 6f, MidpointRounding.AwayFromZero);
        }

        public bool Equals(ScreenView o) =>
            !(o is null) && HasCore == o.HasCore && Shut == o.Shut && Locked == o.Locked && UsedStacks == o.UsedStacks
            && MaxStacks == o.MaxStacks && Drives == o.Drives && FullDrives == o.FullDrives && HeatSegments == o.HeatSegments;

        public override bool Equals(object obj) => Equals(obj as ScreenView);

        public override int GetHashCode()
        {
            int h = (HasCore ? 1 : 0) | (Shut ? 2 : 0) | (Locked ? 4 : 0);
            h = h * 397 ^ UsedStacks;
            h = h * 397 ^ MaxStacks;
            h = h * 397 ^ Drives;
            h = h * 397 ^ FullDrives;
            h = h * 397 ^ HeatSegments;
            return h;
        }

        public static bool operator ==(ScreenView a, ScreenView b) => a is null ? b is null : a.Equals(b);
        public static bool operator !=(ScreenView a, ScreenView b) => !(a == b);
    }

    public enum ScreenOp : byte
    {
        /// <summary>Client to server: I am looking at the terminal at Terminal.</summary>
        Watch = 1,
        /// <summary>Client to server: I stopped looking at the terminal at Terminal.</summary>
        Unwatch = 2,
        /// <summary>Server to client: Terminal belongs to the Core at Core (if HasCore).</summary>
        Assign = 3,
        /// <summary>Server to client: the network at Core now looks like View.</summary>
        State = 4,
    }

    public sealed class ScreenMessage
    {
        public ScreenOp Op;
        public Pos3 Terminal;
        public bool HasCore;
        public Pos3 Core;
        public ScreenView View;
    }

    public static class ScreenCodec
    {
        public static void Write(BinaryWriter w, ScreenMessage m)
        {
            w.Write((byte)m.Op);
            switch (m.Op)
            {
                case ScreenOp.Watch:
                case ScreenOp.Unwatch:
                    WritePos(w, m.Terminal);
                    break;
                case ScreenOp.Assign:
                    WritePos(w, m.Terminal);
                    w.Write(m.HasCore);
                    WritePos(w, m.Core);
                    break;
                case ScreenOp.State:
                    WritePos(w, m.Core);
                    WriteView(w, m.View ?? throw new ArgumentException("State needs a View"));
                    break;
                default:
                    throw new FormatException("Unknown screen op " + (byte)m.Op);
            }
        }

        public static ScreenMessage Read(BinaryReader r)
        {
            var m = new ScreenMessage { Op = (ScreenOp)r.ReadByte() };
            switch (m.Op)
            {
                case ScreenOp.Watch:
                case ScreenOp.Unwatch:
                    m.Terminal = ReadPos(r);
                    break;
                case ScreenOp.Assign:
                    m.Terminal = ReadPos(r);
                    m.HasCore = r.ReadBoolean();
                    m.Core = ReadPos(r);
                    break;
                case ScreenOp.State:
                    m.HasCore = true;
                    m.Core = ReadPos(r);
                    m.View = ReadView(r);
                    break;
                default:
                    throw new FormatException("Unknown screen op " + (byte)m.Op);
            }
            return m;
        }

        static void WritePos(BinaryWriter w, Pos3 p)
        {
            w.Write(p.X);
            w.Write(p.Y);
            w.Write(p.Z);
        }

        static Pos3 ReadPos(BinaryReader r) => new Pos3(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());

        static void WriteView(BinaryWriter w, ScreenView v)
        {
            w.Write(v.HasCore);
            w.Write(v.Shut);
            w.Write(v.Locked);
            w.Write(v.UsedStacks);
            w.Write(v.MaxStacks);
            w.Write(v.Drives);
            w.Write(v.FullDrives);
            w.Write((byte)v.HeatSegments);
        }

        static ScreenView ReadView(BinaryReader r)
        {
            bool core = r.ReadBoolean(), shut = r.ReadBoolean(), locked = r.ReadBoolean();
            int used = r.ReadInt32(), max = r.ReadInt32(), drives = r.ReadInt32(), full = r.ReadInt32();
            return new ScreenView(core, shut, locked, used, max, drives, full, r.ReadByte());
        }
    }
}
