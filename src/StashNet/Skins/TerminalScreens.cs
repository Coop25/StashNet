using System;
using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Protocol;
using StashNet.Core.Skins;
using StashNet.Network;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// PLAYER'S GAME: the live Storage Terminal screens. A terminal TV whose paint has live="true" registers
    /// here (SkinApplier.SetScreens) and leaves when its model is switched off (SkinOwner.OnDisable). This
    /// asks ScreenLink to watch each terminal position once, and puts the server's answers on the TVs:
    ///  - one texture set per network (clean + 2 damaged copies), shared by all its TVs, redrawn in place
    ///    when its numbers change and destroyed when its last TV leaves;
    ///  - one shared "locked out" set and one shared "no signal" set (static), made on first use, kept until
    ///    the world shuts down; those screens glitch 3x as often.
    /// Until the first answer a TV keeps the plain crtScreen picture. Never runs on a dedicated server.
    /// </summary>
    internal static class TerminalScreens
    {
        private const int W = ScreenPainter.Width, H = ScreenPainter.Height;

        private sealed class Tv
        {
            public Vector3i Pos;
            public ScreenGlitcher Glitcher;
            public string Fallback; // the crtScreen sprite, shown until (or when no longer) there is a set
            public float Strength;
        }

        private sealed class CoreScreen
        {
            public ScreenView View;   // last State; null until one arrives
            public Texture2D[] Set;   // made when a live (not shut, has core) view is first shown
            public ScreenView Drawn;  // what Set shows
            public float DrawnStrength;
        }

        private static readonly Dictionary<SkinOwner, Tv> tvs = new Dictionary<SkinOwner, Tv>();
        private static readonly Dictionary<Vector3i, int> tvsAt = new Dictionary<Vector3i, int>();
        /// <summary>Terminal position -> its Core (null = no network). Absent = no answer yet.</summary>
        private static readonly Dictionary<Vector3i, Pos3?> assigned = new Dictionary<Vector3i, Pos3?>();
        private static readonly TerminalScreenRefs<SkinOwner> refs = new TerminalScreenRefs<SkinOwner>();
        private static readonly Dictionary<Pos3, CoreScreen> cores = new Dictionary<Pos3, CoreScreen>();
        private static Texture2D[] shutSet, noSignalSet;

        /// <summary>A live terminal TV (again, after a reload or re-skin): glitch settings updated, current picture kept.</summary>
        internal static void Register(SkinOwner owner, BlockPaint paint)
        {
            if (GameManager.IsDedicatedServer || owner == null || owner.Bed == null || paint == null) return;
            try
            {
                Vector3i pos = owner.Bed.pos;
                if (tvs.TryGetValue(owner, out Tv tv) && tv.Pos != pos)
                {
                    Unregister(owner); // the model now stands for another terminal
                    tv = null;
                }
                if (tv == null)
                {
                    // Always a fresh one: an earlier one may still be waiting for Destroy at the end of this frame.
                    ScreenGlitcher g = owner.gameObject.AddComponent<ScreenGlitcher>();
                    g.Init(pos.GetHashCode() ^ owner.GetInstanceID());
                    tv = new Tv { Pos = pos, Glitcher = g };
                    tvs[owner] = tv;
                    tvsAt.TryGetValue(pos, out int n);
                    tvsAt[pos] = n + 1;
                    if (n == 0) ScreenLink.Watch(pos);
                    refs.Add(owner, assigned.TryGetValue(pos, out Pos3? core) ? core : null);
                }
                tv.Fallback = paint.Screen;
                tv.Strength = Mathf.Clamp01(paint.GlitchStrength);
                tv.Glitcher.Configure(paint);
                tv.Glitcher.SetMaterials(ScreenMaterials(owner));
                Refresh(tv);
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] Live screen for the terminal at {owner.Bed?.pos} failed: {e}");
            }
        }

        /// <summary>The TV's model is going away (or its paint is no longer live). Does not touch its materials.</summary>
        internal static void Unregister(SkinOwner owner)
        {
            if (GameManager.IsDedicatedServer || owner == null || !tvs.TryGetValue(owner, out Tv tv)) return;
            tvs.Remove(owner);
            if (tv.Glitcher != null)
            {
                tv.Glitcher.Drop();
                UnityEngine.Object.Destroy(tv.Glitcher);
            }
            Pos3? freed = refs.Remove(owner);
            if (freed.HasValue) DropCore(freed.Value);
            if (tvsAt.TryGetValue(tv.Pos, out int n) && n > 1)
            {
                tvsAt[tv.Pos] = n - 1;
                return;
            }
            tvsAt.Remove(tv.Pos);
            assigned.Remove(tv.Pos);
            ScreenLink.Unwatch(tv.Pos);
        }

        /// <summary>The terminal at <paramref name="terminal"/> belongs to the Core at <paramref name="core"/> (if hasCore).</summary>
        internal static void OnAssign(Vector3i terminal, bool hasCore, Vector3i core)
        {
            if (GameManager.IsDedicatedServer || !tvsAt.ContainsKey(terminal)) return;
            try
            {
                Pos3? c = hasCore ? ScreenPos.ToPos3(core) : (Pos3?)null;
                assigned[terminal] = c;
                foreach (KeyValuePair<SkinOwner, Tv> e in tvs.Where(e => e.Value.Pos == terminal).ToList())
                {
                    Pos3? freed = refs.Move(e.Key, c);
                    Refresh(e.Value); // show the new picture before the old set is destroyed
                    if (freed.HasValue) DropCore(freed.Value);
                }
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] Live screen assign for the terminal at {terminal} failed: {e}");
            }
        }

        /// <summary>The network at <paramref name="core"/> now looks like <paramref name="view"/>.</summary>
        internal static void OnState(Vector3i core, ScreenView view)
        {
            if (GameManager.IsDedicatedServer || view == null) return;
            try
            {
                Pos3 c = ScreenPos.ToPos3(core);
                IReadOnlyCollection<SkinOwner> owners = refs.TvsOf(c);
                if (owners.Count == 0) return; // no TV shows it: no textures
                if (!cores.TryGetValue(c, out CoreScreen cs)) cores[c] = cs = new CoreScreen();
                cs.View = view;
                foreach (SkinOwner o in owners.ToList())
                    if (tvs.TryGetValue(o, out Tv tv)) Refresh(tv);
            }
            catch (Exception e)
            {
                Log.Warning($"[StashNet] Live screen update for the network at {core} failed: {e}");
            }
        }

        /// <summary>Leaving the game: destroy every texture made here and forget every screen.</summary>
        internal static void Clear()
        {
            foreach (Tv tv in tvs.Values)
                if (tv.Glitcher != null)
                {
                    tv.Glitcher.Drop();
                    UnityEngine.Object.Destroy(tv.Glitcher);
                }
            foreach (CoreScreen cs in cores.Values) Destroy(cs.Set);
            Destroy(shutSet);
            Destroy(noSignalSet);
            shutSet = noSignalSet = null;
            tvs.Clear();
            tvsAt.Clear();
            assigned.Clear();
            refs.Clear();
            cores.Clear();
        }

        /// <summary>Puts the right picture on one TV.</summary>
        private static void Refresh(Tv tv)
        {
            if (tv.Glitcher == null) return;
            Texture2D[] set = null;
            bool restless = false;
            if (assigned.TryGetValue(tv.Pos, out Pos3? core))
            {
                if (!core.HasValue)
                {
                    set = NoSignalSet();
                    restless = true;
                }
                else if (cores.TryGetValue(core.Value, out CoreScreen cs) && cs.View != null)
                {
                    if (!cs.View.HasCore) { set = NoSignalSet(); restless = true; }
                    else if (cs.View.Shut) { set = ShutSet(tv.Strength); restless = true; }
                    else set = CoreSet(cs, tv.Strength);
                }
            }
            if (set != null) tv.Glitcher.Show(set, restless);
            // No numbers yet (or any more): the plain crtScreen picture, never a destroyed set.
            else tv.Glitcher.ShowStill(tv.Fallback != null ? SkinPainter.ScreenTexture(tv.Fallback) : null);
        }

        /// <summary>The TV's own per-renderer screen materials (the ones SetScreens paints).</summary>
        private static List<Material> ScreenMaterials(SkinOwner owner)
        {
            var list = new List<Material>();
            if (owner.Originals == null) return list;
            foreach (Material[] mats in owner.Originals)
                if (mats != null)
                    foreach (Material m in mats)
                        if (m != null && SkinPainter.IsScreen(m) && SkinPainter.IsPerRenderer(m) && !list.Contains(m)) list.Add(m);
            return list;
        }

        /// <summary>The network's own set, drawn (or redrawn in place) to match its last view.</summary>
        private static Texture2D[] CoreSet(CoreScreen cs, float strength)
        {
            if (cs.Set == null) cs.Set = NewSet("Network");
            if (cs.Drawn != cs.View || !Mathf.Approximately(cs.DrawnStrength, strength))
            {
                byte[] clean = ScreenPainter.Draw(cs.View);
                Upload(cs.Set[0], clean);
                Upload(cs.Set[1], ScreenGlitch.Damage(clean, W, H, 1, strength));
                Upload(cs.Set[2], ScreenGlitch.Damage(clean, W, H, 2, strength));
                cs.Drawn = cs.View;
                cs.DrawnStrength = strength;
            }
            return cs.Set;
        }

        private static Texture2D[] ShutSet(float strength)
        {
            if (shutSet != null) return shutSet;
            shutSet = NewSet("Locked");
            byte[] clean = ScreenPainter.Draw(ScreenView.ShutOut());
            Upload(shutSet[0], clean);
            Upload(shutSet[1], ScreenGlitch.Damage(clean, W, H, 1, strength));
            Upload(shutSet[2], ScreenGlitch.Damage(clean, W, H, 2, strength));
            return shutSet;
        }

        /// <summary>Three static frames with NO SIGNAL written over them.</summary>
        private static Texture2D[] NoSignalSet()
        {
            if (noSignalSet != null) return noSignalSet;
            noSignalSet = NewSet("NoSignal");
            byte[] text = ScreenPainter.Draw(ScreenView.NoSignal);
            var bg = ScreenPainter.Background;
            for (int f = 0; f < 3; f++)
            {
                byte[] px = ScreenGlitch.Static(W, H, f + 1);
                for (int i = 0; i < px.Length; i += 4)
                    if (text[i] != bg.R || text[i + 1] != bg.G || text[i + 2] != bg.B)
                    {
                        px[i] = text[i]; px[i + 1] = text[i + 1]; px[i + 2] = text[i + 2]; px[i + 3] = 255;
                    }
                Upload(noSignalSet[f], px);
            }
            return noSignalSet;
        }

        private static Texture2D[] NewSet(string name)
        {
            var set = new Texture2D[3];
            for (int i = 0; i < set.Length; i++)
                set[i] = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    name = $"StashNet_Screen{name}{i}",
                    wrapMode = TextureWrapMode.Repeat, // the glitch's jolt/roll shifts the offset
                    filterMode = FilterMode.Bilinear,  // like SkinPainter.ScreenTexture (Texture2D default)
                };
            return set;
        }

        /// <summary>Rows bottom-up, the order SetPixelData expects (same as the crtScreen crop).</summary>
        private static void Upload(Texture2D tex, byte[] rgba)
        {
            tex.SetPixelData(rgba, 0);
            tex.Apply(false);
        }

        private static void DropCore(Pos3 core)
        {
            if (!cores.TryGetValue(core, out CoreScreen cs)) return;
            cores.Remove(core);
            Destroy(cs.Set);
        }

        private static void Destroy(Texture2D[] set)
        {
            if (set == null) return;
            foreach (Texture2D t in set) if (t != null) UnityEngine.Object.Destroy(t);
        }
    }
}
