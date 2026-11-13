using System.Collections.Generic;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// Sits on a StashNet block's model and remembers its vanilla materials, so the repaint can be redone
    /// (sn skin reload / grid) from the originals, and sn skin dump can save the vanilla textures.
    /// </summary>
    public class SkinOwner : MonoBehaviour
    {
        private static readonly HashSet<SkinOwner> live = new HashSet<SkinOwner>();

        public string BlockName;
        public BlockEntityData Bed;
        public Renderer[] Renderers;
        public Material[][] Originals;

        internal static IEnumerable<SkinOwner> Live => live;

        internal void Track() => live.Add(this);

        internal void CaptureOriginals()
        {
            Renderers = GetComponentsInChildren<Renderer>(true);
            Originals = new Material[Renderers.Length][];
            for (int i = 0; i < Renderers.Length; i++) Originals[i] = Renderers[i].sharedMaterials;
        }

        private void OnDestroy() => live.Remove(this);
    }
}
