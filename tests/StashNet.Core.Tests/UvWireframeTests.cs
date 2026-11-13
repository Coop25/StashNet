using StashNet.Core.Skins;
using Xunit;

namespace StashNet.Core.Tests
{
    /// <summary>The UV map saved next to each dumped texture: which pixels a model's triangle edges cross.</summary>
    public class UvWireframeTests
    {
        private const int Size = 8;

        private static bool At(bool[] mask, int x, int y) => mask[y * Size + x];

        private static bool[] OneTriangle(float u0, float v0, float u1, float v1, float u2, float v2) =>
            UvWireframe.Draw(Size, new[] { u0, u1, u2 }, new[] { v0, v1, v2 }, new[] { 0, 1, 2 });

        [Fact]
        public void Draw_TriangleEdges_SetCornerAndEdgePixels()
        {
            bool[] m = OneTriangle(0, 0, 1, 0, 0, 1);
            Assert.True(At(m, 0, 0));
            Assert.True(At(m, 7, 0));
            Assert.True(At(m, 0, 7));
            Assert.True(At(m, 3, 0));   // bottom edge
            Assert.True(At(m, 0, 4));   // left edge
            Assert.True(At(m, 4, 3));   // diagonal
        }

        [Fact]
        public void Draw_InsideTriangle_StaysEmpty() =>
            Assert.False(At(OneTriangle(0, 0, 1, 0, 0, 1), 2, 2));

        [Fact]
        public void Draw_RowZeroIsBottom_LikeUnityTextures()
        {
            bool[] m = OneTriangle(0, 0.99f, 1, 0.99f, 0.5f, 0.99f);
            Assert.True(At(m, 3, 7));
            Assert.False(At(m, 3, 0));
        }

        [Fact]
        public void Draw_TiledUvs_WrapIntoTheTexture()
        {
            bool[] m = OneTriangle(1.0f + 3f / 7f, 0, 1.0f + 3f / 7f, 0, 1.0f + 3f / 7f, 0);
            Assert.True(At(m, 3, 0));
        }

        [Fact]
        public void Draw_MismatchedArrays_Ignored()
        {
            bool[] m = UvWireframe.Draw(Size, new[] { 0f }, new[] { 0f, 1f }, new[] { 0, 1, 2 });
            Assert.Equal(Size * Size, m.Length);
            Assert.DoesNotContain(true, m);
        }
    }
}
