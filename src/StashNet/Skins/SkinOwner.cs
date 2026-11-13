using System.Collections.Generic;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// Sits on a StashNet block's model and remembers its vanilla materials. The game pools models: a removed
    /// block's model is switched off and later reused, maybe for a vanilla block, and when the pool drops a
    /// model it deletes every runtime-made material on it. So when the model is switched off, this puts the
    /// vanilla materials (and sound) back right away, keeping our shared repaint copies safe and vanilla reuses
    /// vanilla. When it's switched on again, the look comes back only if that spot still holds this StashNet block.
    /// </summary>
    public class SkinOwner : MonoBehaviour
    {
        private static readonly HashSet<SkinOwner> live = new HashSet<SkinOwner>();

        public string BlockName;
        public BlockEntityData Bed;
        public Renderer[] Renderers;
        public Material[][] Originals;
        internal readonly List<AudioPlayer> Silenced = new List<AudioPlayer>();
        private readonly Dictionary<Material, (Texture tex, Vector2 scale, Vector2 offset)> screens =
            new Dictionary<Material, (Texture, Vector2, Vector2)>();

        internal void RememberScreen(Material m)
        {
            if (!screens.ContainsKey(m)) screens[m] = (m.GetTexture("_Albedo"), m.GetTextureScale("_Albedo"), m.GetTextureOffset("_Albedo"));
        }

        internal void RestoreScreen(Material m)
        {
            if (m == null || !screens.TryGetValue(m, out var s)) return;
            m.SetTexture("_Albedo", s.tex);
            m.SetTextureScale("_Albedo", s.scale);
            m.SetTextureOffset("_Albedo", s.offset);
            screens.Remove(m);
        }

        internal static IEnumerable<SkinOwner> Live => live;

        internal void Track() => live.Add(this);

        internal void CaptureOriginals()
        {
            Renderers = GetComponentsInChildren<Renderer>(true);
            Originals = new Material[Renderers.Length][];
            for (int i = 0; i < Renderers.Length; i++) Originals[i] = Renderers[i].sharedMaterials;
        }

        /// <summary>Back to vanilla: original materials, sound players switched back on.</summary>
        internal void Restore()
        {
            if (Renderers != null)
                for (int i = 0; i < Renderers.Length; i++)
                    if (Renderers[i] != null) Renderers[i].sharedMaterials = Originals[i];
            foreach (AudioPlayer p in Silenced) if (p != null) p.enabled = true;
            Silenced.Clear();
            foreach (Material m in new List<Material>(screens.Keys)) RestoreScreen(m);
        }

        /// <summary>Does the world still hold this StashNet block at this model's spot?</summary>
        internal bool StillOurs()
        {
            World world = GameManager.Instance?.World;
            if (Bed == null || Bed.transform != transform || world == null) return false;
            return world.GetBlock(Bed.pos).Block?.GetBlockName() == BlockName;
        }

        private void OnDisable()
        {
            Restore();
            live.Remove(this);
        }

        private void OnEnable()
        {
            if (Originals != null) SkinRunner.Recheck(this);
        }

        private void OnDestroy() => live.Remove(this);
    }
}
