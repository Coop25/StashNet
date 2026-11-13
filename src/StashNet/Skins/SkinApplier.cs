using System.Collections.Generic;
using System.Linq;
using StashNet.Core.Skins;
using UnityEngine;
using UnityEngine.Rendering;

namespace StashNet.Skins
{
    /// <summary>
    /// Dresses a StashNet block's vanilla model in the salvage look: decals from Mod/Skins/layouts.xml placed
    /// on the model's bounding box, built into one mesh per material in a sibling object (see SkinOwner).
    /// Client only; does nothing on a dedicated server.
    /// </summary>
    internal static class SkinApplier
    {
        private static readonly HashSet<string> boundsLogged = new HashSet<string>();

        public static void Apply(Block block, BlockEntityData bed)
        {
            if (GameManager.IsDedicatedServer || block == null || bed == null || bed.transform == null) return;
            string blockName = block.GetBlockName();

            Transform model = bed.transform;
            Renderer sample = model.GetComponentsInChildren<Renderer>(true).FirstOrDefault();
            if (!SkinAssets.TryGet(sample, out SkinAtlas atlas, out SkinLayout layout, out Material normal, out Material glow)) return;
            if (!layout.TryGet(blockName, out BlockSkin skin)) return;
            if (!TryModelBounds(model, out Vec3 min, out Vec3 max)) return;
            if (boundsLogged.Add(blockName)) Log.Out($"[StashNet] Skin {blockName}: model bounds {min}..{max}");

            SkinOwner owner = model.GetComponent<SkinOwner>();
            if (owner == null) owner = model.gameObject.AddComponent<SkinOwner>();
            if (owner.Root != null) Object.Destroy(owner.Root);
            owner.BlockName = blockName;
            owner.Bed = bed;
            owner.Track();

            var root = new GameObject("StashNetSkin");
            root.layer = model.gameObject.layer;
            root.transform.SetParent(model.parent, false);
            root.transform.localPosition = model.localPosition;
            root.transform.localRotation = model.localRotation;
            root.transform.localScale = model.localScale;
            owner.Root = root;

            var plain = new List<(Quad, UvRect)>();
            var lit = new List<(Quad, UvRect)>();
            for (int i = 0; i < skin.Pieces.Count; i++)
            {
                SkinPiece p = skin.Pieces[i];
                if (!atlas.TryGetUv(p.Sprite, out UvRect uv)) continue;
                (p.Glow ? lit : plain).Add((FacePlacement.Place(min, max, skin.Front, p, i), uv));
            }
            AddMesh(root, "Skin", plain, normal, true);
            AddMesh(root, "SkinGlow", lit, glow, false);
        }

        /// <summary>Rebuild every live skin (after sn skin reload). Returns how many blocks were rebuilt.</summary>
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

        /// <summary>The model's meshes, boxed in the model's own local space.</summary>
        private static bool TryModelBounds(Transform model, out Vec3 min, out Vec3 max)
        {
            bool any = false;
            Vector3 lo = Vector3.zero, hi = Vector3.zero;
            Matrix4x4 toModel = model.worldToLocalMatrix;
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                Matrix4x4 m = toModel * mf.transform.localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { lo = hi = p; any = true; }
                    else { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
                }
            }
            min = new Vec3(lo.x, lo.y, lo.z);
            max = new Vec3(hi.x, hi.y, hi.z);
            return any;
        }

        private static void AddMesh(GameObject root, string name, List<(Quad quad, UvRect uv)> quads, Material material, bool receiveShadows)
        {
            if (quads.Count == 0) return;
            var verts = new List<Vector3>(quads.Count * 4);
            var normals = new List<Vector3>(quads.Count * 4);
            var uvs = new List<Vector2>(quads.Count * 4);
            var tris = new List<int>(quads.Count * 6);
            foreach ((Quad q, UvRect uv) in quads)
            {
                int b = verts.Count;
                verts.Add(V(q.BottomLeft)); verts.Add(V(q.BottomRight)); verts.Add(V(q.TopRight)); verts.Add(V(q.TopLeft));
                Vector3 n = V(q.Normal);
                normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
                uvs.Add(new Vector2(uv.U0, uv.V0)); uvs.Add(new Vector2(uv.U1, uv.V0));
                uvs.Add(new Vector2(uv.U1, uv.V1)); uvs.Add(new Vector2(uv.U0, uv.V1));
                // Clockwise as seen from outside = Unity's front face.
                tris.Add(b); tris.Add(b + 3); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            }

            var mesh = new Mesh { name = "StashNet" + name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.layer = root.layer;
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = receiveShadows;
            go.AddComponent<MeshCleanup>().Mesh = mesh;
        }

        private static Vector3 V(Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }

    /// <summary>Frees a skin's generated mesh with its object (meshes made in code aren't garbage-collected).</summary>
    public class MeshCleanup : MonoBehaviour
    {
        public Mesh Mesh;

        private void OnDestroy()
        {
            if (Mesh != null) Destroy(Mesh);
        }
    }
}
