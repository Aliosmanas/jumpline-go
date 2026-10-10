using System.Collections.Generic;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Koşucunun önüne Subway Surfers mantığında engel sıraları dizer; gerideki engelleri havuza geri alır (dünya başına bir tane).
    /// Her sıra bir HAREKET ister ve her zaman en az bir çözümü vardır (zıpla / kay / şerit değiştir / rampaya çık):
    ///  • Tren: bir şeritte rampalı kamyon, diğer iki şerit dolmuşlarla kapalı → rampaya çıkmak zorunlu; kamyondan
    ///    öndeki dolmuşların tavanına atlanır (araçtan araca). Bazen kamyonun üstünde sur kapısı → tavanda KAY.
    ///  • Alçak sıra (zıpla), bariyer sırası (kay), duvar + bariyer + alçak karışık, taksi sırası (üstüne zıpla),
    ///    sur kapısı altında engeller, zikzak (şerit değiştirerek ilerle).
    /// Aralar hızla birlikte açılır (tepki süresi sabit). Çatıda araç/kapı yok: aynı mantık dekor, bariyer ve duvarla.
    /// PARKUR MODU (course atanmışsa, sokak): elle dizilmiş bölümler (Assets/Data/Course_*.json, Blender/scripts/make_course_json.py)
    /// önce sırayla, sonra karışık sırayla ve her ikinci turda sağ-sol aynalı dizilir; satırlar hızla birlikte esner.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private WorldSwitcher switcher;
        [Tooltip("true: çatı dünyası (araç/kapı yok, zipline bölgesi boş).")]
        [SerializeField] private bool rooftop;
        [SerializeField] private Portal[] portals;
        [Tooltip("Sıraların arasına para dizileri koyar (Subway tarzı).")]
        [SerializeField] private CoinField coins;

        [System.Serializable]
        public struct AssetSet
        {
            [Tooltip("Parkur dosyasındaki engel kimliği (low, bus, melon…).")] public string id;
            [Tooltip("Engelin koşucuya bakan ön kenarından kökene mesafe (m).")] public float frontToOrigin;
            [Tooltip("Tüm şeritleri kaplar (sur kapısı): şerit yok sayılır.")] public bool spansLanes;
            [Tooltip("Boy (m). >0 ise araç sayılır: aynı şeritte arka arkaya (≤ chainGap) araçlar tren olur, esnemez.")] public float length;
            public Obstacle[] variants;
        }

        [Header("Parkur (boşsa rastgele desenler)")]
        [SerializeField] private TextAsset course;
        [SerializeField] private AssetSet[] assets;
        [Tooltip("Hız çarpanındaki her +1 için satır aralıklarının esneme oranı.")]
        [SerializeField, Min(0f)] private float stretchPerSpeed = 0.35f;
        [Tooltip("Aynı şeritte bu kadar yakın araçlar tek tren sayılır (esnemez; atlama boşlukları korunur).")]
        [SerializeField, Min(0f)] private float chainGap = 3.5f;
        [Tooltip("Parkurdaki 'portal' satırını yerleştiren portal (sokak → çatı).")]
        [SerializeField] private Portal coursePortal;

        [Header("Şablonlar (sahnede kapalı duran örnekler)")]
        [SerializeField] private Obstacle[] low;
        [SerializeField] private Obstacle[] high;
        [SerializeField] private Obstacle[] walls;
        [SerializeField] private Obstacle[] ramps;
        [SerializeField] private Obstacle[] cars;
        [SerializeField] private Obstacle[] buses;
        [SerializeField] private Obstacle[] gates;
        [SerializeField] private Obstacle[] bars;

        [Header("Araç ölçüleri (Blender build_obstacles / build_vehicles)")]
        [Tooltip("Rampalı kamyon boyu (z, m; rampa başı = 0).")]
        [SerializeField, Min(1f)] private float rampLength = 11.6f;
        [Tooltip("Dolmuş yarı boyu (merkezden, m).")]
        [SerializeField, Min(0.5f)] private float busHalfLength = 2.95f;
        [Tooltip("Kamyondan sonraki dolmuşa atlama boşluğu (m).")]
        [SerializeField, Min(0f)] private float trainGap = 2.5f;
        [Tooltip("Kamyon başından sur kapısına (kamyon tavanının ortası; tavanda kayarak geçilir).")]
        [SerializeField] private float gateOverTruck = 7f;

        [Header("Yerleşim")]
        [SerializeField, Min(5f)] private float startDistance = 30f;
        [SerializeField, Min(20f)] private float spawnAhead = 140f;
        [SerializeField, Min(2f)] private float despawnBehind = 15f;
        [Tooltip("Sıralar arası mesafe (taban hızda); hızla orantılı açılır.")]
        [SerializeField] private Vector2 gap = new Vector2(11f, 17f);

        /// <summary>LevelDirector: desenler arası boşluk (küçük = yoğun).</summary>
        public void SetGap(Vector2 value) => gap = value;
        [Tooltip("Portalın bu kadar önü/arkası boş kalır.")]
        [SerializeField, Min(0f)] private float portalClearance = 10f;
        [Tooltip("Çatıda engel konmayan modül içi aralık (zipline boşluğu + tutunma payı).")]
        [SerializeField] private Vector2 rooftopNoSpawn = new Vector2(28f, 70f);

        private readonly List<Obstacle> active = new List<Obstacle>();
        private readonly Dictionary<Obstacle, Stack<Obstacle>> pool = new Dictionary<Obstacle, Stack<Obstacle>>();
        private readonly Dictionary<Obstacle, Obstacle> sourceOf = new Dictionary<Obstacle, Obstacle>();
        private float nextZ = float.NaN;

        [System.Serializable] private class CourseData { public CourseSection[] sections; }
        [System.Serializable] private class CourseSection { public string name; public float length; public CourseRow[] rows; }
        [System.Serializable] private class CourseRow { public float z; public int lane; public string id; }

        private CourseData courseData;
        private readonly List<int> order = new List<int>();
        private int orderIndex;
        private int loop;

        /// <summary>Canavar kovalamasında parkurun portal satırları yok sayılır (çatıya canavarla birlikte gidilir).</summary>
        public bool IgnoreCoursePortal { get; set; }

        private float OriginX => switcher == null ? 0f : rooftop ? switcher.RooftopOriginX : switcher.StreetOriginX;
        private float Ground => switcher == null ? 0f : rooftop ? switcher.RooftopGround : switcher.StreetGround;

        private void Awake()
        {
            if (course != null) courseData = JsonUtility.FromJson<CourseData>(course.text);
        }

        private void OnEnable()
        {
            // dünyaya (yeniden) girildi: eski engelleri topla, ilk sıra koşucunun biraz önünde
            for (int i = active.Count - 1; i >= 0; i--) Release(active[i]);
            active.Clear();
            nextZ = float.NaN;
            if (coins != null) coins.Clear();
        }

        private void Update()
        {
            if (player == null) return;
            float pz = player.transform.position.z;
            if (float.IsNaN(nextZ)) nextZ = pz + startDistance;
            float scale = Mathf.Max(1f, player.SpeedFactor);
            int guard = 0;
            bool useCourse = !rooftop && courseData != null && courseData.sections != null && courseData.sections.Length > 0;
            while (nextZ < pz + spawnAhead * scale && guard++ < 16)
            {
                float from = nextZ;
                nextZ += useCourse ? SpawnSection(nextZ, scale) : SpawnPattern(nextZ) + Random.Range(gap.x, gap.y) * scale;
                if (coins != null) coins.Populate(from, nextZ, OriginX, Ground, player.LaneWidth);   // engeller yerleşti, paraları diz
            }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].MaxZ >= pz - despawnBehind) continue;
                Release(active[i]);
                active.RemoveAt(i);
            }
        }

        /// <summary>Elmasla devam: [z0, z1] aralığındaki engelleri kaldır (koşucu güvenle yeniden başlasın).</summary>
        public void ClearRange(float z0, float z1)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].MaxZ < z0 || active[i].MinZ > z1) continue;
                Release(active[i]);
                active.RemoveAt(i);
            }
        }

        /// <summary>Parkurdan sıradaki bölümü z'den başlayarak dizer; kapladığı uzunluğu döndürür.</summary>
        private float SpawnSection(float z, float speedScale)
        {
            if (orderIndex >= order.Count) NextLoop();
            CourseSection sec = courseData.sections[order[orderIndex++]];
            float stretch = 1f + stretchPerSpeed * (speedScale - 1f);
            bool mirror = loop % 2 == 1;
            // trenler: aynı şeritte art arda araçlar tek blok (iç boşluklar sabit); blok aralığındaki diğer satırlar da bloğa bağlı
            var chainStartRaw = new float[3];
            var chainEndRaw = new float[3];
            var chainStartZ = new float[3];
            for (int i = 0; i < 3; i++) chainEndRaw[i] = float.NegativeInfinity;
            foreach (CourseRow row in sec.rows)
            {
                int li = Mathf.Clamp(row.lane, -1, 1) + 1;
                bool isVehicle = TryGetSet(row.id, out AssetSet probe) && probe.length > 0f;
                float rz = z + row.z * stretch;
                if (isVehicle)
                {
                    if (row.z - chainEndRaw[li] <= chainGap) rz = chainStartZ[li] + (row.z - chainStartRaw[li]);   // trene eklendi
                    else
                    {
                        for (int k = 0; k < 3; k++)   // yan şeritte bir trenin boyunca başlıyorsa ona hizalı kalsın
                            if (k != li && row.z >= chainStartRaw[k] && row.z <= chainEndRaw[k]) { rz = chainStartZ[k] + (row.z - chainStartRaw[k]); break; }
                        chainStartRaw[li] = row.z;
                        chainStartZ[li] = rz;
                    }
                    chainEndRaw[li] = Mathf.Max(chainEndRaw[li], row.z + probe.length);
                }
                else
                {
                    for (int k = 0; k < 3; k++)   // bir trenin boyunca duran satır (sur kapısı, yan engel) trenle birlikte taşınır
                        if (row.z >= chainStartRaw[k] && row.z <= chainEndRaw[k]) { rz = chainStartZ[k] + (row.z - chainStartRaw[k]); break; }
                }
                int lane = mirror ? -row.lane : row.lane;
                if (row.id == "portal")
                {
                    if (coursePortal != null && !IgnoreCoursePortal) coursePortal.PlaceAt(rz, lane);
                    continue;
                }
                if (!TryGetSet(row.id, out AssetSet set)) continue;
                if (set.spansLanes) PlaceCenter(set.variants, rz + set.frontToOrigin);
                else Place(set.variants, lane, rz + set.frontToOrigin);
            }
            return sec.length * stretch;
        }

        /// <summary>İlk tur: dosya sırası. Sonraki turlar: giriş bölümü hariç karışık, portal meydanı hep sonda.</summary>
        private void NextLoop()
        {
            int n = courseData.sections.Length;
            if (order.Count > 0) loop++;
            order.Clear();
            orderIndex = 0;
            if (loop == 0)
            {
                for (int i = 0; i < n; i++) order.Add(i);
                return;
            }
            for (int i = 1; i < n - 1; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            order.Add(n - 1);
        }

        private bool TryGetSet(string id, out AssetSet set)
        {
            if (assets != null)
                foreach (AssetSet a in assets)
                    if (a.id == id && Has(a.variants)) { set = a; return true; }
            set = default;
            return false;
        }

        /// <summary>z'ye bir sıra koyar; sıranın kapladığı ek uzunluğu döndürür.</summary>
        private float SpawnPattern(float z)
        {
            int a = Random.Range(-1, 2);
            int b = (a + Random.Range(1, 3) + 1) % 3 - 1;      // a'dan farklı şerit
            int c = -a - b;                                   // üçüncü şerit
            float roll = Random.value;
            return rooftop ? RooftopPattern(z, a, b, c, roll) : StreetPattern(z, a, b, c, roll);
        }

        private float StreetPattern(float z, int a, int b, int c, float roll)
        {
            if (roll < 0.22f && Has(ramps) && Has(buses)) return Train(z, a, b, c);
            if (roll < 0.34f) { Place(buses, a, z + busHalfLength); Place(bars, b, z); Place(low, c, z); return 2 * busHalfLength; }   // kay ya da zıpla
            if (roll < 0.46f) { Place(low, a, z); Place(low, b, z); Place(low, c, z); return 0f; }                                    // zıpla
            if (roll < 0.56f) { Place(bars, a, z); Place(bars, b, z); Place(buses, c, z + busHalfLength); return 2 * busHalfLength; } // kay
            if (roll < 0.66f && Has(cars)) { Place(cars, a, z + 2.3f); Place(cars, b, z + 2.3f); Place(low, c, z); return 4.6f; }     // taksiye zıpla
            if (roll < 0.76f && Has(gates))
            {
                PlaceCenter(gates, z);                                                     // sur kapısı altında: zıpla / kay
                Place(low, a, z); Place(bars, b, z); Place(Random.value < 0.5f ? low : bars, c, z);
                return 1f;
            }
            if (roll < 0.86f)
            {
                Place(walls, a, z); Place(buses, b, z + busHalfLength);                     // kapalı iki şerit → geç, sonra zıpla
                Place(low, c, z + 7f);
                return 2 * busHalfLength + 1f;
            }
            if (roll < 0.94f) { Place(low, a, z); Place(bars, b, z + 6f); Place(low, c, z + 12f); return 12f; }   // zikzak
            Place(buses, a, z + busHalfLength); Place(buses, b, z + busHalfLength); Place(cars, c, z + 2.3f);     // taksiye çık
            return 2 * busHalfLength;
        }

        /// <summary>
        /// Tren: a şeridinde rampalı kamyon + önünde 1-2 dolmuş (tavandan tavana atla); b ve c şeritleri kamyon boyunca
        /// dolmuşlarla kapalı → rampa tek yol. %35 kamyon tavanında sur kapısı (tavanda kay).
        /// </summary>
        private float Train(float z, int a, int b, int c)
        {
            Place(ramps, a, z);
            float bus2 = 2f * busHalfLength + 0.15f;
            foreach (int side in new[] { b, c })
            {
                Place(buses, side, z + busHalfLength + 0.1f);
                Place(buses, side, z + busHalfLength + 0.1f + bus2);
            }
            float end = z + rampLength;
            int chain = Random.Range(1, 3);                          // kamyondan sonra dolmuşlara atla
            for (int i = 0; i < chain; i++)
            {
                float center = end + trainGap + busHalfLength;
                Place(buses, a, center);
                end = center + busHalfLength;
            }
            if (Has(gates) && Random.value < 0.35f) PlaceCenter(gates, z + gateOverTruck);
            return end - z;
        }

        private float RooftopPattern(float z, int a, int b, int c, float roll)
        {
            if (switcher != null)
            {
                float rel = Mathf.Repeat(z - switcher.RooftopRootZ, switcher.RooftopModuleLength);
                if (rel > rooftopNoSpawn.x && rel < rooftopNoSpawn.y) return rooftopNoSpawn.y - rel;
                if (rel + 13f > rooftopNoSpawn.x && rel < rooftopNoSpawn.x) return rooftopNoSpawn.y - rel;   // zikzak sığmaz
            }
            if (roll < 0.22f) { Place(low, a, z); Place(low, b, z); Place(low, c, z); return 0f; }                 // zıpla
            if (roll < 0.42f) { Place(bars, a, z); Place(bars, b, z); Place(walls, c, z); return 0f; }            // kay
            if (roll < 0.62f) { Place(walls, a, z); Place(bars, b, z); Place(low, c, z); return 0f; }             // kay / zıpla
            if (roll < 0.80f) { Place(low, a, z); Place(bars, b, z + 6f); Place(low, c, z + 12f); return 12f; }   // zikzak
            Place(walls, a, z); Place(high, b, z); Place(low, c, z + 6f);                                         // geç + zıpla
            return 6f;
        }

        private static bool Has(Obstacle[] set) => set != null && set.Length > 0;

        private void Place(Obstacle[] set, int lane, float z)
        {
            if (!Has(set) || player == null) return;
            float x = OriginX + lane * player.LaneWidth;
            if (NearPortal(x, z, false)) return;
            Spawn(set[Random.Range(0, set.Length)], x, z);
        }

        private void PlaceCenter(Obstacle[] set, float z)
        {
            if (!Has(set) || player == null || NearPortal(OriginX, z, true)) return;
            Spawn(set[Random.Range(0, set.Length)], OriginX, z);
        }

        private void Spawn(Obstacle template, float x, float z)
        {
            Obstacle o = Take(template);
            o.transform.position = new Vector3(x, Ground, z);
            o.enabled = true;   // güçle parçalanıp kapatılmış olabilir
            o.gameObject.SetActive(true);
            active.Add(o);
        }

        private bool NearPortal(float x, float z, bool anyLane)
        {
            if (portals == null) return false;
            foreach (Portal p in portals)
            {
                if (p == null) continue;
                Vector3 pp = p.transform.position;
                if ((anyLane ? Mathf.Abs(pp.x - x) < 6f : Mathf.Abs(pp.x - x) < 1f) && Mathf.Abs(pp.z - z) < portalClearance) return true;
            }
            return false;
        }

        private Obstacle Take(Obstacle template)
        {
            if (!pool.TryGetValue(template, out var stack)) pool[template] = stack = new Stack<Obstacle>();
            Obstacle o = stack.Count > 0 ? stack.Pop() : Instantiate(template, transform);
            sourceOf[o] = template;
            return o;
        }

        private void Release(Obstacle o)
        {
            if (o == null) return;
            o.gameObject.SetActive(false);
            if (sourceOf.TryGetValue(o, out var template)) pool[template].Push(o);
        }
    }
}
