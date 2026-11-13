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
    /// (shaders, texture slots, sizes). Goes to %APPDATA%\7DaysToDie\StashNetSkinDump\&lt;block&gt;\. This is what the texture
    /// repaint is painted from. Read-only for the game: nothing is changed.
    /// </summary>
    internal static class SkinDump
    {
        private const int UvSize = 1024;

        public static string Run()
        {
            // Outside the mod folder: installing a new version replaces Mods/StashNet and would delete it.
            string outDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7DaysToDie", "StashNetSkinDump");
            var done = new HashSet<string>();
            int files = 0;
            foreach (SkinOwner owner in SkinOwner.Live.ToList())
            {
                if (owner == null || owner.Bed == null || owner.Bed.transform == null || !done.Add(owner.BlockName)) continue;
                string dir = Path.Combine(outDir, owner.BlockName);
                Directory.CreateDirectory(dir);
                files += DumpModel(owner, dir);
            }
            return done.Count == 0
                ? "No StashNet blocks loaded nearby. Place one of each block you want, stand near them, and run it again."
                : $"Saved {files} files for {done.Count} block types ({string.Join(", ", done)}) to {outDir}";
        }

        /// <summary>Always the vanilla materials (the ones remembered before any repaint).</summary>
        private static int DumpModel(SkinOwner owner, string dir)
        {
            Transform model = owner.Bed.transform;
            int files = 0;
            var info = new StringBuilder();
            info.AppendLine($"block: {owner.BlockName}");
            info.AppendLine($"model: {model.name}  layer {model.gameObject.layer}  localScale {model.localScale}");
            var savedTextures = new HashSet<int>();

            Renderer[] renderers = owner.Renderers ?? model.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                if (rend == null) continue;
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

                Material[] mats = owner.Originals != null && r < owner.Originals.Length ? owner.Originals[r] : rend.sharedMaterials;
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

        private static void Save(Texture tex, int slice, string path) =>
            File.WriteAllBytes(path, TextureReadback.ToPng(TextureReadback.ToRgba(tex, slice), tex.width, tex.height));

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
