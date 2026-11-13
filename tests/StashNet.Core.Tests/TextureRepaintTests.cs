using System.Linq;
using StashNet.Core.Skins;
using Xunit;

namespace StashNet.Core.Tests
{
    /// <summary>Pixel math for the runtime repaint. Pixel arrays are RGBA, row 0 = bottom (like Unity textures).</summary>
    public class TextureRepaintTests
    {
        private static byte[] Solid(int w, int h, byte r, byte g, byte b, byte a = 255)
        {
            var px = new byte[w * h * 4];
            for (int i = 0; i < px.Length; i += 4) { px[i] = r; px[i + 1] = g; px[i + 2] = b; px[i + 3] = a; }
            return px;
        }

        private static (byte r, byte g, byte b, byte a) At(byte[] px, int w, int x, int y)
        {
            int i = (y * w + x) * 4;
            return (px[i], px[i + 1], px[i + 2], px[i + 3]);
        }

        /// <summary>A whole-atlas sprite: atlas pixels and a UV rect covering all of it.</summary>
        private static SpriteImage Whole(byte[] atlas, int w, int h) => new SpriteImage(atlas, w, h, new UvRect(0, 0, 1, 1));

        // ---- Recolor ----

        [Fact]
        public void Recolor_MidGrayFullStrength_BecomesTint_AlphaKept()
        {
            byte[] px = Solid(1, 1, 128, 128, 128, 200);
            TextureRepaint.Recolor(px, 1, 1, 74, 80, 63, 1f);
            var p = At(px, 1, 0, 0);
            Assert.InRange(p.r, 72, 76);
            Assert.InRange(p.g, 78, 82);
            Assert.InRange(p.b, 61, 65);
            Assert.Equal(200, p.a);
        }

        [Fact]
        public void Recolor_ZeroStrength_Unchanged()
        {
            byte[] px = Solid(1, 1, 10, 200, 30);
            TextureRepaint.Recolor(px, 1, 1, 74, 80, 63, 0f);
            Assert.Equal((10, 200, 30, 255), At(px, 1, 0, 0));
        }

        [Fact]
        public void Recolor_KeepsDetail_DarkStaysDarker()
        {
            byte[] px = Solid(2, 1, 40, 40, 40);
            px[4] = px[5] = px[6] = 200;
            TextureRepaint.Recolor(px, 2, 1, 74, 80, 63, 1f);
            Assert.True(At(px, 2, 0, 0).g < At(px, 2, 1, 0).g);
        }

        // ---- Mask ----

        [Fact]
        public void SetAlpha_ChangesOnlyAlpha()
        {
            byte[] px = Solid(2, 2, 10, 20, 30, 255);
            TextureRepaint.SetAlpha(px, 2, 2, 0);
            for (int i = 0; i < px.Length; i += 4)
                Assert.Equal(new byte[] { 10, 20, 30, 0 }, new[] { px[i], px[i + 1], px[i + 2], px[i + 3] });
        }

        // ---- Grime ----

        [Fact]
        public void Grime_SameSeed_SameResult_NeverBrightens()
        {
            byte[] a = Solid(32, 32, 150, 150, 150), b = Solid(32, 32, 150, 150, 150);
            TextureRepaint.Grime(a, 32, 32, 5, 1f);
            TextureRepaint.Grime(b, 32, 32, 5, 1f);
            Assert.Equal(a, b);
            for (int i = 0; i < a.Length; i += 4) Assert.True(a[i] <= 150);
            Assert.Contains(a.Where((v, i) => i % 4 == 0), v => v < 150);
        }

        [Fact]
        public void Grime_ZeroAmount_Unchanged()
        {
            byte[] px = Solid(8, 8, 150, 150, 150);
            TextureRepaint.Grime(px, 8, 8, 5, 0f);
            Assert.Equal(Solid(8, 8, 150, 150, 150), px);
        }

        // ---- Stamp ----

        [Fact]
        public void Stamp_OpaqueSprite_PaintsCenterNotCorner()
        {
            byte[] dst = Solid(8, 8, 0, 0, 0);
            TextureRepaint.Stamp(dst, 8, 8, Whole(Solid(4, 4, 255, 0, 0), 4, 4), 0.5f, 0.5f, 0.5f, 0.5f, 0f);
            Assert.Equal((255, 0, 0, 255), At(dst, 8, 4, 4));
            Assert.Equal((0, 0, 0, 255), At(dst, 8, 0, 0));
        }

        [Fact]
        public void Stamp_MissingSprite_LeavesTexture()
        {
            byte[] dst = Solid(8, 8, 9, 9, 9);
            TextureRepaint.Stamp(dst, 8, 8, null, 0.5f, 0.5f, 1f, 1f, 0f);
            Assert.Equal(Solid(8, 8, 9, 9, 9), dst);
        }

        [Fact]
        public void Stamp_TransparentSprite_LeavesTexture()
        {
            byte[] dst = Solid(8, 8, 9, 9, 9);
            TextureRepaint.Stamp(dst, 8, 8, Whole(Solid(4, 4, 255, 0, 0, 0), 4, 4), 0.5f, 0.5f, 1f, 1f, 0f);
            Assert.Equal(Solid(8, 8, 9, 9, 9), dst);
        }

