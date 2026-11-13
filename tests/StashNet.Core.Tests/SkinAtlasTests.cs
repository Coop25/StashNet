using StashNet.Core.Skins;
using Xunit;

namespace StashNet.Core.Tests
{
    public class SkinAtlasTests
    {
        private const string OneSprite =
            "<atlas width=\"1024\" height=\"1024\"><sprite name=\"bulletHole1\" x=\"0\" y=\"0\" w=\"256\" h=\"128\" /></atlas>";

        [Fact]
        public void Parse_ConvertsPixelRectToBottomLeftUv()
        {
            SkinAtlas atlas = SkinAtlas.Parse(OneSprite);
            Assert.True(atlas.TryGetUv("bulletHole1", out UvRect uv));
            Assert.Equal(0f, uv.U0, 6);
            Assert.Equal(0.875f, uv.V0, 6);
            Assert.Equal(0.25f, uv.U1, 6);
            Assert.Equal(1f, uv.V1, 6);
        }

        [Fact]
        public void TryGetUv_UnknownSprite_ReturnsFalse() =>
            Assert.False(SkinAtlas.Parse(OneSprite).TryGetUv("nope", out _));

        [Fact]
        public void Parse_MalformedXml_Throws() =>
            Assert.Throws<SkinFormatException>(() => SkinAtlas.Parse("<atlas width=\"1024\""));

        /// <summary>Every sprite the plan's layouts (both phases) rely on, as drawn by tools/skin/draw-skin-atlas.ps1.</summary>
        private static readonly string[] ShippedSprites =
        {
            "stencilStashnet", "labelUnitBay", "labelUnitCore", "labelUnitTerminal", "labelUnitCraft",
            "bareMetal1", "bareMetal2", "bareMetal3", "rustStreak1", "rustStreak2", "rustBase",
            "bulletHole1", "bulletHole2", "bulletDent", "dent1", "clawGouge", "pryMarks", "scorch1", "scorch2",
            "stripeBlue", "stripeGreen", "stripePurple",
            "readoutDrv4", "readoutDrv8", "readoutDrv12", "readoutCore", "readoutCraft",
            "ductTape", "conduit", "cableBundle", "wireSpark", "crtScreen",
        };

        [Fact]
        public void ShippedAtlas_ParsesAndHasAllSprites()
        {
            string path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Mod", "Skins", "atlas.xml");
            SkinAtlas atlas = SkinAtlas.Parse(System.IO.File.ReadAllText(path));
            foreach (string name in ShippedSprites)
                Assert.True(atlas.TryGetUv(name, out _), $"atlas.xml is missing sprite '{name}'");
        }

        [Fact]
        public void Parse_ZeroSize_Throws() =>
            Assert.Throws<SkinFormatException>(() => SkinAtlas.Parse(
                "<atlas width=\"1024\" height=\"1024\"><sprite name=\"a\" x=\"0\" y=\"0\" w=\"0\" h=\"10\" /></atlas>"));
    }
}
