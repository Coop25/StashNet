using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using StashNet.Core.Skins;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StashNet.Skins
{
    /// <summary>
    /// sn skin dump: for each StashNet block type with a model loaded nearby, saves the textures its vanilla
    /// model really uses (as PNG), a map of where the model's faces sit on them (uv-*.png), and info.txt
    /// (shaders, texture slots, sizes). Goes to Mods/StashNet/SkinDump/&lt;block&gt;/. This is what the texture
    /// repaint is painted from. Read-only for the game: nothing is changed.
    /// </summary>
    internal static class SkinDump
    {
        private const int UvSize = 1024;

        public static string Run()
        {
            string outDir = Path.Combine(StashNetMod.ModPath ?? "", "SkinDump");
            var done = new HashSet<string>();
            int files = 0;
            foreach (SkinOwner owner in SkinOwner.Live.ToList())
            {
                if (owner == null || owner.Bed == null || owner.Bed.transform == null || !done.Add(owner.BlockName)) continue;
                string dir = Path.Combine(outDir, owner.BlockName);
                Directory.CreateDirectory(dir);
                files += DumpModel(owner.BlockName, owner.Bed.transform, dir);
            }
            return done.Count == 0
                ? "No StashNet blocks loaded nearby. Place one of each block you want, stand near them, and run it again."
                : $"Saved {files} files for {done.Count} block types ({string.Join(", ", done)}) to {outDir}";
        }

        private static int DumpModel(string blockName, Transform model, string dir)
        {
            int files = 0;
            var info = new StringBuilder();
            info.AppendLine($"block: {blockName}");
            info.AppendLine($"model: {model.name}  layer {model.gameObject.layer}  localScale {model.localScale}");
            var savedTextures = new HashSet<int>();

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                string rname = Safe($"{r:00}-{rend.name}");
                info.AppendLine();
                info.AppendLine($"renderer {rname}: {rend.GetType().Name}, enabled {rend.enabled}, path {PathOf(rend.transform, model)}");

                Mesh mesh = rend is SkinnedMeshRenderer sk ? sk.sharedMesh : rend.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) info.AppendLine("  mesh: none");
                else
                {
                    info.AppendLine($"  mesh: {mesh.name}, {mesh.vertexCount} verts, {mesh.subMeshCount} submeshes, readable {mesh.isReadable}, bounds {mesh.bounds}");
                    if (mesh.isReadable)
                    {
                        var uvs = new List<Vector2>();
                        mesh.GetUVs(0, uvs);
                        float[] us = uvs.Select(p => p.x).ToArray(), vs = uvs.Select(p => p.y).ToArray();
                        for (int s = 0; s < mesh.subMeshCount; s++)
                        {
                            bool[] mask = UvWireframe.Draw(UvSize, us, vs, mesh.GetTriangles(s));
                            SaveMask(mask, Path.Combine(dir, $"uv-{rname}-sub{s}.png"));
                            files++;
                        }
                    }
                }

                Material[] mats = rend.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) { info.AppendLine($"  material {m}: none"); continue; }
                    info.AppendLine($"  material {m} (submesh {m}): {mat.name}, shader '{mat.shader?.name}', queue {mat.renderQueue}, keywords [{string.Join(" ", mat.shaderKeywords)}]");
                    foreach (string prop in mat.GetTexturePropertyNames())
                    {
                        Texture tex = mat.GetTexture(prop);
                        if (tex == null) continue;
                        info.AppendLine($"    {prop}: {tex.name} ({tex.GetType().Name} {tex.width}x{tex.height}{Slices(tex)}) scale {mat.GetTextureScale(prop)} offset {mat.GetTextureOffset(prop)}");
                        if (!savedTextures.Add(tex.GetInstanceID())) continue;
                        files += SaveTexture(tex, Path.Combine(dir, Safe($"tex-{tex.name}")));
                    }
                    foreach (string c in new[] { "_Color", "_EmissionColor", "_TintColor" })
                        if (mat.HasProperty(c)) info.AppendLine($"    {c} = {mat.GetColor(c)}");
                }
            }

            File.WriteAllText(Path.Combine(dir, "info.txt"), info.ToString());
            return files + 1;
        }

        private static string Slices(Texture tex) => tex is Texture2DArray a ? $", {a.depth} slices" : "";

        /// <summary>Copies a (usually GPU-only) texture through a render texture so it can be saved.</summary>
        private static int SaveTexture(Texture tex, string pathNoExt)
        {
            try
            {
                if (tex is Texture2DArray arr)
                {
                    int n = Math.Min(arr.depth, 32);
                    for (int i = 0; i < n; i++) Save(tex, i, $"{pathNoExt}-slice{i:00}.png");
                    return n;
                }
                Save(tex, -1, pathNoExt + ".png");
                return 1;
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] skin dump: couldn't save {tex.name}: {e.Message}");
                return 0;
            }
        }

        private static void Save(Texture tex, int slice, string path)
        {
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture prev = RenderTexture.active;
            try
            {
                if (slice >= 0) Graphics.Blit(tex, rt, slice, 0);
                else Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                copy.Apply();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(copy));
                Object.Destroy(copy);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static void SaveMask(bool[] mask, string path)
        {
            var tex = new Texture2D(UvSize, UvSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[mask.Length];
            var on = new Color32(255, 255, 255, 255);
            var off = new Color32(0, 0, 0, 0);
            for (int i = 0; i < mask.Length; i++) pixels[i] = mask[i] ? on : off;
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
            Object.Destroy(tex);
        }

        private static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (Transform x = t; x != null && x != root; x = x.parent) parts.Insert(0, x.name);
            return parts.Count == 0 ? "(model root)" : string.Join("/", parts);
        }

        private static string Safe(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Replace(' ', '_');
        }
    }
}
