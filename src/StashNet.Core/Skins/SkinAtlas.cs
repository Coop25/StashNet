using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace StashNet.Core.Skins
{
    /// <summary>A sprite's texture coordinates, Unity convention (origin bottom-left, 0..1).</summary>
    public readonly struct UvRect
    {
        public readonly float U0, V0, U1, V1;

        public UvRect(float u0, float v0, float u1, float v1)
        {
            U0 = u0; V0 = v0; U1 = u1; V1 = v1;
        }
    }

    /// <summary>
    /// The index of Mod/Skins/stashnet-skin.png: sprite name -> pixel rect (origin top-left, as drawn by
    /// tools/skin/draw-skin-atlas.ps1), converted to UVs.
    /// </summary>
    public sealed class SkinAtlas
    {
        private readonly Dictionary<string, UvRect> sprites = new Dictionary<string, UvRect>();

        public IReadOnlyCollection<string> SpriteNames => sprites.Keys;

        public bool TryGetUv(string sprite, out UvRect uv) => sprites.TryGetValue(sprite ?? "", out uv);

        public static SkinAtlas Parse(string xml)
        {
            XElement root = SkinXml.Load(xml, "atlas");
            float width = SkinXml.Number(root, "width", "atlas");
            float height = SkinXml.Number(root, "height", "atlas");
            if (width <= 0 || height <= 0) throw new SkinFormatException("atlas: width and height must be positive");

            var atlas = new SkinAtlas();
            foreach (XElement s in root.Elements("sprite"))
            {
                string name = (string)s.Attribute("name");
                if (string.IsNullOrEmpty(name)) throw new SkinFormatException("atlas: a sprite has no name");
                float x = SkinXml.Number(s, "x", name), y = SkinXml.Number(s, "y", name);
                float w = SkinXml.Number(s, "w", name), h = SkinXml.Number(s, "h", name);
                if (w <= 0 || h <= 0) throw new SkinFormatException($"atlas: sprite {name} has no size");
                atlas.sprites[name] = new UvRect(x / width, 1f - (y + h) / height, (x + w) / width, 1f - y / height);
            }
            return atlas;
        }
    }

    internal static class SkinXml
    {
        internal static XElement Load(string xml, string rootName)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Parse(xml ?? "");
            }
            catch (XmlException e)
            {
                throw new SkinFormatException($"{rootName}: {e.Message}");
            }
            if (doc.Root == null || doc.Root.Name != rootName) throw new SkinFormatException($"expected a <{rootName}> root element");
            return doc.Root;
        }

        internal static float Number(XElement e, string attr, string where)
        {
            if (!TryNumber(e, attr, out float value)) throw new SkinFormatException($"{where}: missing or bad '{attr}'");
            return value;
        }

        internal static bool TryNumber(XElement e, string attr, out float value)
        {
            value = 0f;
            string text = (string)e.Attribute(attr);
            return text != null && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
