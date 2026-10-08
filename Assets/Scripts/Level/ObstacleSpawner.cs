using System.Collections.Generic;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Koşucunun önüne engel desenleri dizer, gerideki engelleri havuza geri alır (her dünya için bir tane).
    /// Desenler her zaman geçilebilir: en az bir şerit ya boştur, ya zıplanabilir, ya da üstüne çıkılabilir.
    ///  • Alçak (zıpla, çarpınca savrulur): sandık yığını, sandık, testiler, çuval, karpuz, halı…
    ///  • Yüksek (tam şarjla zıpla): kebap arabası, semaver
    ///  • Duvar (şerit değiştir): demir kapı, oymalı sütun, şadırvan
    ///  • Taksi (zıplayıp üstüne çıkılabilir) · Dolmuş (çıkılmaz, şerit değiştir)
    ///  • Rampalı kamyon: rampadan çık, tavanda koş — sur kapısı gelirse yana kaç (kafa çarpar)
    ///  • Sur kapısı (tavan): yerdeki koşucu altından geçer, tam güçle zıplayan ya da araç tavanındaki çarpar
    ///  • Kalas bariyer: altından KAY (aşağı kaydır) ya da üstünden zıpla
    /// Aralar hızla birlikte açılır (aynı tepki süresi). Çatıda yalnız dekor engeller; zipline bölgesi ve portal önü boş.
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
        [Tooltip("Rampalı aracın boyu (z, m).")]
        [SerializeField, Min(1f)] private float rampLength = 11.6f;
        [Tooltip("Kamyon başından sur kapısına mesafe (kamyon tavanının ortası).")]
        [SerializeField] private float gateOverTruck = 7f;

        [Header("Yerleşim")]
        [SerializeField, Min(5f)] private float startDistance = 28f;
        [SerializeField, Min(20f)] private float spawnAhead = 130f;
        [SerializeField, Min(2f)] private float despawnBehind = 15f;
        [Tooltip("Desenler arası mesafe (taban hızda); hızla orantılı açılır.")]
        [SerializeField] private Vector2 gap = new Vector2(14f, 22f);
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
            // dünyaya (yeniden) girildi: eski engelleri topla, ilk desen koşucunun biraz önünde
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

        /// <summary>z'ye bir desen koyar; desenin kapladığı ek uzunluğu döndürür.</summary>
        private float SpawnPattern(float z)
        {
            int a = Random.Range(-1, 2);
            int b = (a + Random.Range(1, 3) + 1) % 3 - 1;      // a'dan farklı şerit
            int c = -a - b;                                   // üçüncü şerit
            float roll = Random.value;

            if (rooftop)
            {
                if (switcher != null)
                {
                    float rel = Mathf.Repeat(z - switcher.RooftopRootZ, switcher.RooftopModuleLength);
                    if (rel > rooftopNoSpawn.x && rel < rooftopNoSpawn.y) return rooftopNoSpawn.y - rel;
                }
                if (roll < 0.18f && Has(bars)) { Place(bars, a, z); Place(bars, b, z); if (Random.value < 0.5f) Place(low, c, z); return 0f; }
                if (roll < 0.36f) { Place(low, a, z); Place(low, b, z); return 0f; }
                if (roll < 0.55f) { Place(walls, a, z); Place(low, b, z); return 0f; }
                if (roll < 0.75f) { Place(high, a, z); Place(walls, b, z); return 0f; }
                if (roll < 0.88f) { Place(low, a, z); Place(low, b, z); Place(low, c, z); return 0f; }
                Place(low, a, z); Place(high, b, z + 6f);
                return 6f;
            }

            if (roll < 0.17f && Has(ramps))
            {
                Place(ramps, a, z);                                                    // kamyon: rampadan çık
                if (Has(gates) && Random.value < 0.55f) PlaceCenter(gates, z + gateOverTruck);   // tavandayken kaç!
                if (Random.value < 0.6f) Place(Random.value < 0.5f ? buses : walls, b, z + 4f);
                if (Random.value < 0.4f) Place(low, c, z + 2f);
                return rampLength;
            }
            if (roll < 0.31f && Has(buses))
            {
                Place(buses, a, z);                                                    // dolmuş (çıkılmaz)
                Place(Has(cars) ? cars : high, b, z);                                  // taksi (üstüne zıpla)
                if (Random.value < 0.5f) Place(low, c, z - 2f);
                return 6f;
            }
            if (roll < 0.40f && Has(gates))
            {
                PlaceCenter(gates, z);                                                 // sur kapısı + altında alçak engel
                Place(low, a, z + 1.5f);
                if (Random.value < 0.6f) Place(low, b, z + 1.5f);
                return 2f;
            }
            if (roll < 0.50f && Has(bars))
            {
                Place(bars, a, z);                                                     // bariyer: kay ya da zıpla
                Place(Random.value < 0.6f ? bars : low, b, z);
                if (Random.value < 0.4f) Place(walls, c, z);
                return 0f;
            }
            if (roll < 0.56f) { Place(low, a, z); Place(Has(cars) ? cars : low, b, z + 1f); return 4f; }
            if (roll < 0.64f) { Place(low, a, z); Place(low, b, z); return 0f; }
            if (roll < 0.74f) { Place(walls, a, z); Place(low, b, z); if (Random.value < 0.35f) Place(cars, c, z + 1f); return 3f; }
            if (roll < 0.82f) { Place(high, a, z); Place(Has(buses) ? buses : walls, b, z); return 5f; }
            if (roll < 0.90f && Has(buses)) { Place(buses, a, z); Place(buses, b, z + 1f); return 6f; }
            Place(low, a, z); Place(low, b, z); Place(low, c, z);                       // üç şerit: zıpla
            return 0f;
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
