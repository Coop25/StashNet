using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using StashNet.Core.Skins;
using UnityEngine;
using Object = UnityEngine.Object;

namespace StashNet.Skins
{
    /// <summary>
    /// Makes the repainted copies of vanilla materials, from the game's own texture read back at runtime, so no
    /// game art ships with the mod. The pixel work runs on a background thread (it took ~1.6 s per texture on
    /// the main thread); until a texture is ready the block keeps its vanilla look, then it's re-applied.
    /// Copies are only ever put on StashNet blocks; vanilla materials are never changed.
    /// </summary>
    internal static class SkinPainter
    {
        private const string ScreenShader = "GameScreenFlicker";

        private sealed class Job
        {
            public string Block, Key, TexName;
            public byte[] Pixels;
            public int W, H;
            public TextureWrapMode Wrap;
            public FilterMode Filter;
            public int Aniso;
            public bool Grid, Linear;
            public volatile bool Done;
            public Exception Error;
            public long Ms;
        }

        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Job> jobs = new Dictionary<string, Job>();
        private static readonly HashSet<string> failed = new HashSet<string>();
        private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private static readonly List<Object> made = new List<Object>();
        private static int generation;

        /// <summary>sn skin grid: paint a lettered grid instead of the look, to find where each face is.</summary>
        public static bool GridMode;

        /// <summary>The materials a renderer should wear: repainted copies where ready, else the originals.</summary>
        public static Material[] Paint(string block, BlockPaint paint, Material[] originals)
        {
            var result = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                Material m = originals[i];
                result[i] = m;
                if (m == null || m.shader == null) continue;

                // A per-TV screen material ("(Instance)") is left in place and gets its picture swapped by
                // SkinApplier (the game drives that exact material; a copy of it rendered black in v0.10.3/4).
                if (IsScreen(m) && paint.Screen != null && !GridMode && !IsPerRenderer(m))
                    result[i] = ScreenCopy(block, m, paint.Screen) ?? m;
                else if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") is Texture tex && paint.TryGetTexture(tex.name, out TexturePaint tp))
                    result[i] = PaintedCopy(block, m, tex, tp) ?? m;
            }
            return result;
        }

        /// <summary>Called every frame (SkinRunner): turns finished jobs into textures and re-applies those blocks.</summary>
        public static void Pump()
        {
            if (jobs.Count == 0) return;
            var finished = new List<Job>();
            foreach (Job j in jobs.Values) if (j.Done) finished.Add(j);
            var blocks = new HashSet<string>();
            foreach (Job j in finished)
            {
                jobs.Remove(j.Key);
                if (j.Error != null)
                {
                    failed.Add(j.Key);
                    Log.Warning($"[StashNet] Skin repaint failed for {j.Block}/{j.TexName}: {j.Error.Message}");
                    continue;
                }
                var t = new Texture2D(j.W, j.H, TextureFormat.RGBA32, true, j.Linear) { name = $"StashNet_{j.TexName}", wrapMode = j.Wrap, filterMode = j.Filter, anisoLevel = j.Aniso };
                t.SetPixelData(j.Pixels, 0);
                t.Apply(true, true);
                textures[j.Key] = t;
                made.Add(t);
                blocks.Add(j.Block);
                Log.Out($"[StashNet] Skin {j.Block}: repainted {j.TexName} ({j.W}x{j.H}{(j.Grid ? ", grid" : "")}) in {j.Ms} ms (background)");
            }
            foreach (string b in blocks) SkinApplier.ReapplyBlock(b);
        }

        /// <summary>Drop every copy (sn skin reload / grid); the next Paint makes them again.</summary>
        public static void Clear()
        {
            generation++;          // background jobs from before are ignored when they finish
            jobs.Clear();
            failed.Clear();
            foreach (Object o in made) if (o != null) Object.Destroy(o);
            made.Clear();
            textures.Clear();
            materials.Clear();
        }

        private static Material PaintedCopy(string block, Material original, Texture tex, TexturePaint tp)
        {
            string texKey = $"{block}|{tex.name}|{GridMode}";
            string matKey = $"{texKey}|{original.name}";
            if (Alive(materials, matKey, out Material ready)) return ready;

            // The tint mask lives in _RMOM's alpha: masked parts show only the block's flat tint color (which
            // the game sets per renderer, overriding the material), never the painted texture. v0.10.3's
            // inspect showed it: the cabinet front stayed orange. A copy of _RMOM with the mask off fixes that.
            Texture rmom = tp.Mask >= 0 && original.HasProperty("_RMOM") ? original.GetTexture("_RMOM") : null;
            string maskKey = rmom != null ? $"{block}|{rmom.name}|mask{tp.Mask}" : null;

            bool waiting = false;
            if (!Alive(textures, texKey, out Texture2D painted))
            {
                waiting = true;
                if (!jobs.ContainsKey(texKey) && !failed.Contains(texKey)) StartPaint(block, texKey, tex, tp);
            }
            Texture2D mask = null;
            if (maskKey != null && !Alive(textures, maskKey, out mask))
            {
                waiting = true;
                if (!jobs.ContainsKey(maskKey) && !failed.Contains(maskKey)) StartMask(block, maskKey, rmom, (byte)tp.Mask);
            }
            if (waiting) return null;

            var copy = new Material(original) { name = $"StashNet_{original.name}" };
            copy.SetTexture("_MainTex", painted);
            if (mask != null) copy.SetTexture("_RMOM", mask);
            if (copy.HasProperty("_Color")) copy.SetColor("_Color", Color.white);
            materials[matKey] = copy;
            made.Add(copy);
            return copy;
        }

