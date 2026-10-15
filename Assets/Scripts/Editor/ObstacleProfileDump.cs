using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>Oyun sahnesindeki engel şablonlarının çarpışma profillerini ve görsel sınırlarını günlüğe yazar (kontrol için).</summary>
    public static class ObstacleProfileDump
    {
        [MenuItem("Jumpline/Dump Obstacle Profiles")]
        public static void Dump()
        {
            EditorSceneManager.OpenScene(GameplaySceneBuilder.ScenePath);
            var sb = new StringBuilder("[Jumpline] PROFILLER\n");
            foreach (var o in Resources.FindObjectsOfTypeAll<Jumpline.Level.Obstacle>())
            {
                if (!o.gameObject.scene.IsValid() || !o.name.StartsWith("Obs_")) continue;
                var rs = o.GetComponentsInChildren<Renderer>(true);
                Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds();
                foreach (var r in rs) b.Encapsulate(r.bounds);
                Vector3 p = o.transform.position;
                sb.Append($"{o.name} {o.Type} hw={o.HalfWidth:0.00} vis z[{b.min.z - p.z:0.0},{b.max.z - p.z:0.0}] y<{b.max.y - p.y:0.0}> :");
                foreach (var s in o.Segments) sb.Append($" [{s.z0:0.0}-{s.z1:0.0}:{s.h0:0.00}->{s.h1:0.00}]");
                sb.Append('\n');
            }
            Debug.Log(sb.ToString());
        }
    }
}
