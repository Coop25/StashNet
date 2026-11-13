using System;
using System.Collections.Generic;
using System.IO;
using StashNet.Core.Skins;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// Loads Mod/Skins (atlas PNG + index + layouts) once, lazily, on the client. Any failure logs one line
    /// and turns skins off until <see cref="Reload"/>; the game and the rest of StashNet keep working.
    /// </summary>
    internal static class SkinAssets
    {
        private static bool loaded;
        private static bool failed;
        private static SkinAtlas atlas;
        private static SkinLayout layout;
        private static Texture2D texture;
        private static Material normal;
        private static Material glow;

        /// <param name="sample">A renderer of the model being skinned; its shader is the first choice for decals.</param>
        public static bool TryGet(Renderer sample, out SkinAtlas atlasOut, out SkinLayout layoutOut, out Material normalOut, out Material glowOut)
        {
            atlasOut = null; layoutOut = null; normalOut = null; glowOut = null;
            if (!loaded) Load();
            if (failed) return false;
            if (normal == null && !PickMaterials(sample)) return false;

            atlasOut = atlas; layoutOut = layout; normalOut = normal; glowOut = glow;
            return true;
        }

        /// <summary>Forget everything; the next <see cref="TryGet"/> reads the files again (sn skin reload).</summary>
        public static void Reload()
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            if (normal != null) UnityEngine.Object.Destroy(normal);
            if (glow != null) UnityEngine.Object.Destroy(glow);
            loaded = failed = false;
            atlas = null; layout = null; texture = null; normal = null; glow = null;
        }

        private static void Load()
        {
            loaded = true;
            try
            {
                string dir = Path.Combine(StashNetMod.ModPath ?? "", "Skins");
                atlas = SkinAtlas.Parse(File.ReadAllText(Path.Combine(dir, "atlas.xml")));
                var warnings = new List<string>();
                layout = SkinLayout.Parse(File.ReadAllText(Path.Combine(dir, "layouts.xml")), atlas, warnings);
                foreach (string w in warnings) Log.Warning($"[StashNet] Skin layout: {w}");

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(Path.Combine(dir, "stashnet-skin.png")), false))
                    throw new InvalidDataException("stashnet-skin.png isn't a readable PNG");
                texture.name = "StashNetSkin";
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
            }
            catch (Exception e)
            {
                Disable(e.Message);
            }
        }

        private static bool PickMaterials(Renderer sample)
        {
            Shader normalShader = PickNormalShader(sample, out string why);
            Shader glowShader = Find(layout.GlowShader) ?? Find("Unlit/Transparent") ?? Find("Unlit/Transparent Cutout");
            if (normalShader == null || glowShader == null)
            {
                Disable("no usable shader");
                return false;
            }

            normal = MakeMaterial(normalShader, "StashNetSkin");
            glow = MakeMaterial(glowShader, "StashNetSkinGlow");
            Log.Out($"[StashNet] Skin shaders: decals '{normalShader.name}' ({why}), screens '{glowShader.name}'");
            return true;
        }

        private static Shader PickNormalShader(Renderer sample, out string why)
        {
            Shader s = Find(layout.Shader);
            if (s != null) { why = "layouts.xml override"; return s; }

            Material m = sample != null ? sample.sharedMaterial : null;
            if (m != null && m.shader != null && m.HasProperty("_MainTex") && m.HasProperty("_Cutoff"))
            {
                why = "model's own shader";
                return m.shader;
            }
            why = "fallback";
            return Find("Legacy Shaders/Transparent/Cutout/Diffuse") ?? Find("Unlit/Transparent Cutout");
        }

        private static Shader Find(string name) => string.IsNullOrEmpty(name) ? null : Shader.Find(name);

        private static Material MakeMaterial(Shader shader, string name)
        {
            var m = new Material(shader) { name = name, mainTexture = texture };
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
            if (shader.name == "Standard")
            {
                // Standard only clips with alpha test switched on.
                m.SetFloat("_Mode", 1f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            return m;
        }

        private static void Disable(string reason)
        {
            failed = true;
            Log.Error($"[StashNet] Skins disabled: {reason}");
        }
    }
}
