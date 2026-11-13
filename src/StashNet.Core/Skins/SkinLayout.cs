using System.Collections.Generic;
using System.Xml.Linq;

namespace StashNet.Core.Skins
{
    /// <summary>One decal on a block's model. See Mod/Skins/layouts.xml for what each field means.</summary>
    public sealed class SkinPiece
    {
        public string Sprite;
        public string Face;
        public float U, V, W, H, Rot;
        public bool Glow;
    }

    /// <summary>All decals for one block, plus which model-local axis is its front.</summary>
    public sealed class BlockSkin
    {
        public string BlockName;
        public string Front;
        public IReadOnlyList<SkinPiece> Pieces;
    }

    /// <summary>
    /// Mod/Skins/layouts.xml. A whole-file problem throws <see cref="SkinFormatException"/>; a single bad
    /// piece or block is skipped and described in <c>warnings</c>, so one typo never turns all skins off.
    /// </summary>
    public sealed class SkinLayout
    {
        public static readonly string[] Fronts = { "+x", "-x", "+z", "-z" };
        public static readonly string[] Faces = { "front", "back", "left", "right", "top" };

        private readonly Dictionary<string, BlockSkin> blocks = new Dictionary<string, BlockSkin>();

        public bool TryGet(string blockName, out BlockSkin skin) => blocks.TryGetValue(blockName ?? "", out skin);

        public static SkinLayout Parse(string xml, SkinAtlas atlas, List<string> warnings)
        {
            XElement root = SkinXml.Load(xml, "skins");
            var layout = new SkinLayout();
            foreach (XElement b in root.Elements("block"))
            {
                string name = (string)b.Attribute("name");
                if (string.IsNullOrEmpty(name))
                {
                    warnings.Add("a <block> has no name; skipped");
                    continue;
                }
                string front = (string)b.Attribute("front");
                if (System.Array.IndexOf(Fronts, front) < 0)
                {
                    warnings.Add($"{name}: front '{front}' must be +x, -x, +z or -z; block skipped");
                    continue;
                }

                var pieces = new List<SkinPiece>();
                foreach (XElement p in b.Elements("piece"))
                {
                    SkinPiece piece = ReadPiece(name, p, atlas, warnings);
                    if (piece != null) pieces.Add(piece);
                }

                if (layout.blocks.ContainsKey(name)) warnings.Add($"{name}: listed twice; the last one wins");
                layout.blocks[name] = new BlockSkin { BlockName = name, Front = front, Pieces = pieces };
            }
            return layout;
        }

        private static SkinPiece ReadPiece(string block, XElement p, SkinAtlas atlas, List<string> warnings)
        {
            string sprite = (string)p.Attribute("sprite");
            if (!atlas.TryGetUv(sprite, out _))
            {
                warnings.Add($"{block}: unknown sprite '{sprite}'; piece skipped");
                return null;
            }
            string face = (string)p.Attribute("face");
            if (System.Array.IndexOf(Faces, face) < 0)
            {
                warnings.Add($"{block}: {sprite} has face '{face}' (use front, back, left, right or top); piece skipped");
                return null;
            }
            if (!SkinXml.TryNumber(p, "u", out float u) || !SkinXml.TryNumber(p, "v", out float v) ||
                !SkinXml.TryNumber(p, "w", out float w) || !SkinXml.TryNumber(p, "h", out float h))
            {
                warnings.Add($"{block}: {sprite} needs numbers for u, v, w and h; piece skipped");
                return null;
            }
            if (w <= 0 || h <= 0)
            {
                warnings.Add($"{block}: {sprite} has no size (w and h must be above 0); piece skipped");
                return null;
            }
            float rot = 0f;
            if (p.Attribute("rot") != null && !SkinXml.TryNumber(p, "rot", out rot))
            {
                warnings.Add($"{block}: {sprite} has a bad rot; piece skipped");
                return null;
            }
            bool glow = string.Equals((string)p.Attribute("glow"), "true", System.StringComparison.OrdinalIgnoreCase);
            return new SkinPiece { Sprite = sprite, Face = face, U = u, V = v, W = w, H = h, Rot = rot, Glow = glow };
        }
    }
}
