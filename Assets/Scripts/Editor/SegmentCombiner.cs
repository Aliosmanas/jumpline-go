using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Performans (aşama 14): bir yol/çatı modülündeki sabit mesh'leri malzemeye göre tek mesh'te birleştirir.
    /// Modül StreetRecycler ile bütün halinde taşındığı için birleşik mesh de onunla gider. Telefonda kare başına
    /// yüzlerce çizim çağrısı, malzeme sayısı kadar çağrıya iner.
    /// Birleştirilmez: modül kökü dışında betik (MonoBehaviour) ya da Animator taşıyan nesnelerin altı (kırılabilir
    /// kasa, zipline, portal…), deri/parçacık renderer'ları. Çarpışma kutuları yerinde kalır (yalnız renderer silinir).
    /// </summary>
    public static class SegmentCombiner
    {
        public static int Combine(Transform segment)
        {
            var groups = new Dictionary<(Material mat, ShadowCastingMode shadow), List<CombineInstance>>();
            var consumed = new List<MeshRenderer>();
            Matrix4x4 toLocal = segment.worldToLocalMatrix;
            foreach (MeshRenderer r in segment.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!r.enabled || HasBehaviour(r.transform, segment)) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Mesh mesh = mf.sharedMesh;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mesh.subMeshCount && i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var key = (mats[i], r.shadowCastingMode);
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = toLocal * r.localToWorldMatrix });
                }
                consumed.Add(r);
            }
            if (consumed.Count < 2) return 0;

            var root = new GameObject("Combined").transform;
            root.SetParent(segment, false);
            int n = 0;
            foreach (var kv in groups)
            {
                var combined = new Mesh { name = $"{segment.name}_{kv.Key.mat.name}" };
                long verts = 0;
                foreach (var ci in kv.Value) verts += ci.mesh.vertexCount;
                if (verts > 65000) combined.indexFormat = IndexFormat.UInt32;
                combined.CombineMeshes(kv.Value.ToArray(), true, true);
                combined.RecalculateBounds();
                var go = new GameObject("Combined_" + n++, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = combined;
                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key.mat;
                mr.shadowCastingMode = kv.Key.shadow;
                mr.receiveShadows = true;
            }
            foreach (MeshRenderer r in consumed)
            {
                var mf = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r);
                if (mf != null) Object.DestroyImmediate(mf);
            }
            return consumed.Count;
        }

        /// <summary>Nesne ya da modül köküne kadar bir atası betik/Animator taşıyorsa true (kök hariç).</summary>
        private static bool HasBehaviour(Transform t, Transform segment)
        {
            for (Transform p = t; p != null && p != segment; p = p.parent)
                if (p.GetComponent<MonoBehaviour>() != null || p.GetComponent<Animator>() != null) return true;
            return false;
        }
    }
}
