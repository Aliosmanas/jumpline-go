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
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private WorldSwitcher switcher;
        [Tooltip("true: çatı dünyası (araç/kapı yok, zipline bölgesi boş).")]
        [SerializeField] private bool rooftop;
        [SerializeField] private Portal[] portals;

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
        [Tooltip("Portalın bu kadar önü/arkası boş kalır.")]
        [SerializeField, Min(0f)] private float portalClearance = 10f;
        [Tooltip("Çatıda engel konmayan modül içi aralık (zipline boşluğu + tutunma payı).")]
        [SerializeField] private Vector2 rooftopNoSpawn = new Vector2(28f, 70f);

        private readonly List<Obstacle> active = new List<Obstacle>();
        private readonly Dictionary<Obstacle, Stack<Obstacle>> pool = new Dictionary<Obstacle, Stack<Obstacle>>();
        private readonly Dictionary<Obstacle, Obstacle> sourceOf = new Dictionary<Obstacle, Obstacle>();
        private float nextZ = float.NaN;

        private float OriginX => switcher == null ? 0f : rooftop ? switcher.RooftopOriginX : switcher.StreetOriginX;
        private float Ground => switcher == null ? 0f : rooftop ? switcher.RooftopGround : switcher.StreetGround;

        private void OnEnable()
        {
            // dünyaya (yeniden) girildi: eski engelleri topla, ilk sıra koşucunun biraz önünde
            for (int i = active.Count - 1; i >= 0; i--) Release(active[i]);
            active.Clear();
            nextZ = float.NaN;
        }

        private void Update()
        {
            if (player == null) return;
            float pz = player.transform.position.z;
            if (float.IsNaN(nextZ)) nextZ = pz + startDistance;
            float scale = Mathf.Max(1f, player.SpeedFactor);
            int guard = 0;
            while (nextZ < pz + spawnAhead * scale && guard++ < 16)
                nextZ += SpawnPattern(nextZ) + Random.Range(gap.x, gap.y) * scale;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].MaxZ >= pz - despawnBehind) continue;
                Release(active[i]);
                active.RemoveAt(i);
            }
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
