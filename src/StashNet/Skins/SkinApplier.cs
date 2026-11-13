using System.Linq;
using StashNet.Core.Skins;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// Puts the salvage look on a StashNet block's vanilla model: swaps its materials for repainted copies
    /// (SkinPainter, settings in Mod/Skins/paint.xml), and mutes it if asked. Client only; does nothing on a
    /// dedicated server.
    /// </summary>
    internal static class SkinApplier
    {
        public static void Apply(Block block, BlockEntityData bed)
        {
            if (GameManager.IsDedicatedServer || block == null || bed == null || bed.transform == null) return;

            // Every StashNet model is tracked, painted or not, so sn skin dump/inspect/reload/grid can find it.
            SkinOwner owner = bed.transform.GetComponent<SkinOwner>();
            if (owner == null) owner = bed.transform.gameObject.AddComponent<SkinOwner>();
            if (owner.Originals == null) owner.CaptureOriginals();
            owner.BlockName = block.GetBlockName();
            owner.Bed = bed;
            owner.Track();

            Reapply(owner);
            SkinRunner.FollowUp(owner);
        }

        internal static void Reapply(SkinOwner owner)
        {
            BlockPaint paint = null;
            bool painted = SkinAssets.TryGet(out PaintSpec spec) && spec.TryGet(owner.BlockName, out paint);
            for (int i = 0; i < owner.Renderers.Length; i++)
            {
                Renderer r = owner.Renderers[i];
                if (r == null) continue;
                r.sharedMaterials = painted ? SkinPainter.Paint(owner.BlockName, paint, owner.Originals[i]) : owner.Originals[i];
            }
            SetScreens(owner, painted && !SkinPainter.GridMode ? paint.Screen : null);
            if (painted && paint.Mute)
            {
                // The CRT's hum comes from the game's AudioPlayer script: stop what's playing, then switch it off
                // so it can't start again (SkinOwner switches it back on if the model is reused).
                foreach (AudioPlayer p in owner.GetComponentsInChildren<AudioPlayer>(true))
                {
                    p.StopAudio();
                    if (p.enabled) owner.Silenced.Add(p);
                    p.enabled = false;
                }
                foreach (AudioSource a in owner.GetComponentsInChildren<AudioSource>(true)) a.mute = true;
            }
        }

        /// <summary>
        /// Puts our picture into the TV's own per-renderer screen material (or puts the original back when
        /// sprite is null). The original picture is remembered on the owner for Restore.
        /// </summary>
        private static void SetScreens(SkinOwner owner, string sprite)
        {
            Texture2D picture = sprite != null ? SkinPainter.ScreenTexture(sprite) : null;
            foreach (Material[] mats in owner.Originals)
                foreach (Material m in mats)
                {
                    if (!SkinPainter.IsScreen(m) || !SkinPainter.IsPerRenderer(m)) continue;
                    if (picture == null)
                    {
                        owner.RestoreScreen(m);
                        continue;
                    }
                    owner.RememberScreen(m);
                    m.SetTexture("_Albedo", picture);
                    m.SetTextureScale("_Albedo", Vector2.one);
                    m.SetTextureOffset("_Albedo", Vector2.zero);
                }
        }

        /// <summary>Redo every live block of one type (its repaint just finished).</summary>
        public static void ReapplyBlock(string blockName)
        {
            foreach (SkinOwner owner in SkinOwner.Live.ToList())
                if (owner != null && owner.Bed != null && owner.BlockName == blockName) Reapply(owner);
        }

        /// <summary>Redo every live block (after reload or grid). Returns how many blocks were redone.</summary>
        public static int ReapplyAll()
        {
            int n = 0;
            foreach (SkinOwner owner in SkinOwner.Live.ToList())
            {
                if (owner == null || owner.Bed == null) continue;
                Reapply(owner);
                n++;
            }
            return n;
        }
    }
}
