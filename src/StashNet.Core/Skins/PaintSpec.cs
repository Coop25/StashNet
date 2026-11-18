using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace StashNet.Core.Skins
{
    /// <summary>One sprite painted into a texture. u,v = center (0..1, origin bottom-left); w,h = fraction of the texture.</summary>
    public sealed class TextureStamp
    {
        public string Sprite;
        public float U, V, W, H, Rot;

        /// <summary>Also smooth the surface bumps (normal map) under this stamp, e.g. to hide an embossed logo.</summary>
        public bool Flat;
    }

    /// <summary>How one of a model's vanilla textures (by its name) gets repainted.</summary>
    public sealed class TexturePaint
    {
        public string Name;
        public byte R, G, B;
        public float Strength = 0.85f;
        public float Damage = 1f;
        public int Seed = 1;

        /// <summary>
        /// The value written into the tint mask (the _RMOM texture's alpha), 0..255, or -1 to leave it alone.
        /// 0 (default) turns the mask off, so the painted texture shows everywhere.
        /// </summary>
        public int Mask;

        /// <summary>True for glass: stamps are painted solid (they raise the texture's see-through alpha).</summary>
        public bool SolidStamps;

        /// <summary>False for armor: bullet hits become dents, never holes.</summary>
        public bool Holes = true;

        public IReadOnlyList<TextureStamp> Stamps;
    }

    /// <summary>A block's repaint: its textures and, for the CRT, the sprite shown on the screen.</summary>
    public sealed class BlockPaint
    {
        public string BlockName;
        public string Screen;

        /// <summary>Silence the model's own sounds (the CRT TV's hum).</summary>
        public bool Mute;

        /// <summary>The screen shows live network numbers instead of a fixed sprite.</summary>
        public bool Live;

        public float GlitchMinGap = 8f;
        public float GlitchMaxGap = 30f;
        public float GlitchLength = 0.4f;
        public float GlitchStrength = 0.6f;

        internal readonly Dictionary<string, TexturePaint> Textures = new Dictionary<string, TexturePaint>();

        public bool TryGetTexture(string name, out TexturePaint paint) => Textures.TryGetValue(name ?? "", out paint);
    }

    /// <summary>
    /// Mod/Skins/paint.xml. Whole-file problems throw <see cref="SkinFormatException"/>; a bad block, texture
    /// or stamp is skipped and described in <c>warnings</c>.
    /// </summary>
    public sealed class PaintSpec
    {
        private readonly Dictionary<string, BlockPaint> blocks = new Dictionary<string, BlockPaint>();

        public bool TryGet(string blockName, out BlockPaint paint) => blocks.TryGetValue(blockName ?? "", out paint);

        public static PaintSpec Parse(string xml, SkinAtlas atlas, List<string> warnings)
        {
            XElement root = SkinXml.Load(xml, "paint");
            var spec = new PaintSpec();
            foreach (XElement b in root.Elements("block"))
            {
                string name = (string)b.Attribute("name");
                if (string.IsNullOrEmpty(name))
                {
                    warnings.Add("a <block> has no name; skipped");
                    continue;
                }
                var block = new BlockPaint
                {
                    BlockName = name,
                    Mute = string.Equals((string)b.Attribute("mute"), "true", System.StringComparison.OrdinalIgnoreCase),
                };
                block.Live = string.Equals((string)b.Attribute("live"), "true", System.StringComparison.OrdinalIgnoreCase);
                ReadGlitch(name, b, block, warnings);
                string screen = (string)b.Attribute("screen");
                if (!string.IsNullOrEmpty(screen))
                {
                    if (atlas.TryGetUv(screen, out _)) block.Screen = screen;
                    else warnings.Add($"{name}: unknown screen sprite '{screen}'; screen left as it was");
                }
                foreach (XElement t in b.Elements("texture"))
                {
                    TexturePaint tex = ReadTexture(name, t, atlas, warnings);
                    if (tex != null) block.Textures[tex.Name] = tex;
                }
                if (spec.blocks.ContainsKey(name)) warnings.Add($"{name}: listed twice; the last one wins");
                spec.blocks[name] = block;
            }
            return spec;
        }

        private static bool ReadGlitchNumber(string block, XElement b, string attr, List<string> warnings, ref float target)
        {
            if (b.Attribute(attr) == null) return false;
            if (!SkinXml.TryNumber(b, attr, out float v) || v < 0f)
            {
                warnings.Add($"{block}: {attr} must be a number of 0 or more; default kept");
                return false;
            }
            target = v;
            return true;
        }

        private static void ReadGlitch(string block, XElement b, BlockPaint paint, List<string> warnings)
        {
            ReadGlitchNumber(block, b, "glitchMin", warnings, ref paint.GlitchMinGap);
            ReadGlitchNumber(block, b, "glitchMax", warnings, ref paint.GlitchMaxGap);
            ReadGlitchNumber(block, b, "glitchLength", warnings, ref paint.GlitchLength);
            ReadGlitchNumber(block, b, "glitchStrength", warnings, ref paint.GlitchStrength);
            if (paint.GlitchMinGap > paint.GlitchMaxGap)
            {
                warnings.Add($"{block}: glitchMin is above glitchMax; swapped");
                float t = paint.GlitchMinGap;
                paint.GlitchMinGap = paint.GlitchMaxGap;
                paint.GlitchMaxGap = t;
            }
        }

        private static TexturePaint ReadTexture(string block, XElement t, SkinAtlas atlas, List<string> warnings)
        {
            string name = (string)t.Attribute("name");
            string tint = (string)t.Attribute("tint");
            if (string.IsNullOrEmpty(name) || !TryHex(tint, out byte r, out byte g, out byte bl))
            {
                warnings.Add($"{block}: texture '{name}' needs a name and a tint like 4A503F; skipped");
                return null;
            }
            var tex = new TexturePaint { Name = name, R = r, G = g, B = bl };
            if (SkinXml.TryNumber(t, "strength", out float strength)) tex.Strength = strength;
            if (SkinXml.TryNumber(t, "damage", out float damage)) tex.Damage = damage;
            if (SkinXml.TryNumber(t, "seed", out float seed)) tex.Seed = (int)seed;
            string mask = (string)t.Attribute("mask");
            if (string.Equals(mask, "keep", System.StringComparison.OrdinalIgnoreCase)) tex.Mask = -1;
            else if (mask != null)
            {
                if (!int.TryParse(mask, NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) || m < 0 || m > 255)
                {
                    warnings.Add($"{block}: texture '{name}' has mask '{mask}' (use 0-255 or keep); skipped");
                    return null;
                }
                tex.Mask = m;
            }
            tex.SolidStamps = string.Equals((string)t.Attribute("solid"), "true", System.StringComparison.OrdinalIgnoreCase);
            tex.Holes = !string.Equals((string)t.Attribute("holes"), "false", System.StringComparison.OrdinalIgnoreCase);

            var stamps = new List<TextureStamp>();
            foreach (XElement s in t.Elements("stamp"))
            {
                string sprite = (string)s.Attribute("sprite");
                if (!atlas.TryGetUv(sprite, out _))
                {
                    warnings.Add($"{block}/{name}: unknown stamp sprite '{sprite}'; skipped");
                    continue;
                }
                if (!SkinXml.TryNumber(s, "u", out float u) || !SkinXml.TryNumber(s, "v", out float v) ||
                    !SkinXml.TryNumber(s, "w", out float w) || !SkinXml.TryNumber(s, "h", out float h) || w <= 0 || h <= 0)
                {
                    warnings.Add($"{block}/{name}: {sprite} needs u, v and a size above 0 (w, h); skipped");
                    continue;
                }
                SkinXml.TryNumber(s, "rot", out float rot);
                bool flat = string.Equals((string)s.Attribute("flat"), "true", System.StringComparison.OrdinalIgnoreCase);
                stamps.Add(new TextureStamp { Sprite = sprite, U = u, V = v, W = w, H = h, Rot = rot, Flat = flat });
            }
            tex.Stamps = stamps;
            return tex;
        }

        private static bool TryHex(string hex, out byte r, out byte g, out byte b)
        {
            r = g = b = 0;
            if (hex == null || hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v)) return false;
            r = (byte)(v >> 16); g = (byte)(v >> 8); b = (byte)v;
            return true;
        }
    }
}
