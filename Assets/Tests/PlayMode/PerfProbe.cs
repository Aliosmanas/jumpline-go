using System.Collections;
using System.Linq;
using Jumpline.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>
    /// Ölçüm (test değil, elle çalıştırılır: -testFilter PerfProbe): oyun sahnesinde birkaç saniye koşup çizim yükünü
    /// loga yazar — telefonda kasmanın CPU (çizim çağrısı, gölge düşüren nesne) mi GPU mu olduğunu ayırmak için.
    /// </summary>
    [Explicit, Category("Perf")]
    public class PerfProbe
    {
        [UnityTest]
        public IEnumerator Gameplay_RenderLoad()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            GameSession.Mode = RunMode.Campaign;
            SceneManager.LoadScene("Gameplay");
            yield return new WaitForSeconds(4f);
            var renderers = Object.FindObjectsByType<Renderer>();
            var visible = renderers.Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            int casters = visible.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off);
            int skinned = visible.OfType<SkinnedMeshRenderer>().Count();
            int animators = Object.FindObjectsByType<Animator>().Count(a => a.isActiveAndEnabled);
            int lights = Object.FindObjectsByType<Light>().Count(l => l.isActiveAndEnabled);
            int particles = Object.FindObjectsByType<ParticleSystem>().Count(p => p.gameObject.activeInHierarchy);
            int mats = visible.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count();
            long tris = 0;
            foreach (var mf in Object.FindObjectsByType<MeshFilter>())
                if (mf.sharedMesh != null && mf.gameObject.activeInHierarchy) tris += mf.sharedMesh.triangles.Length / 3;
var planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);
            var inView = visible.Where(r => GeometryUtility.TestPlanesAABB(planes, r.bounds)).ToArray();
            int viewCasters = inView.Count(r => r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off);
            int subMeshes = inView.Sum(r => r.sharedMaterials.Length);
            string stats = $"kamerada={inView.Length} (alt-mesh={subMeshes}, gölgeli={viewCasters}) uzak={Camera.main.farClipPlane:F0}m";
            var top = visible.GroupBy(r => r.transform.root.name).OrderByDescending(g => g.Count()).Take(8)
                             .Select(g => $"{g.Key}:{g.Count()}");
            Debug.Log($"[PerfProbe] renderers={visible.Length} casters={casters} skinned={skinned} animators={animators} lights={lights} " +
                      $"particles={particles} materials={mats} meshTris={tris} {stats}\n[PerfProbe] kökler: {string.Join(", ", top)}");
            var street = GameObject.Find("Street");
            if (street != null)
            {
                var loose = street.GetComponentsInChildren<Renderer>().Where(r => !r.name.StartsWith("Combined_"))
                    .GroupBy(r => { var mb = r.GetComponentInParent<MonoBehaviour>(); return r.GetType().Name + "/" + (mb != null ? mb.GetType().Name : "-") + "/" + r.name; })
                    .OrderByDescending(g => g.Count()).Take(12).Select(g => $"{g.Key}:{g.Count()}");
                Debug.Log("[PerfProbe] birleşmeyen sokak: " + string.Join(", ", loose));
            }
            Assert.Pass();
        }
    }
}
