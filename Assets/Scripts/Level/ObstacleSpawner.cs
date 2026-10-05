using System.Collections.Generic;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Koşucunun önüne engel desenleri dizer, gerideki engelleri havuza geri alır (her dünya için bir tane).
    /// Desenler her zaman geçilebilir: en az bir şerit ya boştur ya da zıplanabilir; üç şeridi birden kapatan blok yok.
    ///  • Alçak (zıpla): dekor kasalar, sandık, testiler, çuval, karpuz, rulo halı
    ///  • Yüksek (tam şarjla zıpla): kebap arabası, semaver
    ///  • Duvar (şerit değiştir): demir kapı, oymalı sütun, şadırvan
    ///  • Rampa (sokak): rampalı kamyonet — rampadan çık, tavanda koş, önden in
    /// Çatıda zipline boşluğu ve tutunma bölgesi boş bırakılır; portal önü de boş bırakılır.
    /// </summary>
    public class ObstacleSpawner : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private WorldSwitcher switcher;
        [Tooltip("true: çatı dünyası (kamyon yok, zipline bölgesi boş).")]
        [SerializeField] private bool rooftop;
        [SerializeField] private Portal[] portals;

        [Header("Şablonlar (sahnede kapalı duran örnekler)")]
        [SerializeField] private Obstacle[] low;
        [SerializeField] private Obstacle[] high;
        [SerializeField] private Obstacle[] walls;
        [SerializeField] private Obstacle[] ramps;
        [Tooltip("Rampalı aracın boyu (z, m).")]
        [SerializeField, Min(1f)] private float rampLength = 11.6f;

        [Header("Yerleşim")]
        [SerializeField, Min(5f)] private float startDistance = 30f;
        [SerializeField, Min(20f)] private float spawnAhead = 110f;
        [SerializeField, Min(2f)] private float despawnBehind = 15f;
        [SerializeField] private Vector2 gap = new Vector2(20f, 32f);
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
            int guard = 0;
            while (nextZ < pz + spawnAhead && guard++ < 16)
                nextZ += SpawnPattern(nextZ) + Random.Range(gap.x, gap.y);
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
            if (rooftop && switcher != null)
            {
                float rel = Mathf.Repeat(z - switcher.RooftopRootZ, switcher.RooftopModuleLength);
                if (rel > rooftopNoSpawn.x && rel < rooftopNoSpawn.y) return rooftopNoSpawn.y - rel;
            }
            int a = Random.Range(-1, 2);
            int b = (a + Random.Range(1, 3) + 1) % 3 - 1;      // a'dan farklı şerit
            int c = -a - b;                                   // üçüncü şerit
            float roll = Random.value;
            bool canRamp = !rooftop && ramps != null && ramps.Length > 0;
            if (canRamp && roll < 0.22f)
            {
                Place(Pick(ramps), a, z);                                  // kamyon: rampadan çık
                if (Random.value < 0.6f) Place(Pick(walls), b, z + rampLength * 0.5f);
                if (Random.value < 0.5f) Place(Pick(low), c, z + 3f);
                return rampLength;
            }
            if (roll < 0.42f) { Place(Pick(low), a, z); return 0f; }                               // tek alçak
            if (roll < 0.60f) { Place(Pick(low), a, z); Place(Pick(low), b, z); return 0f; }       // iki alçak, biri boş
            if (roll < 0.78f) { Place(Pick(walls), a, z); Place(Pick(low), b, z); return 0f; }    // duvar + alçak
            if (roll < 0.88f) { Place(Pick(high), a, z); Place(Pick(walls), b, z); return 0f; }   // yüksek + duvar
            Place(Pick(low), a, z); Place(Pick(low), b, z); Place(Pick(low), c, z);                // üç şerit: zıpla
            return 0f;
        }

        private static Obstacle Pick(Obstacle[] set) => set == null || set.Length == 0 ? null : set[Random.Range(0, set.Length)];

        private void Place(Obstacle template, int lane, float z)
        {
            if (template == null || player == null) return;
            float x = OriginX + lane * player.LaneWidth;
            if (NearPortal(x, z)) return;
            Obstacle o = Take(template);
            o.transform.position = new Vector3(x, Ground, z);
            o.gameObject.SetActive(true);
            active.Add(o);
        }

        private bool NearPortal(float x, float z)
        {
            if (portals == null) return false;
            foreach (Portal p in portals)
            {
                if (p == null) continue;
                Vector3 pp = p.transform.position;
                if (Mathf.Abs(pp.x - x) < 1f && Mathf.Abs(pp.z - z) < portalClearance) return true;
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
