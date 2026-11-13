using System;
using System.Collections.Generic;
using System.IO;
using StashNet.Core.Skins;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// Loads Mod/Skins (atlas pixels + index + paint.xml) once, lazily, on the client. Any failure logs one
    /// line and turns skins off until <see cref="Reload"/>; the game and the rest of StashNet keep working.
    /// </summary>
    internal static class SkinAssets
    {
        private static bool loaded;
        private static bool failed;
        private static SkinAtlas atlas;
        private static PaintSpec paint;
        private static byte[] atlasPixels;
        private static int atlasW, atlasH;
        private static readonly Dictionary<string, SpriteImage> sprites = new Dictionary<string, SpriteImage>();

        public static bool TryGet(out PaintSpec paintOut)
        {
            if (!loaded) Load();
            paintOut = failed ? null : paint;
            return !failed;
        }

        /// <summary>A sprite from the atlas, or null when the atlas has no such sprite.</summary>
        public static SpriteImage Sprite(string name)
        {
            if (sprites.TryGetValue(name, out SpriteImage s)) return s;
            if (atlas == null || !atlas.TryGetUv(name, out UvRect uv)) return null;
            return sprites[name] = new SpriteImage(atlasPixels, atlasW, atlasH, uv);
        }

        /// <summary>The sprite's own pixels (e.g. the CRT screen picture).</summary>
        public static byte[] Crop(string name, out int w, out int h)
        {
            w = h = 0;
            if (atlas == null || !atlas.TryGetUv(name, out UvRect uv)) return null;
            return TextureRepaint.Crop(atlasPixels, atlasW, atlasH, uv, out w, out h);
        }

        /// <summary>Forget everything; the next <see cref="TryGet"/> reads the files again (sn skin reload).</summary>
        public static void Reload()
        {
            loaded = failed = false;
            atlas = null; paint = null; atlasPixels = null;
            sprites.Clear();
        }

        private static void Load()
        {
            loaded = true;
            try
            {
                string dir = Path.Combine(StashNetMod.ModPath ?? "", "Skins");
                atlas = SkinAtlas.Parse(File.ReadAllText(Path.Combine(dir, "atlas.xml")));
                var warnings = new List<string>();
                paint = PaintSpec.Parse(File.ReadAllText(Path.Combine(dir, "paint.xml")), atlas, warnings);
                foreach (string w in warnings) Log.Warning($"[StashNet] Skin paint: {w}");

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(Path.Combine(dir, "stashnet-skin.png")), false))
                        throw new InvalidDataException("stashnet-skin.png isn't a readable PNG");
                    atlasW = tex.width;
                    atlasH = tex.height;
                    Color32[] c = tex.GetPixels32();
                    atlasPixels = new byte[c.Length * 4];
                    for (int i = 0; i < c.Length; i++)
                    {
                        atlasPixels[i * 4] = c[i].r; atlasPixels[i * 4 + 1] = c[i].g;
                        atlasPixels[i * 4 + 2] = c[i].b; atlasPixels[i * 4 + 3] = c[i].a;
                    }
                }
                finally
                {
                    UnityEngine.Object.Destroy(tex);
                }
            }
            catch (Exception e)
            {
                failed = true;
                Log.Error($"[StashNet] Skins disabled: {e.Message}");
            }
        }
    }
}
