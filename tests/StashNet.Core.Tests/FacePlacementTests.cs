using StashNet.Core.Skins;
using Xunit;

namespace StashNet.Core.Tests
{
    public class FacePlacementTests
    {
        private static readonly Vec3 Min = new Vec3(-0.5f, 0f, -0.5f);
        private static readonly Vec3 Max = new Vec3(0.5f, 2f, 0.5f);

        private static SkinPiece Piece(string face, float u, float v, float w, float h, float rot = 0f) =>
            new SkinPiece { Sprite = "s", Face = face, U = u, V = v, W = w, H = h, Rot = rot };

        private static void Near(Vec3 expected, Vec3 actual)
        {
            Assert.Equal(expected.X, actual.X, 5);
            Assert.Equal(expected.Y, actual.Y, 5);
            Assert.Equal(expected.Z, actual.Z, 5);
        }

        [Fact]
        public void Place_FrontMinusZ_Center()
        {
            Quad q = FacePlacement.Place(Min, Max, "-z", Piece("front", 0.5f, 0.5f, 0.2f, 0.2f), 0);
            Near(new Vec3(0, 0, -1), q.Normal);
            Near(new Vec3(-0.1f, 0.9f, -0.504f), q.BottomLeft);
            Near(new Vec3(0.1f, 1.1f, -0.504f), q.TopRight);
        }

        [Fact]
        public void Place_FrontPlusZ_RightIsMinusX()
        {
            Quad q = FacePlacement.Place(Min, Max, "+z", Piece("front", 0.5f, 0.5f, 0.2f, 0.2f), 0);
            Assert.Equal(0.1f, q.BottomLeft.X, 5);
            Assert.Equal(0.504f, q.BottomLeft.Z, 5);
            Near(new Vec3(0, 0, 1), q.Normal);
        }

        [Fact]
        public void Place_RightFace_FrontMinusZ_IsPlusX()
        {
            Quad q = FacePlacement.Place(Min, Max, "-z", Piece("right", 0f, 0f, 0.2f, 0.2f), 0);
            Near(new Vec3(1, 0, 0), q.Normal);
            foreach (Vec3 c in new[] { q.BottomLeft, q.BottomRight, q.TopRight, q.TopLeft })
                Assert.Equal(0.504f, c.X, 5);
        }

        [Fact]
        public void Place_Rot90_SwapsCornerDirections()
        {
            Quad flat = FacePlacement.Place(Min, Max, "-z", Piece("front", 0.5f, 0.5f, 0.2f, 0.2f), 0);
            Quad turned = FacePlacement.Place(Min, Max, "-z", Piece("front", 0.5f, 0.5f, 0.2f, 0.2f, 90f), 0);
            Near(flat.BottomRight, turned.BottomLeft);
        }

        [Fact]
        public void Place_Layer2_LiftsFurther()
        {
            Quad q = FacePlacement.Place(Min, Max, "-z", Piece("front", 0.5f, 0.5f, 0.2f, 0.2f), 2);
            Assert.Equal(-0.505f, q.BottomLeft.Z, 5);
        }

        [Fact]
        public void Place_PieceLargerThanFace_ClampedInside()
        {
            Quad q = FacePlacement.Place(Min, Max, "-z", Piece("front", 0.9f, 0.5f, 3f, 0.2f), 0);
            Assert.True(System.Math.Abs(q.BottomRight.X - q.BottomLeft.X) <= 1.0f + 1e-5f);
            foreach (Vec3 c in new[] { q.BottomLeft, q.BottomRight, q.TopRight, q.TopLeft })
                Assert.InRange(c.X, -0.5f - 1e-5f, 0.5f + 1e-5f);
        }

        [Fact]
        public void Place_Top_FrontMinusZ()
        {
            Quad q = FacePlacement.Place(Min, Max, "-z", Piece("top", 0.5f, 0f, 0.2f, 0.2f), 0);
            Near(new Vec3(0, 1, 0), q.Normal);
            Assert.Equal(2.004f, q.BottomLeft.Y, 5);
            Assert.Equal(-0.5f, q.BottomLeft.Z, 5);
            Assert.Equal(-0.5f, q.BottomRight.Z, 5);
            Assert.Equal(-0.3f, q.TopLeft.Z, 5);
        }
    }
}
