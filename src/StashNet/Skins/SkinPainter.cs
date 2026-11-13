using System.Collections.Generic;
using System.Diagnostics;
using StashNet.Core.Skins;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// Makes the repainted copies of vanilla materials: once per (block, material, texture), from the game's own
    /// texture read back at runtime, so no game art ships with the mod. Copies are only ever put on StashNet
    /// blocks; the vanilla materials are never changed.
    /// </summary>
    internal static class SkinPainter
    {
        private const string ScreenShader = "GameScreenFlicker";

        private static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
        private static readonly List<Object> made = new List<Object>();

        /// <summary>sn skin grid: paint a lettered grid instead of the look, to find where each face is.</summary>
        public static bool GridMode;

        /// <summary>The materials a renderer should wear: repainted copies where paint.xml says so, else the originals.</summary>
        public static Material[] Paint(string block, BlockPaint paint, Material[] originals)
        {
            var result = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                Material m = originals[i];
                result[i] = m;
                if (m == null || m.shader == null) continue;

                if (m.shader.name == ScreenShader && paint.Screen != null && m.HasProperty("_Albedo") && !GridMode)
                    result[i] = Cached($"{block}|screen|{m.name}", () => ScreenCopy(m, paint.Screen)) ?? m;
                else if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") is Texture tex && paint.TryGetTexture(tex.name, out TexturePaint tp))
                    result[i] = Cached($"{block}|{m.name}|{tex.name}|{GridMode}", () => RepaintCopy(block, m, tex, tp)) ?? m;
            }
            return result;
        }

        /// <summary>Drop every copy (sn skin reload / grid); the next Paint makes them again.</summary>
        public static void Clear()
        {
            foreach (Object o in made) if (o != null) Object.Destroy(o);
            made.Clear();
            cache.Clear();
        }

        private static Material Cached(string key, System.Func<Material> make)
        {
            if (cache.TryGetValue(key, out Material m)) return m;
            try
            {
                m = make();
            }
            catch (System.Exception e)
            {
                Log.Warning($"[StashNet] Skin repaint failed for {key}: {e.Message}");
                m = null;
            }
            cache[key] = m;     // remember failures too, so a broken texture isn't retried every frame
            return m;
        }

        private static Material RepaintCopy(string block, Material original, Texture tex, TexturePaint tp)
        {
            var timer = Stopwatch.StartNew();
            int w = tex.width, h = tex.height;
            byte[] px = TextureReadback.ToRgba(tex);
            if (GridMode)
            {
                TextureRepaint.Grid(px, w, h, c => SkinAssets.Sprite("glyph" + c));
            }
            else
            {
                TextureRepaint.Recolor(px, w, h, tp.R, tp.G, tp.B, tp.Strength);
                TextureRepaint.Grime(px, w, h, tp.Seed, tp.Damage);
                foreach (TextureStamp s in TextureRepaint.ScatterDamage(tp.Seed, tp.Damage, tp.Holes)) Stamp(px, w, h, s);
                foreach (TextureStamp s in tp.Stamps) Stamp(px, w, h, s);
            }

            Texture2D painted = TextureReadback.FromRgba(px, w, h, $"StashNet_{tex.name}");
            painted.wrapMode = tex.wrapMode;
            painted.filterMode = tex.filterMode;
            painted.anisoLevel = tex.anisoLevel;
            var copy = new Material(original) { name = $"StashNet_{original.name}" };
            copy.SetTexture("_MainTex", painted);
            if (copy.HasProperty("_Color")) copy.SetColor("_Color", Color.white);
            made.Add(painted);
            made.Add(copy);
            Log.Out($"[StashNet] Skin {block}: repainted {tex.name} ({w}x{h}{(GridMode ? ", grid" : "")}) in {timer.ElapsedMilliseconds} ms");
            return copy;
        }

        private static void Stamp(byte[] px, int w, int h, TextureStamp s)
        {
            SpriteImage sprite = SkinAssets.Sprite(s.Sprite);
            if (sprite != null) TextureRepaint.Stamp(px, w, h, sprite, s.U, s.V, s.W, s.H, s.Rot);
        }

        private static Material ScreenCopy(Material original, string sprite)
        {
            byte[] px = SkinAssets.Crop(sprite, out int w, out int h);
            if (px == null || w == 0 || h == 0) return null;
            Texture2D screen = TextureReadback.FromRgba(px, w, h, $"StashNet_{sprite}");
            screen.wrapMode = TextureWrapMode.Clamp;
            var copy = new Material(original) { name = $"StashNet_{original.name}" };
            copy.SetTexture("_Albedo", screen);
            copy.SetTextureScale("_Albedo", Vector2.one);
            copy.SetTextureOffset("_Albedo", Vector2.zero);
            made.Add(screen);
            made.Add(copy);
            return copy;
        }
    }
}
