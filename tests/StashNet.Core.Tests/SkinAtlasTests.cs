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

        [Fact]
        public void Parse_ZeroSize_Throws() =>
            Assert.Throws<SkinFormatException>(() => SkinAtlas.Parse(
                "<atlas width=\"1024\" height=\"1024\"><sprite name=\"a\" x=\"0\" y=\"0\" w=\"0\" h=\"10\" /></atlas>"));
    }
}
