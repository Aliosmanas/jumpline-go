using System.Collections.Generic;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level.Designed
{
    /// <summary>
    /// Sokak: tasarım haritalarını (<see cref="DesignedMap"/>) koşucunun önüne arka arkaya dizer (16 Eki 2026, kullanıcı:
    /// "bu tarzda farklı kombinasyonlarla sürekli farklı haritalar"). Sıra rastgele, son birkaç harita tekrar etmez; birkaç
    /// mahalle haritasından sonra bir sur üstü (deniz) haritası gelir. Arkada kalan harita havuza döner (yeniden kullanılır).
    /// Her haritaya para dizileri (CoinField, engellere uyarak) ve — yeterince koşulduysa — tasarımdaki portal konur
    /// (canavar kovalamasında yok sayılır). Işınlanmada zincir koşucunun önünde yeniden kurulur.
    /// </summary>
    public class DesignedMapChain : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private CoinField coins;
        [Tooltip("Sokak → çatı portalı (tasarımdaki portal yerine konur).")]
        [SerializeField] private Portal coursePortal;
        [SerializeField] private DesignedMap[] maps = System.Array.Empty<DesignedMap>();
        [Tooltip("Koşucunun önünde hazır duran yol (m).")]
        [SerializeField, Min(40f)] private float ahead = 170f;
        [SerializeField, Min(5f)] private float behind = 25f;
        [Tooltip("Bu kadar mahalle haritasından sonra bir sur üstü haritası (aralık).")]
        [SerializeField] private Vector2Int seaEvery = new Vector2Int(3, 5);
        [Tooltip("Son bu kadar harita tekrar gelmez.")]
        [SerializeField, Min(0)] private int noRepeat = 8;
        [Tooltip("Bölüm başından bu kadar koşmadan portal konmaz (m).")]
        [SerializeField, Min(0f)] private float portalMinDistance = 600f;
        [Tooltip("Havuzda tutulan en fazla kopya (bellek).")]
        [SerializeField, Min(3)] private int poolLimit = 10;
        [SerializeField] private int seed;

        private readonly List<(DesignedMap prefab, DesignedMap inst, float z0, float z1)> live = new List<(DesignedMap, DesignedMap, float, float)>();
        private readonly Dictionary<DesignedMap, DesignedMap> pool = new Dictionary<DesignedMap, DesignedMap>();
        private readonly Queue<DesignedMap> recent = new Queue<DesignedMap>();
        private System.Random rng;
        private float nextZ;
        private float startZ = float.NaN;
        private int untilSea;
        private bool portalPlaced;
        private bool noObstacles;

        /// <summary>Canavar kovalamasında portal konmaz (çatıya canavarla birlikte gidilir).</summary>
        public bool IgnoreCoursePortal { get; set; }

        private float OriginX => player != null ? player.LaneOriginX : 0f;
        private float Ground => player != null ? player.GroundHeight : 0f;

        private void Awake() => rng = seed != 0 ? new System.Random(seed) : new System.Random();

        private void OnEnable()
        {
            if (player == null) return;
            // sokağa (yeniden) girildi: portal için yine en az portalMinDistance koşulur
            startZ = player.transform.position.z;
            portalPlaced = false;
            Rebuild(startZ);
        }

        private void LateUpdate()
        {
            if (player == null || maps.Length == 0) return;
            float pz = player.transform.position.z;
            if (live.Count == 0 || pz < live[0].z0 - 40f || pz > nextZ + 5f) { Rebuild(pz); return; }   // ışınlanma (zincir koşucunun 12 m önünden başlar)
            while (live.Count > 0 && live[0].z1 < pz - behind) Release(0);
            while (nextZ < pz + ahead) Append();
        }

        /// <summary>Zinciri koşucunun bulunduğu yerden yeniden kurar (oyun başı / dünyaya dönüş).</summary>
        public void Rebuild(float playerZ)
        {
            while (live.Count > 0) Release(0);
            rng ??= new System.Random();
            if (float.IsNaN(startZ)) startZ = playerZ;
            untilSea = Next(seaEvery);
            nextZ = playerZ + 12f;            // koşucunun önü birkaç metre boş başlar
            while (nextZ < playerZ + ahead) Append();
        }

        private int Next(Vector2Int r) => rng.Next(r.x, r.y + 1);

        private DesignedMap Pick()
        {
            bool sea = untilSea <= 0;
            var cands = new List<DesignedMap>();
            foreach (var m in maps)
                if (m != null && m.IsSeaOnly == sea && !recent.Contains(m) && !IsLive(m)) cands.Add(m);
            if (cands.Count == 0)
                foreach (var m in maps) if (m != null && !IsLive(m)) cands.Add(m);
            if (cands.Count == 0) return null;
            var p = cands[rng.Next(cands.Count)];
            if (p.IsSeaOnly) untilSea = Next(seaEvery); else untilSea--;
            recent.Enqueue(p);
            while (recent.Count > noRepeat) recent.Dequeue();
            return p;
        }

        private bool IsLive(DesignedMap prefab)
        {
            foreach (var l in live) if (l.prefab == prefab) return true;
            return false;
        }

        private void Append()
        {
            var prefab = Pick();
            if (prefab == null) { nextZ += 30f; return; }
            if (!pool.TryGetValue(prefab, out var inst) || inst == null)
            {
                inst = Instantiate(prefab, transform);
                if (pool.Count >= poolLimit) TrimPool();
                pool[prefab] = inst;
            }
            inst.transform.position = new Vector3(OriginX, Ground, nextZ);
            inst.ResetObstacles();
            inst.gameObject.SetActive(true);
            if (noObstacles) inst.ClearRange(-1e7f, 1e7f);
            float z0 = nextZ, z1 = nextZ + inst.Length;
            live.Add((prefab, inst, z0, z1));
            if (coins != null) coins.Populate(z0 + 4f, z1 - 4f, OriginX, Ground, player.LaneWidth);
            PlacePortal(inst, z0);
            nextZ = z1;
        }

        private void PlacePortal(DesignedMap inst, float z0)
        {
            if (coursePortal == null || IgnoreCoursePortal || portalPlaced || inst.Portals.Length == 0) return;
            if (z0 - startZ < portalMinDistance) return;
            var p = inst.Portals[0];
            coursePortal.PlaceAt(z0 + p.z, p.lane, Ground + p.y);
            portalPlaced = true;   // bölümde bir kez (girilse de kaçırılsa da; dünyaya dönünce yeniden)
        }

        private void Release(int i)
        {
            var l = live[i];
            live.RemoveAt(i);
            if (l.inst != null) l.inst.gameObject.SetActive(false);
        }

        private void TrimPool()
        {
            foreach (var kv in pool)
            {
                if (kv.Value == null || kv.Value.gameObject.activeSelf) continue;
                Destroy(kv.Value.gameObject);
                pool.Remove(kv.Key);
                return;
            }
        }

        /// <summary>Elmasla devam / portal önü: z aralığındaki engelleri kaldır.</summary>
        public void ClearRange(float z0, float z1)
        {
            foreach (var l in live) if (l.inst != null && l.z1 >= z0 && l.z0 <= z1) l.inst.ClearRange(z0, z1);
        }

        /// <summary>Testler / önizleme: haritalar (yol, dekor) gelmeye devam eder ama engelsiz.</summary>
        public void StopObstacles() => noObstacles = true;
    }
}
