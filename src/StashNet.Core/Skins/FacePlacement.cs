using System;

namespace StashNet.Core.Skins
{
    /// <summary>A small game-free 3D vector (model-local space, Unity's axes: +y up, left-handed).</summary>
    public readonly struct Vec3
    {
        public readonly float X, Y, Z;

        public Vec3(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        /// <summary>Size of this vector along an axis-aligned unit direction.</summary>
        internal float Along(Vec3 axis) => Math.Abs(X * axis.X + Y * axis.Y + Z * axis.Z);

        public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
    }

    /// <summary>A decal's four corners, as seen from outside the face, plus the face's outward normal.</summary>
    public readonly struct Quad
    {
        public readonly Vec3 BottomLeft, BottomRight, TopRight, TopLeft, Normal;

        public Quad(Vec3 bottomLeft, Vec3 bottomRight, Vec3 topRight, Vec3 topLeft, Vec3 normal)
        {
            BottomLeft = bottomLeft; BottomRight = bottomRight; TopRight = topRight; TopLeft = topLeft; Normal = normal;
        }
    }

    /// <summary>
    /// Puts a <see cref="SkinPiece"/> on one face of a model's bounding box. "front" names the model axis the
    /// player sees as the front; left/right are as seen while facing the front. On the top face, u runs along
    /// the front's right and v from the front edge (0) to the back edge (1).
    /// </summary>
    public static class FacePlacement
    {
        /// <summary>How far the first decal floats off the face (meters).</summary>
        public const float Lift = 0.004f;

        /// <summary>Extra lift per later decal, so overlapping decals never z-fight.</summary>
        public const float LayerStep = 0.0005f;

        private static readonly Vec3 Up = new Vec3(0, 1, 0);

        public static Quad Place(Vec3 boundsMin, Vec3 boundsMax, string front, SkinPiece piece, int layer)
        {
            Vec3 f = FrontNormal(front);
            Vec3 frontRight = RightOf(f);
            Vec3 normal, right, up;
            switch (piece.Face)
            {
                case "front": normal = f; right = frontRight; up = Up; break;
                case "back": normal = -f; right = -frontRight; up = Up; break;
                case "right": normal = frontRight; right = RightOf(frontRight); up = Up; break;
                case "left": normal = -frontRight; right = RightOf(-frontRight); up = Up; break;
                case "top": normal = Up; right = frontRight; up = -f; break;
                default: throw new ArgumentException($"unknown face '{piece.Face}'");
            }

            Vec3 center = (boundsMin + boundsMax) * 0.5f;
            Vec3 half = (boundsMax - boundsMin) * 0.5f;
            float faceW = 2f * half.Along(right), faceH = 2f * half.Along(up);
            Vec3 faceCenter = center + normal * half.Along(normal);

            // Too big for the face: shrink to fit, keeping the shape. Then keep the whole piece on the face.
            float w = piece.W, h = piece.H;
            float fit = Math.Min(1f, Math.Min(w > 0 ? faceW / w : 1f, h > 0 ? faceH / h : 1f));
            w *= fit;
            h *= fit;
            float x = Clamp(piece.U * faceW, w / 2f, faceW - w / 2f) - faceW / 2f;
            float y = Clamp(piece.V * faceH, h / 2f, faceH - h / 2f) - faceH / 2f;

            Vec3 c = faceCenter + right * x + up * y + normal * (Lift + layer * LayerStep);
            double a = piece.Rot * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            Vec3 r = right * cos + up * sin;     // counter-clockwise as seen from outside
            Vec3 u = up * cos - right * sin;
            Vec3 hr = r * (w / 2f), hu = u * (h / 2f);
            return new Quad(c - hr - hu, c + hr - hu, c + hr + hu, c - hr + hu, normal);
        }

        private static Vec3 FrontNormal(string front)
        {
            switch (front)
            {
                case "+x": return new Vec3(1, 0, 0);
                case "-x": return new Vec3(-1, 0, 0);
                case "+z": return new Vec3(0, 0, 1);
                case "-z": return new Vec3(0, 0, -1);
                default: throw new ArgumentException($"unknown front '{front}'");
            }
        }

        /// <summary>The right-hand direction of someone outside a horizontal face looking at it: up x (-normal).</summary>
        private static Vec3 RightOf(Vec3 n)
        {
            Vec3 forward = -n;
            return new Vec3(Up.Y * forward.Z - Up.Z * forward.Y, Up.Z * forward.X - Up.X * forward.Z, Up.X * forward.Y - Up.Y * forward.X);
        }

        private static float Clamp(float value, float min, float max) =>
            min > max ? (min + max) / 2f : Math.Max(min, Math.Min(max, value));
    }
}