        [Fact]
        public void Stamp_HalfAlpha_Blends()
        {
            byte[] dst = Solid(8, 8, 0, 0, 0);
            TextureRepaint.Stamp(dst, 8, 8, Whole(Solid(4, 4, 200, 200, 200, 128), 4, 4), 0.5f, 0.5f, 1f, 1f, 0f);
            Assert.InRange(At(dst, 8, 4, 4).r, 95, 106);
            Assert.Equal(255, At(dst, 8, 4, 4).a);
        }

        [Fact]
        public void Stamp_Rot90_LeftOfSpriteGoesToBottom()
        {
            // 2x1 sprite: left pixel red, right pixel blue.
            byte[] sprite = { 255, 0, 0, 255, 0, 0, 255, 255 };
            byte[] dst = Solid(16, 16, 0, 0, 0);
            TextureRepaint.Stamp(dst, 16, 16, Whole(sprite, 2, 1), 0.5f, 0.5f, 0.5f, 0.5f, 90f);
            Assert.True(At(dst, 16, 8, 5).r > 200 && At(dst, 16, 8, 5).b < 50, "below center should be red");
            Assert.True(At(dst, 16, 8, 10).b > 200 && At(dst, 16, 8, 10).r < 50, "above center should be blue");
        }

        [Fact]
        public void SpriteImage_SamplesOnlyItsOwnRect()
        {
            // 2x1 atlas: left red, right green. The sprite is the right half only.
            byte[] atlas = { 255, 0, 0, 255, 0, 255, 0, 255 };
            var sprite = new SpriteImage(atlas, 2, 1, new UvRect(0.5f, 0, 1, 1));
            sprite.Sample(0f, 0.5f, out byte r, out byte g, out _, out _);
            Assert.Equal(0, r);
            Assert.Equal(255, g);
        }

        [Fact]
        public void Crop_CopiesTheSpritesPixels()
        {
            // 4x2 atlas, bottom row: r=0..3; top row: r=10..13. The sprite is x 2..3 of the top row.
            var atlas = new byte[4 * 2 * 4];
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 4; x++) { int i = (y * 4 + x) * 4; atlas[i] = (byte)(y * 10 + x); atlas[i + 3] = 255; }
            byte[] crop = TextureRepaint.Crop(atlas, 4, 2, new UvRect(0.5f, 0.5f, 1f, 1f), out int w, out int h);
            Assert.Equal(2, w);
            Assert.Equal(1, h);
            Assert.Equal(new byte[] { 12, 0, 0, 255, 13, 0, 0, 255 }, crop);
        }

        // ---- Scatter ----

        [Fact]
        public void ScatterDamage_DeterministicInsideTexture_ScalesWithAmount()
        {
            var a = TextureRepaint.ScatterDamage(3, 1f);
            var b = TextureRepaint.ScatterDamage(3, 1f);
            Assert.Equal(a.Select(s => (s.Sprite, s.U, s.V, s.W, s.H, s.Rot)), b.Select(s => (s.Sprite, s.U, s.V, s.W, s.H, s.Rot)));
            Assert.NotEqual(a.Select(s => s.U), TextureRepaint.ScatterDamage(4, 1f).Select(s => s.U));
            Assert.All(a, s => { Assert.InRange(s.U, 0f, 1f); Assert.InRange(s.V, 0f, 1f); Assert.True(s.W > 0 && s.H > 0); });
            Assert.Empty(TextureRepaint.ScatterDamage(3, 0f));
            Assert.True(TextureRepaint.ScatterDamage(3, 2f).Count > a.Count);
        }

        // ---- Grid ----

        [Fact]
        public void GridCell_ColumnsLeftToRight_RowsFromTop()
        {
            Assert.Equal("A1", TextureRepaint.GridCell(0.01f, 0.99f));
            Assert.Equal("H8", TextureRepaint.GridCell(0.99f, 0.01f));
            Assert.Equal("C4", TextureRepaint.GridCell(0.3f, 0.6f));
        }

        [Fact]
        public void CellCenter_RoundTrips()
        {
            (float u, float v) = TextureRepaint.CellCenter("C4");
            Assert.Equal(0.3125f, u, 5);
            Assert.Equal(0.5625f, v, 5);
            Assert.Equal("C4", TextureRepaint.GridCell(u, v));
        }

        [Fact]
        public void Grid_DrawsCellLines()
        {
            byte[] px = Solid(16, 16, 0, 0, 0);
            TextureRepaint.Grid(px, 16, 16, _ => Whole(Solid(1, 1, 0, 0, 0, 0), 1, 1));
            Assert.NotEqual((0, 0, 0, 255), At(px, 16, 2, 7));   // column line at x = 16/8
            Assert.Equal((0, 0, 0, 255), At(px, 16, 1, 1));      // inside a cell
        }
    }
}
