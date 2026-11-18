using System.Collections.Generic;
using StashNet.Core.Skins;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// PLAYER'S GAME: one per live terminal TV (on its model, next to SkinOwner). Shows the TV's current
    /// picture set (clean + broken copies) on the TV's own per-renderer screen materials and runs the glitch:
    /// between glitches it only counts down; each glitch is a random GlitchRun (flicker, jolt, roll, tear or
    /// stretch, random strength, sometimes a quick second one), applied every 0.05 s as a texture swap and
    /// offset/scale, then frame 0, offset 0 and scale 1 go back.
    /// A NO SIGNAL set (static frames) is cycled through constantly instead, every 0.1 s.
    /// Added and removed by TerminalScreens; never on a dedicated server.
    /// </summary>
    public class ScreenGlitcher : MonoBehaviour
    {
        private const string Prop = "_Albedo";
        private const float Step = 0.05f;

        private readonly List<Material> materials = new List<Material>();
        private Texture2D[] set;
        private bool restless;
        private System.Random rng;
        private float minGap = 8f, maxGap = 30f, length = 0.4f, strength = 0.6f;

        private float wait;        // seconds until the next glitch
        private float glitchLeft;  // > 0 while glitching
        private float stepLeft;
        private GlitchRun run;     // the current glitch
        private int stepIndex;

        private const float CycleStep = 0.1f;
        private bool cycling;      // NO SIGNAL: the frames are static, stepped through all the time
        private int cycleFrame;
        private float cycleLeft;

        internal Texture2D[] Set => set;

        internal void Init(int seed)
        {
            if (rng == null) rng = new System.Random(seed);
        }

        internal void Configure(BlockPaint paint)
        {
            minGap = paint.GlitchMinGap;
            maxGap = paint.GlitchMaxGap;
            length = paint.GlitchLength;
            strength = Mathf.Clamp01(paint.GlitchStrength);
        }

        /// <summary>The screen materials this TV owns (replaces the list).</summary>
        internal void SetMaterials(IEnumerable<Material> mats)
        {
            materials.Clear();
            materials.AddRange(mats);
        }

        /// <summary>
        /// Shows a picture set (frame 0, no offset) and glitches it. The countdown keeps running unless the TV
        /// had no set or its restlessness changed. <paramref name="newCycling"/> (NO SIGNAL static) steps through
        /// the set's frames all the time, every 0.1 s (a texture swap, no redraw), glitches on top.
        /// </summary>
        internal void Show(Texture2D[] newSet, bool newRestless, bool newCycling = false)
        {
            if (newSet == null)
            {
                ShowStill(null);
                return;
            }
            bool timerReset = set == null || restless != newRestless;
            set = newSet;
            restless = newRestless;
            if (cycling != newCycling)
            {
                cycling = newCycling;
                cycleFrame = 0;
                cycleLeft = CycleStep;
            }
            if (cycleFrame >= set.Length) cycleFrame = 0;
            glitchLeft = 0f;
            Paint(RestFrame, Vector2.zero);
            if (timerReset) wait = NextGap();
        }

        /// <summary>The frame shown between glitches: 0, or the current static frame while cycling.</summary>
        private int RestFrame => cycling ? cycleFrame : 0;

        /// <summary>A still picture, no glitching (the crtScreen fallback). Null leaves the materials as they are.</summary>
        internal void ShowStill(Texture2D tex)
        {
            set = null;
            glitchLeft = 0f;
            Paint(tex, Vector2.zero);
        }

        /// <summary>Forget the materials and picture; does not touch the materials (the owner restores them).</summary>
        internal void Drop()
        {
            materials.Clear();
            set = null;
            glitchLeft = 0f;
            enabled = false;
        }

        private void Update()
        {
            if (set == null || materials.Count == 0) return;
            float dt = Time.deltaTime;
            if (glitchLeft <= 0f)
            {
                if (cycling)
                {
                    cycleLeft -= dt;
                    if (cycleLeft <= 0f)
                    {
                        cycleLeft = CycleStep;
                        cycleFrame = (cycleFrame + 1) % set.Length;
                        Paint(cycleFrame, Vector2.zero);
                    }
                }
                wait -= dt;
                if (wait > 0f) return;
                run = new GlitchRun(rng, length, strength, set.Length - 1);
                glitchLeft = run.Length;
                stepLeft = 0f;
                stepIndex = 0;
            }

            glitchLeft -= dt;
            if (glitchLeft <= 0f)
            {
                Paint(RestFrame, Vector2.zero);
                wait = run?.FollowUpGap ?? NextGap(); // about a third come in bursts
                return;
            }
            stepLeft -= dt;
            if (stepLeft > 0f) return;
            stepLeft = Step;

            GlitchFrame g = run.Step(stepIndex++, rng);
            Paint(g.Frame == GlitchFrame.Rest ? RestFrame : g.Frame, new Vector2(g.OffsetX, g.OffsetY), g.ScaleY);
        }

        private float NextGap()
        {
            float gap = ScreenGlitch.NextGap(rng, minGap, maxGap);
            return restless ? gap / 3f : gap;
        }

        private void Paint(int frame, Vector2 offset, float scaleY = 1f)
        {
            if (set != null && frame < set.Length) Paint(set[frame], offset, scaleY);
        }

        private void Paint(Texture2D tex, Vector2 offset, float scaleY = 1f)
        {
            if (tex == null) return;
            for (int i = materials.Count - 1; i >= 0; i--)
            {
                Material m = materials[i];
                if (m == null)
                {
                    materials.RemoveAt(i); // deleted by the game: forget it
                    continue;
                }
                m.SetTexture(Prop, tex);
                m.SetTextureScale(Prop, new Vector2(1f, scaleY));
                m.SetTextureOffset(Prop, offset);
            }
        }
    }
}
