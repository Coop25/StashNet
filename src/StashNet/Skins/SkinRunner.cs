using System.Collections.Generic;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// One hidden object that ticks the skin system every frame: finishes background repaints, and re-applies a
    /// block's look a moment after its model appears (the game finishes setting some models up after our hook
    /// runs; v0.10.2's CRT screen stayed black until a manual reload).
    /// </summary>
    public class SkinRunner : MonoBehaviour
    {
        private const float FollowUpDelay = 0.5f;

        private static SkinRunner instance;
        private static readonly List<(SkinOwner owner, float at)> followUps = new List<(SkinOwner, float)>();

        internal static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("StashNetSkinRunner");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SkinRunner>();
        }

        internal static void FollowUp(SkinOwner owner)
        {
            Ensure();
            followUps.Add((owner, Time.time + FollowUpDelay));
        }

        private void Update()
        {
            SkinPainter.Pump();
            if (followUps.Count == 0) return;
            float now = Time.time;
            for (int i = followUps.Count - 1; i >= 0; i--)
            {
                (SkinOwner owner, float at) = followUps[i];
                if (now < at) continue;
                followUps.RemoveAt(i);
                if (owner != null && owner.Bed != null) SkinApplier.Reapply(owner);
            }
        }
    }
}
