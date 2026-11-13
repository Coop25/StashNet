using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StashNet.Skins
{
    /// <summary>
    /// sn skin inspect: writes to the game log what each StashNet block's model looks like right now: its
    /// renderers (now vs. when we first saw it), the materials and textures they wear, any per-renderer
    /// overrides (property blocks), and the scripts on the model. For finding what undoes a repaint.
    /// </summary>
    internal static class SkinInspect
    {
        public static string Run()
        {
            var done = new HashSet<string>();
            foreach (SkinOwner owner in SkinOwner.Live.ToList())
            {
                if (owner == null || owner.Bed == null || owner.Bed.transform == null || !done.Add(owner.BlockName)) continue;
                Log.Out(Describe(owner));
            }
            return done.Count == 0
                ? "No StashNet blocks loaded nearby."
                : $"Inspected {done.Count} block types ({string.Join(", ", done)}); details are in the game log (lines starting [StashNet] inspect).";
        }

        private static string Describe(SkinOwner owner)
        {
            Transform model = owner.Bed.transform;
            var sb = new StringBuilder();
            sb.AppendLine($"[StashNet] inspect {owner.BlockName}: model '{model.name}' active {model.gameObject.activeInHierarchy}, bed.transform same object: {owner.transform == model}");
            Renderer[] live = model.GetComponentsInChildren<Renderer>(true);
            sb.AppendLine($"  renderers now {live.Length}, captured {owner.Renderers?.Length ?? 0}, same set: {owner.Renderers != null && live.SequenceEqual(owner.Renderers)}");
            var block = new MaterialPropertyBlock();
            foreach (Renderer r in live)
            {
                sb.AppendLine($"  {r.name}: {r.GetType().Name} enabled {r.enabled} visible {r.isVisible} propertyBlock {r.HasPropertyBlock()}");
                foreach (Material m in r.sharedMaterials)
                {
                    if (m == null) { sb.AppendLine("    material: none"); continue; }
                    string tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex")?.name : m.HasProperty("_Albedo") ? m.GetTexture("_Albedo")?.name : "-";
                    string col = m.HasProperty("_Color") ? m.GetColor("_Color").ToString() : "-";
                    sb.AppendLine($"    material {m.name} shader '{m.shader?.name}' tex {tex} _Color {col}");
                }
                if (r.HasPropertyBlock())
                {
                    r.GetPropertyBlock(block);
                    sb.AppendLine($"    propertyBlock: _MainTex {block.GetTexture("_MainTex")?.name ?? "(not set)"} _Color {block.GetColor("_Color")} empty {block.isEmpty}");
                }
            }
            var scripts = model.GetComponentsInChildren<MonoBehaviour>(true).Where(b => b != null).Select(b => b.GetType().FullName).Distinct();
            sb.AppendLine($"  scripts: {string.Join(", ", scripts)}");
            var audio = model.GetComponentsInChildren<AudioSource>(true);
            sb.Append($"  audio sources: {audio.Length} (muted {audio.Count(a => a.mute)})");
            return sb.ToString();
        }
    }
}
