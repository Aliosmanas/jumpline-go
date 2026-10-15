using System.Collections.Generic;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Sonsuz harita zinciri (Subway Surfers akışı): önde birkaç 30 m'lik harita parçası hazır durur; koşucunun gerisinde
    /// kalan parça havuza döner, en öne sıradaki rastgele parça eklenir. Sıra: birkaç MAHALLE → GEÇİŞ (sur kapısı) →
    /// birkaç SUR ÜSTÜ (deniz) → GEÇİŞ → MAHALLE … Aynı parça arka arkaya gelmez. Işınlanmada zincir koşucunun önünde yeniden kurulur.
    /// Parçalar Blender/Harita tasarımından üretilen prefab'lerdir (GameplaySceneBuilder.Maps).
    /// </summary>
    public class MapChain : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameObject[] neighborhood;
        [SerializeField] private GameObject[] seaWalls;
        [SerializeField] private GameObject toSeaWalls;
        [SerializeField] private GameObject toNeighborhood;
        [SerializeField, Min(5f)] private float segmentLength = 30f;
        [Tooltip("Koşucunun önünde hazır duran parça sayısı (sis bunun ötesini gizler).")]
        [SerializeField, Min(2)] private int ahead = 6;
        [Tooltip("Arkada bırakılan parça sayısı (kamera geriye baktığında boşluk görünmesin).")]
        [SerializeField, Min(0)] private int behind = 1;
        [SerializeField] private Vector2Int neighborhoodRun = new Vector2Int(4, 7);
        [SerializeField] private Vector2Int seaWallRun = new Vector2Int(3, 5);
        [SerializeField] private int seed;

        private readonly List<(GameObject prefab, GameObject instance, float z)> live = new List<(GameObject, GameObject, float)>();
        private readonly Dictionary<GameObject, Stack<GameObject>> pool = new Dictionary<GameObject, Stack<GameObject>>();
        private System.Random rng;
        private bool inSea;
        private int runLeft;
        private GameObject last;
        private float nextZ;

        private void Awake() => rng = seed != 0 ? new System.Random(seed) : new System.Random();

        private void OnEnable()
        {
            if (player != null) Rebuild(player.position.z);
        }

        private void LateUpdate()
        {
            if (player == null) return;
            float pz = player.position.z;
            if (live.Count == 0 || pz < live[0].z - segmentLength || pz > nextZ + segmentLength) { Rebuild(pz); return; }   // ışınlanma
            while (live.Count > 0 && live[0].z + segmentLength < pz - behind * segmentLength) Release(0);
            while (nextZ < pz + ahead * segmentLength) Append();
        }

        /// <summary>Zinciri koşucunun bulunduğu yerden yeniden kurar (oyun başı / dünya geçişi).</summary>
        public void Rebuild(float playerZ)
        {
            while (live.Count > 0) Release(0);
            rng ??= new System.Random();
            inSea = false; runLeft = Next(neighborhoodRun); last = null;
            nextZ = Mathf.Floor(playerZ / segmentLength) * segmentLength - behind * segmentLength;
            while (nextZ < playerZ + ahead * segmentLength) Append();
        }

        private int Next(Vector2Int range) => rng.Next(range.x, range.y + 1);

        private GameObject PickFrom(GameObject[] set)
        {
            if (set == null || set.Length == 0) return null;
            if (set.Length == 1) return set[0];
            GameObject p;
            int guard = 0;
            do p = set[rng.Next(set.Length)]; while ((p == last || IsLive(p)) && guard++ < 12);
            return p;
        }

        private bool IsLive(GameObject prefab)
        {
            foreach (var l in live) if (l.prefab == prefab) return true;
            return false;
        }

        private GameObject NextPrefab()
        {
            if (runLeft > 0)
            {
                runLeft--;
                return PickFrom(inSea ? seaWalls : neighborhood);
            }
            // bölge bitti: geçiş parçası, sonra öbür bölge
            inSea = !inSea;
            runLeft = Next(inSea ? seaWallRun : neighborhoodRun);
            GameObject gate = inSea ? toSeaWalls : toNeighborhood;
            if (gate == null || (inSea ? seaWalls : neighborhood).Length == 0) { inSea = false; return PickFrom(neighborhood); }
            return gate;
        }

        private void Append()
        {
            GameObject prefab = NextPrefab();
            if (prefab == null) { nextZ += segmentLength; return; }
            GameObject inst = Take(prefab);
            inst.transform.SetPositionAndRotation(new Vector3(transform.position.x, transform.position.y, nextZ), Quaternion.identity);
            inst.SetActive(true);
            live.Add((prefab, inst, nextZ));
            last = prefab;
            nextZ += segmentLength;
        }

        private GameObject Take(GameObject prefab)
        {
            if (pool.TryGetValue(prefab, out var stack) && stack.Count > 0) return stack.Pop();
            var go = Instantiate(prefab, transform);
            go.name = prefab.name;
            return go;
        }

        private void Release(int index)
        {
            var (prefab, inst, _) = live[index];
            live.RemoveAt(index);
            inst.SetActive(false);
            if (!pool.TryGetValue(prefab, out var stack)) pool[prefab] = stack = new Stack<GameObject>();
            stack.Push(inst);
        }
    }
}
