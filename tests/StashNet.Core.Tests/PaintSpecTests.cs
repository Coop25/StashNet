using System.Collections.Generic;
using StashNet.Core.Skins;
using Xunit;

namespace StashNet.Core.Tests
{
    public class PaintSpecTests
    {
        private static readonly SkinAtlas Atlas = SkinAtlas.Parse(
            "<atlas width=\"64\" height=\"64\">" +
            "<sprite name=\"stencilStashnet\" x=\"0\" y=\"0\" w=\"32\" h=\"8\" />" +
            "<sprite name=\"crtScreen\" x=\"0\" y=\"16\" w=\"32\" h=\"24\" />" +
            "</atlas>");

        private static PaintSpec Parse(string body, List<string> warnings) =>
            PaintSpec.Parse("<paint>" + body + "</paint>", Atlas, warnings);

        [Fact]
        public void Parse_ReadsBlockTextureAndStamps()
        {
            var warnings = new List<string>();
            PaintSpec spec = Parse(
                "<block name=\"stashnetTerminal\" screen=\"crtScreen\">" +
                "<texture name=\"tvCRT\" tint=\"4A503F\" strength=\"0.8\" damage=\"1.5\" seed=\"7\">" +
                "<stamp sprite=\"stencilStashnet\" u=\"0.3\" v=\"0.6\" w=\"0.2\" h=\"0.05\" rot=\"90\" />" +
                "</texture></block>", warnings);

            Assert.Empty(warnings);
            Assert.True(spec.TryGet("stashnetTerminal", out BlockPaint block));
            Assert.Equal("crtScreen", block.Screen);
            Assert.True(block.TryGetTexture("tvCRT", out TexturePaint tex));
            Assert.Equal(0x4A, tex.R);
            Assert.Equal(0x50, tex.G);
            Assert.Equal(0x3F, tex.B);
            Assert.Equal(0.8f, tex.Strength, 6);
            Assert.Equal(1.5f, tex.Damage, 6);
            Assert.Equal(7, tex.Seed);
            TextureStamp s = Assert.Single(tex.Stamps);
            Assert.Equal("stencilStashnet", s.Sprite);
            Assert.Equal(0.3f, s.U, 6);
            Assert.Equal(0.6f, s.V, 6);
            Assert.Equal(0.2f, s.W, 6);
            Assert.Equal(0.05f, s.H, 6);
            Assert.Equal(90f, s.Rot, 6);
        }

        [Fact]
        public void Parse_Defaults_StrengthDamageSeedAndNoScreen()
        {
            PaintSpec spec = Parse("<block name=\"b\"><texture name=\"t\" tint=\"000000\" /></block>", new List<string>());
            Assert.True(spec.TryGet("b", out BlockPaint block));
            Assert.Null(block.Screen);
            Assert.True(block.TryGetTexture("t", out TexturePaint tex));
            Assert.Equal(0.85f, tex.Strength, 6);
            Assert.Equal(1f, tex.Damage, 6);
            Assert.Equal(1, tex.Seed);
            Assert.Empty(tex.Stamps);
        }

        [Fact]
        public void Parse_Mask_DefaultClearsKeepKeepsNumberSets()
        {
            PaintSpec spec = Parse(
                "<block name=\"b\">" +
                "<texture name=\"a\" tint=\"000000\" />" +
                "<texture name=\"k\" tint=\"000000\" mask=\"keep\" />" +
                "<texture name=\"n\" tint=\"000000\" mask=\"255\" />" +
                "</block>", new List<string>());
            spec.TryGet("b", out BlockPaint block);
            block.TryGetTexture("a", out TexturePaint a);
            block.TryGetTexture("k", out TexturePaint k);
            block.TryGetTexture("n", out TexturePaint n);
            Assert.Equal(0, a.Mask);
            Assert.Equal(-1, k.Mask);
            Assert.Equal(255, n.Mask);
        }

        [Fact]
        public void Parse_BadMask_TextureSkippedWithWarning()
        {
            var warnings = new List<string>();
            PaintSpec spec = Parse("<block name=\"b\"><texture name=\"t\" tint=\"000000\" mask=\"300\" /></block>", warnings);
            Assert.Single(warnings);
            spec.TryGet("b", out BlockPaint block);
            Assert.False(block.TryGetTexture("t", out _));
        }

