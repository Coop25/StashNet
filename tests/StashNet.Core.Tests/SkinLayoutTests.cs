using System.Collections.Generic;
using StashNet.Core.Skins;
using Xunit;

namespace StashNet.Core.Tests
{
    public class SkinLayoutTests
    {
        private static readonly SkinAtlas Atlas = SkinAtlas.Parse(
            "<atlas width=\"1024\" height=\"1024\"><sprite name=\"bulletHole1\" x=\"0\" y=\"0\" w=\"64\" h=\"64\" /></atlas>");

        private static SkinLayout Parse(string body, List<string> warnings) =>
            SkinLayout.Parse("<skins>" + body + "</skins>", Atlas, warnings);

        [Fact]
        public void Parse_ReadsBlockAndPieces()
        {
            var warnings = new List<string>();
            SkinLayout layout = Parse(
                "<block name=\"stashnetDriveBay\" front=\"-z\">" +
                "<piece sprite=\"bulletHole1\" face=\"front\" u=\"0.7\" v=\"0.6\" w=\"0.06\" h=\"0.06\" rot=\"30\" glow=\"true\" />" +
                "<piece sprite=\"bulletHole1\" face=\"top\" u=\"0.1\" v=\"0.2\" w=\"0.3\" h=\"0.4\" />" +
                "</block>", warnings);

            Assert.Empty(warnings);
            Assert.True(layout.TryGet("stashnetDriveBay", out BlockSkin skin));
            Assert.Equal("-z", skin.Front);
            Assert.Equal(2, skin.Pieces.Count);
            SkinPiece p = skin.Pieces[0];
            Assert.Equal("bulletHole1", p.Sprite);
            Assert.Equal("front", p.Face);
            Assert.Equal(0.7f, p.U, 6);
            Assert.Equal(0.6f, p.V, 6);
            Assert.Equal(0.06f, p.W, 6);
            Assert.Equal(0.06f, p.H, 6);
            Assert.Equal(30f, p.Rot, 6);
            Assert.True(p.Glow);
            Assert.Equal(0f, skin.Pieces[1].Rot);
            Assert.False(skin.Pieces[1].Glow);
        }

        [Fact]
        public void Parse_BadPiece_SkippedWithWarning()
        {
            var warnings = new List<string>();
            SkinLayout layout = Parse(
                "<block name=\"b\" front=\"-z\">" +
                "<piece sprite=\"missing\" face=\"front\" u=\"0.5\" v=\"0.5\" w=\"0.1\" h=\"0.1\" />" +
                "<piece sprite=\"bulletHole1\" face=\"bottom\" u=\"0.5\" v=\"0.5\" w=\"0.1\" h=\"0.1\" />" +
                "<piece sprite=\"bulletHole1\" face=\"front\" u=\"0.5\" v=\"0.5\" w=\"0\" h=\"0.1\" />" +
                "<piece sprite=\"bulletHole1\" face=\"front\" u=\"0.5\" v=\"0.5\" w=\"0.1\" h=\"0.1\" />" +
                "</block>", warnings);

            Assert.Equal(3, warnings.Count);
            Assert.All(warnings, w => Assert.Contains("b", w));
            Assert.Contains(warnings, w => w.Contains("missing"));
            Assert.Contains(warnings, w => w.Contains("bottom"));
            Assert.True(layout.TryGet("b", out BlockSkin skin));
            Assert.Single(skin.Pieces);
        }

        [Fact]
        public void Parse_BadFront_BlockSkippedWithWarning()
        {
            var warnings = new List<string>();
            SkinLayout layout = Parse("<block name=\"b\" front=\"y\" />", warnings);
            Assert.Single(warnings);
            Assert.False(layout.TryGet("b", out _));
        }

        [Fact]
        public void Parse_MalformedXml_Throws() =>
            Assert.Throws<SkinFormatException>(() => SkinLayout.Parse("<skins><block", Atlas, new List<string>()));

        [Fact]
        public void ShippedLayouts_ParseWithoutWarnings()
        {
            string dir = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Mod", "Skins");
            SkinAtlas atlas = SkinAtlas.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "atlas.xml")));
            var warnings = new List<string>();
            SkinLayout layout = SkinLayout.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "layouts.xml")), atlas, warnings);
            Assert.Empty(warnings);
            Assert.True(layout.TryGet("stashnetDriveBay", out _));
        }

        [Fact]
        public void Parse_ReadsShaderOverrides()
        {
            SkinLayout layout = SkinLayout.Parse("<skins shader=\"Legacy Shaders/Diffuse\" glowShader=\"Unlit/Texture\" />", Atlas, new List<string>());
            Assert.Equal("Legacy Shaders/Diffuse", layout.Shader);
            Assert.Equal("Unlit/Texture", layout.GlowShader);
        }

        [Fact]
        public void Parse_NoShaderOverrides_AreNull()
        {
            SkinLayout layout = SkinLayout.Parse("<skins />", Atlas, new List<string>());
            Assert.Null(layout.Shader);
            Assert.Null(layout.GlowShader);
        }

        [Fact]
        public void Parse_DuplicateBlock_LastWinsWithWarning()
        {
            var warnings = new List<string>();
            SkinLayout layout = Parse(
                "<block name=\"b\" front=\"-z\" />" +
                "<block name=\"b\" front=\"+x\" />", warnings);
            Assert.Single(warnings);
            Assert.True(layout.TryGet("b", out BlockSkin skin));
            Assert.Equal("+x", skin.Front);
        }
    }
}