        /// <summary>
        /// A cached copy, if it still exists. The game deletes runtime-made materials when its model pool drops a
        /// model; a deleted copy shows pink (v0.10.4), so it's forgotten here and made again.
        /// </summary>
        private static bool Alive<T>(Dictionary<string, T> cache, string key, out T value) where T : Object
        {
            if (cache.TryGetValue(key, out value) && value != null) return true;
            cache.Remove(key);
            value = null;
            return false;
        }

        private static Job NewJob(string block, string key, Texture tex, bool linear)
        {
            var job = new Job
            {
                Block = block, Key = key, TexName = tex.name, W = tex.width, H = tex.height,
                Wrap = tex.wrapMode, Filter = tex.filterMode, Aniso = tex.anisoLevel, Grid = GridMode, Linear = linear,
            };
            try
            {
                job.Pixels = TextureReadback.ToRgba(tex, -1, linear);
                return job;
            }
            catch (Exception e)
            {
                failed.Add(key);
                Log.Warning($"[StashNet] Skin repaint failed for {block}/{tex.name}: {e.Message}");
                return null;
            }
        }

        private static void Run(Job job, Action work)
        {
            jobs[job.Key] = job;
            int gen = generation;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var timer = Stopwatch.StartNew();
                try
                {
                    work();
                }
                catch (Exception e)
                {
                    job.Error = e;
                }
                job.Ms = timer.ElapsedMilliseconds;
                if (gen == generation) job.Done = true;
            });
        }

        private static void StartMask(string block, string key, Texture rmom, byte value)
        {
            Job job = NewJob(block, key, rmom, true);
            if (job != null) Run(job, () => TextureRepaint.SetAlpha(job.Pixels, job.W, job.H, value));
        }

        private static void StartPaint(string block, string key, Texture tex, TexturePaint tp)
        {
            Job job = NewJob(block, key, tex, false);
            if (job == null) return;
            // Sprites are looked up here, on the main thread; the job only reads their pixels.
            Func<string, SpriteImage> sprite = SkinAssets.Sprite;
            var glyphs = new Dictionary<char, SpriteImage>();
            foreach (char c in "ABCDEFGH12345678") glyphs[c] = sprite("glyph" + c);
            var stamps = new List<(SpriteImage, TextureStamp)>();
            if (!job.Grid)
            {
                foreach (TextureStamp s in TextureRepaint.ScatterDamage(tp.Seed, tp.Damage, tp.Holes)) stamps.Add((sprite(s.Sprite), s));
                foreach (TextureStamp s in tp.Stamps) stamps.Add((sprite(s.Sprite), s));
            }

            Run(job, () =>
            {
                if (job.Grid)
                {
                    TextureRepaint.Grid(job.Pixels, job.W, job.H, c => glyphs.TryGetValue(c, out SpriteImage g) ? g : null);
                    return;
                }
                TextureRepaint.Recolor(job.Pixels, job.W, job.H, tp.R, tp.G, tp.B, tp.Strength);
                TextureRepaint.Grime(job.Pixels, job.W, job.H, tp.Seed, tp.Damage);
                foreach ((SpriteImage img, TextureStamp s) in stamps)
                    TextureRepaint.Stamp(job.Pixels, job.W, job.H, img, s.U, s.V, s.W, s.H, s.Rot);
            });
        }

        internal static bool IsScreen(Material m) => m != null && m.shader != null && m.shader.name == ScreenShader && m.HasProperty("_Albedo");

        /// <summary>The game's own per-renderer copy (safe to change: no other block uses it).</summary>
        internal static bool IsPerRenderer(Material m) => m.name.EndsWith("(Instance)");

        /// <summary>The screen picture as its own texture, made once.</summary>
        internal static Texture2D ScreenTexture(string sprite)
        {
            string key = $"screenTex|{sprite}";
            if (Alive(textures, key, out Texture2D tex)) return tex;
            byte[] px = SkinAssets.Crop(sprite, out int w, out int h);
            if (px == null || w == 0 || h == 0) return null;
            tex = TextureReadback.FromRgba(px, w, h, $"StashNet_{sprite}");
            tex.wrapMode = TextureWrapMode.Clamp;
            textures[key] = tex;
            made.Add(tex);
            return tex;
        }

        private static Material ScreenCopy(string block, Material original, string sprite)
        {
            string key = $"{block}|screen|{original.name}";
            if (Alive(materials, key, out Material ready)) return ready;
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
            materials[key] = copy;
            return copy;
        }
    }
}
