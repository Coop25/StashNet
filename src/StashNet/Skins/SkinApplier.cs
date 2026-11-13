using System.Linq;
using StashNet.Core.Skins;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// Puts the salvage look on a StashNet block's vanilla model: swaps its materials for repainted copies
    /// (SkinPainter, settings in Mod/Skins/paint.xml). Client only; does nothing on a dedicated server.
    /// </summary>
    internal static class SkinApplier
    {
        public static void Apply(Block block, BlockEntityData bed)
        {
            if (GameManager.IsDedicatedServer || block == null || bed == null || bed.transform == null) return;
            string blockName = block.GetBlockName();

            // Every StashNet model is tracked, painted or not, so sn skin dump/reload/grid can find it.
            SkinOwner owner = bed.transform.GetComponent<SkinOwner>();
            if (owner == null) owner = bed.transform.gameObject.AddComponent<SkinOwner>();
            if (owner.Originals == null) owner.CaptureOriginals();
            owner.BlockName = blockName;
            owner.Bed = bed;
            owner.Track();

            BlockPaint paint = null;
            bool painted = SkinAssets.TryGet(out PaintSpec spec) && spec.TryGet(blockName, out paint);
            for (int i = 0; i < owner.Renderers.Length; i++)
            {
                Renderer r = owner.Renderers[i];
                if (r == null) continue;
                r.sharedMaterials = painted ? SkinPainter.Paint(blockName, paint, owner.Originals[i]) : owner.Originals[i];
            }
        }

        /// <summary>Redo every live block (after reload or grid). Returns how many blocks were redone.</summary>
        public static int ReapplyAll()
        {
            int n = 0;
            foreach (SkinOwner owner in SkinOwner.Live.ToList())
            {
                if (owner == null || owner.Bed == null) continue;
                Apply(owner.Bed.blockValue.Block, owner.Bed);
                n++;
            }
            return n;
        }
    }
}
