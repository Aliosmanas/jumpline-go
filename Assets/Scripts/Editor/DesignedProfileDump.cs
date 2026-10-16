using System.Text;
using Jumpline.Level;
using UnityEditor;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>Kontrol: tasarım haritası prefab'larındaki rampalı araç ve araç engellerinin çarpışma profilleri → günlük.</summary>
    public static class DesignedProfileDump
    {
        [MenuItem("Jumpline/Check/Designed Ramp Profiles")]
        public static void Dump()
        {
            var sb = new StringBuilder("[Jumpline] RAMPA PROFILLERI\n");
            int n = 0;
            foreach (string g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Designed" }))
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
                foreach (var o in p.GetComponentsInChildren<Obstacle>(true))
                {
                    if (!o.name.Contains("ramp_vehicle") || n++ > 3) continue;
                    var vis = o.transform.Find("Visual");
                    var rs = o.GetComponentsInChildren<Renderer>();
                    Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    sb.Append($"{p.name}/{o.name} root={o.transform.localPosition} visZ[{b.min.z - o.transform.position.z:0.0},{b.max.z - o.transform.position.z:0.0}] :");
                    foreach (var s in o.Segments) sb.Append($" [{s.z0:0.00}-{s.z1:0.00}:{s.h0:0.00}->{s.h1:0.00}]");
                    sb.Append('\n');
                }
            }
            Debug.Log(sb.ToString());
        }
    }
}