        [Fact]
        public void Parse_SolidStamps_ForGlass()
        {
            PaintSpec spec = Parse("<block name=\"b\"><texture name=\"g\" tint=\"000000\" solid=\"true\" /><texture name=\"m\" tint=\"000000\" /></block>", new List<string>());
            spec.TryGet("b", out BlockPaint block);
            block.TryGetTexture("g", out TexturePaint g);
            block.TryGetTexture("m", out TexturePaint m);
            Assert.True(g.SolidStamps);
            Assert.False(m.SolidStamps);
        }

        [Fact]
        public void Parse_MuteTrue_SilencesTheBlock()
        {
            PaintSpec spec = Parse("<block name=\"tv\" mute=\"true\" /><block name=\"bay\" />", new List<string>());
            spec.TryGet("tv", out BlockPaint tv);
            spec.TryGet("bay", out BlockPaint bay);
            Assert.True(tv.Mute);
            Assert.False(bay.Mute);
        }

        [Fact]
        public void Parse_HolesFalse_ForArmored()
        {
            PaintSpec spec = Parse("<block name=\"b\"><texture name=\"t\" tint=\"000000\" holes=\"false\" /><texture name=\"u\" tint=\"000000\" /></block>", new List<string>());
            spec.TryGet("b", out BlockPaint block);
            block.TryGetTexture("t", out TexturePaint t);
            block.TryGetTexture("u", out TexturePaint u);
            Assert.False(t.Holes);
            Assert.True(u.Holes);
        }

        [Fact]
        public void Parse_BadTint_TextureSkippedWithWarning()
        {
            var warnings = new List<string>();
            PaintSpec spec = Parse("<block name=\"b\"><texture name=\"t\" tint=\"green\" /></block>", warnings);
            Assert.Single(warnings);
            Assert.True(spec.TryGet("b", out BlockPaint block));
            Assert.False(block.TryGetTexture("t", out _));
        }

        [Fact]
        public void Parse_UnknownStampSprite_StampSkippedWithWarning()
        {
            var warnings = new List<string>();
            PaintSpec spec = Parse(
                "<block name=\"b\"><texture name=\"t\" tint=\"000000\">" +
                "<stamp sprite=\"nope\" u=\"0.5\" v=\"0.5\" w=\"0.1\" h=\"0.1\" />" +
                "<stamp sprite=\"stencilStashnet\" u=\"0.5\" v=\"0.5\" w=\"0\" h=\"0.1\" />" +
                "</texture></block>", warnings);
            Assert.Equal(2, warnings.Count);
            spec.TryGet("b", out BlockPaint block);
            block.TryGetTexture("t", out TexturePaint tex);
            Assert.Empty(tex.Stamps);
        }

        [Fact]
        public void Parse_UnknownScreenSprite_ScreenDroppedWithWarning()
        {
            var warnings = new List<string>();
            PaintSpec spec = Parse("<block name=\"b\" screen=\"nope\" />", warnings);
            Assert.Single(warnings);
            Assert.True(spec.TryGet("b", out BlockPaint block));
            Assert.Null(block.Screen);
        }

        [Fact]
        public void Parse_MalformedXml_Throws() =>
            Assert.Throws<SkinFormatException>(() => PaintSpec.Parse("<paint><block", Atlas, new List<string>()));

        [Fact]
        public void ShippedPaint_ParsesWithoutWarnings()
        {
            string dir = System.IO.Path.Combine(System.AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Mod", "Skins");
            SkinAtlas atlas = SkinAtlas.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "atlas.xml")));
            var warnings = new List<string>();
            PaintSpec spec = PaintSpec.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "paint.xml")), atlas, warnings);
            Assert.Empty(warnings);
            foreach (string b in new[] { "stashnetDriveBay", "stashnetDriveBayReinforced", "stashnetDriveBayArmored",
                                         "stashnetScrapCore", "stashnetStorageCore", "stashnetAdvancedCore",
                                         "stashnetTerminal", "stashnetCraftingTerminal" })
                Assert.True(spec.TryGet(b, out _), $"paint.xml has no entry for {b}");
        }
    }
}
