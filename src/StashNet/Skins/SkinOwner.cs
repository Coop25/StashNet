using System.Collections.Generic;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// Sits on a StashNet block's model and owns that model's skin object. The skin is a sibling of the
    /// model, not a child: the game tints every renderer under the model through "_Color"
    /// (BlockEntityData.SetMaterialColor), which would turn a blue stripe army green. When the model goes
    /// away (picked up, upgraded, chunk unloaded), this goes with it and takes the skin along.
    /// </summary>
    public class SkinOwner : MonoBehaviour
    {
        private static readonly HashSet<SkinOwner> live = new HashSet<SkinOwner>();

        public string BlockName;
        public GameObject Root;
        public BlockEntityData Bed;

        internal static IEnumerable<SkinOwner> Live => live;

        internal void Track() => live.Add(this);

        private void OnDestroy()
        {
            live.Remove(this);
            if (Root != null) Destroy(Root);
        }
    }
}
