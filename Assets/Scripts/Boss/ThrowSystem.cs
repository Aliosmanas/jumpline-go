using System.Collections.Generic;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Jumpline.Boss
{
    /// <summary>
    /// Fırlatılabilir nesneler (karpuz, bakır testi): yolda süzülen toplanabilirler + elde tutma + atış.
    ///  • Yerleşim rastgele ama dengeli: belli aralıkla (m), engeli olmayan, düz zeminli, tavansız bir şeride konur;
    ///    elde nesne yokken ve canavar kaçmak üzereyken sıradaki daha yakına gelir (acıma kuralı).
    ///  • Atış (<see cref="Throw"/>, arayüz düğmesi ya da F/E tuşu) yalnız koşucunun bulunduğu şeride, ileri uçar;
    ///    canavarın hizasına varınca <see cref="BossChase.TryHit"/> sorulur. Atılırken canavara haber verilir (kaçınma).
    /// Canavar yoksa (Hızlı Yarış) kendini kapatır.
    /// </summary>
    public class ThrowSystem : MonoBehaviour
    {
        [System.Serializable]
        public struct Kind
        {
            public string id;
            [Tooltip("Yolda duran ve uçan görsel (kapalı şablon).")] public GameObject template;
            [Tooltip("Arayüz ikonu adı (UIKit Icons).")] public Sprite icon;
            [Tooltip("Çıkma ağırlığı.")] public float weight;
            [Tooltip("Kırılınca (karpuz: kırmızı-yeşil parçalar, testi: bakır kıymıklar). Boşsa genel efekt.")] public ParticleSystem smashFx;
        }

        [SerializeField] private PlayerController player;
        [SerializeField] private BossChase boss;
        [SerializeField] private Kind[] kinds;

        [Header("Yerleşim (m)")]
        [Tooltip("İki nesne arası koşu mesafesi.")]
        [SerializeField] private Vector2 spacing = new Vector2(120f, 170f);
        [Tooltip("Elde nesne yokken ve canavar uzaklaşmışken (Gap01 ≥ pityAt) aralık bu kadar kısalır.")]
        [SerializeField] private Vector2 pitySpacing = new Vector2(45f, 65f);
        [SerializeField, Range(0f, 1f)] private float pityAt = 0.5f;
        [Tooltip("Final (köprü): nesneler daha sık çıkar.")]
        [SerializeField] private Vector2 finaleSpacing = new Vector2(55f, 80f);
        [SerializeField, Min(20f)] private float spawnAhead = 85f;
        [SerializeField, Min(0.3f)] private float hoverHeight = 1.0f;
        [SerializeField, Range(0f, 1f)] private float preferPlayerLane = 0.35f;
        [SerializeField, Min(1)] private int maxHeld = 3;

        [Header("Toplama / görsel")]
        [SerializeField, Min(0.2f)] private float pickupRadius = 1.0f;
        [SerializeField] private float spinSpeed = 120f;
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private ParticleSystem pickupFx;
        [SerializeField] private ParticleSystem smashFx;

        [Header("Atış")]
        [Tooltip("Koşucuya göre ileri hız (m/sn).")]
        [SerializeField, Min(5f)] private float throwSpeed = 30f;
        [Tooltip("Hedef yükseklik (canavarın göğsü, ayaktan).")]
        [SerializeField, Min(0.5f)] private float aimHeight = 2.4f;
        [SerializeField, Min(0f)] private float arcHeight = 1.2f;
        [Tooltip("Canavar yoksa/ıskalarsa bu kadar uçup düşer.")]
        [SerializeField, Min(5f)] private float maxRange = 60f;
        [SerializeField, Min(0f)] private float throwCooldown = 0.35f;

        private struct Pickup { public Transform t; public int kind; public float baseY; }
        private struct Shot { public Transform t; public int kind; public float x; public float z; public float y0; public float travelled; public float total; public bool resolved; }

        private readonly List<Pickup> pickups = new List<Pickup>();
        private readonly List<Shot> shots = new List<Shot>();
        private readonly Dictionary<int, Stack<Transform>> pool = new Dictionary<int, Stack<Transform>>();
        private readonly Stack<int> held = new Stack<int>();
        private float nextSpawnDistance;
        private bool finale;
        private float lastThrow = -10f;

        /// <summary>Elindeki nesne sayısı / en üstteki nesnenin türü değişti.</summary>
        public event System.Action Changed;
        public event System.Action Thrown;
        public event System.Action PickedUp;

        public int Count => held.Count;
        /// <summary>Bu koşuda yapılan atış sayısı (yıldız: az atışla yenmek).</summary>
        public int ThrowsMade { get; private set; }
        public Sprite CurrentIcon => held.Count > 0 && kinds[held.Peek()].icon != null ? kinds[held.Peek()].icon : null;
        public bool CanThrow => held.Count > 0 && player != null && !player.IsDead && Time.time - lastThrow >= throwCooldown;

        private void Awake()
        {
            if (boss == null || !boss.gameObject.activeSelf || kinds == null || kinds.Length == 0) gameObject.SetActive(false);
        }

        private void Start()
        {
            nextSpawnDistance = player.Distance + Random.Range(spacing.x, spacing.y) * 0.4f;   // ilki erken gelsin
            Changed?.Invoke();
        }

        private void Update()
        {
            if (player == null) return;
            var kb = Keyboard.current;
            if (kb != null && (kb.fKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) Throw();
            UpdateSpawning();
            UpdatePickups();
            UpdateShots(Time.deltaTime);
        }

        // ------------------------------------------------------------------
        // Yerleşim
        // ------------------------------------------------------------------

        private void UpdateSpawning()
        {
            if (boss == null || !boss.IsChasing) return;
            bool pity = held.Count == 0 && pickups.Count == 0 && boss.Gap01 >= pityAt;
            if (pity) nextSpawnDistance = Mathf.Min(nextSpawnDistance, player.Distance + Random.Range(pitySpacing.x, pitySpacing.y));
            if (player.Distance < nextSpawnDistance) return;
            Vector2 sp = finale ? finaleSpacing : spacing;
            if (TrySpawn()) nextSpawnDistance = player.Distance + Random.Range(sp.x, sp.y);
            else nextSpawnDistance = player.Distance + 6f;   // uygun yer yok: biraz sonra yeniden dene
        }

        private bool TrySpawn()
        {
            float z = player.transform.position.z + spawnAhead;
            float g = player.GroundHeight;
            int first = Random.value < preferPlayerLane ? player.Lane : Random.Range(-1, 2);
            for (int i = 0; i < 3; i++)
            {
                int lane = (first + 1 + i) % 3 - 1;
                float x = player.LaneOriginX + lane * player.LaneWidth;
                if (ObstacleField.AnySolid(x, 0.9f, z - 3f, z + 3f)) continue;
                if (Mathf.Abs(ObstacleField.SurfaceAt(x, z, g) - g) > 0.05f) continue;
                if (ObstacleField.HitsCeiling(x, z, g, g + 2.2f)) continue;
                int kind = PickKind();
                Transform t = Take(kind);
                float y = g + hoverHeight;
                t.position = new Vector3(x, y, z);
                var trail = t.GetComponentInChildren<TrailRenderer>();
                if (trail != null) trail.Clear();   // havuzdan gelirken eski yerden iz çekmesin
                pickups.Add(new Pickup { t = t, kind = kind, baseY = y });
                return true;
            }
            return false;
        }

        private int PickKind()
        {
            float sum = 0f;
            foreach (Kind k in kinds) sum += Mathf.Max(0f, k.weight);
            float r = Random.value * sum;
            for (int i = 0; i < kinds.Length; i++)
            {
                r -= Mathf.Max(0f, kinds[i].weight);
                if (r <= 0f) return i;
            }
            return 0;
        }

        private void UpdatePickups()
        {
            Vector3 p = player.transform.position;
            float head = player.IsSliding ? 1.0f : 1.7f;
            float bob = Mathf.Sin(Time.time * 3f) * bobAmplitude;
            Quaternion spin = Quaternion.Euler(0f, spinSpeed * Time.deltaTime, 0f);
            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                Pickup pk = pickups[i];
                Vector3 cp = pk.t.position;
                bool otherWorld = Mathf.Abs(pk.baseY - (player.GroundHeight + hoverHeight)) > 1f;   // portaldan geçildi
                if (cp.z < p.z - 10f || otherWorld) { Release(pk.kind, pk.t); pickups.RemoveAt(i); continue; }
                cp.y = pk.baseY + bob;
                pk.t.position = cp;
                pk.t.rotation = spin * pk.t.rotation;
                if (player.IsDead) continue;
                if (Mathf.Abs(cp.x - p.x) < pickupRadius && Mathf.Abs(cp.z - p.z) < pickupRadius && cp.y > p.y - 0.4f && cp.y < p.y + head + 0.4f)
                {
                    if (pickupFx != null) { pickupFx.transform.position = cp; pickupFx.Emit(18); }
                    Release(pk.kind, pk.t);
                    pickups.RemoveAt(i);
                    if (held.Count < maxHeld) held.Push(pk.kind);
                    Haptics.Pulse(Haptics.Light, 120);
                    PickedUp?.Invoke();
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>Final arenası: daha sık nesne; ilki hemen gelir.</summary>
        public void SetFinale(bool on)
        {
            finale = on;
            if (on) nextSpawnDistance = player.Distance + 25f;
        }

        /// <summary>Dünya değişti / devam edildi: yoldaki nesneleri topla (elindekiler kalır).</summary>
        public void ClearTrack()
        {
            foreach (Pickup pk in pickups) Release(pk.kind, pk.t);
            pickups.Clear();
            nextSpawnDistance = player.Distance + 20f;
        }

        // ------------------------------------------------------------------
        // Atış
        // ------------------------------------------------------------------

        /// <summary>Elindeki en üstteki nesneyi bulunduğun şeride, ileri at.</summary>
        public void Throw()
        {
            if (!CanThrow) return;
            lastThrow = Time.time;
            ThrowsMade++;
            int kind = held.Pop();
            Vector3 p = player.transform.position;
            float x = player.LaneOriginX + player.Lane * player.LaneWidth;   // hedef şerit (geçiş sırasında da)
            Transform t = Take(kind);
            t.position = new Vector3(x, p.y + 1.5f, p.z + 0.8f);
            float total = boss != null && boss.Visible && boss.IsChasing ? Mathf.Max(1f, boss.Gap - 0.8f) : maxRange;
            shots.Add(new Shot { t = t, kind = kind, x = x, z = p.z + 0.8f, y0 = p.y + 1.5f, travelled = 0f, total = total });
            if (boss != null && boss.Visible) boss.IncomingThrow(player.Lane, total / throwSpeed);
            Thrown?.Invoke();
            Haptics.Pulse(Haptics.Light, 90);
            var trail = t.GetComponentInChildren<TrailRenderer>();
            if (trail != null) trail.Clear();
            Changed?.Invoke();
        }

        private void UpdateShots(float dt)
        {
            if (dt <= 0f) return;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Shot s = shots[i];
                float vz = player.RunSpeed + throwSpeed;
                s.z += vz * dt;
                s.travelled += throwSpeed * dt;   // koşucuya göre yol
                float k = Mathf.Clamp01(s.travelled / s.total);
                float targetY = (boss != null && boss.Visible ? boss.Position.y : player.GroundHeight) + aimHeight;
                float y = Mathf.Lerp(s.y0, s.resolved ? player.GroundHeight : targetY, k) + arcHeight * 4f * k * (1f - k);
                s.t.position = new Vector3(s.x, y, s.z);
                s.t.Rotate(new Vector3(720f, 90f, 0f) * dt, Space.Self);
                bool reached = boss != null && boss.Visible && boss.IsChasing && !s.resolved && s.z >= boss.Position.z - 0.6f;
                if (reached)
                {
                    if (boss.TryHit(s.x)) { Smash(s.t.position, s.kind, 1.4f); Finish(i, s); continue; }
                    s.resolved = true;                   // ıskaladı: yanından geçip yere düşer
                    s.y0 = y;
                    s.travelled = 0f;
                    s.total = 12f;
                }
                if (k >= 1f && (s.resolved || boss == null || !boss.Visible || !boss.IsChasing))
                {
                    Smash(new Vector3(s.x, player.GroundHeight + 0.2f, s.z), s.kind, 0.8f);
                    Finish(i, s);
                    continue;
                }
                shots[i] = s;
            }
        }

        private void Smash(Vector3 at, int kind, float strength)
        {
            ParticleSystem fx = kinds[kind].smashFx != null ? kinds[kind].smashFx : smashFx;
            if (fx != null)
            {
                fx.transform.position = at;
                fx.Emit(Mathf.RoundToInt(34 * strength));
            }
            if (smashFx != null && fx != smashFx)   // parıltı
            {
                smashFx.transform.position = at;
                smashFx.Emit(Mathf.RoundToInt(12 * strength));
            }
        }

        private void Finish(int i, Shot s)
        {
            Release(s.kind, s.t);
            shots.RemoveAt(i);
        }

        // ------------------------------------------------------------------

        private Transform Take(int kind)
        {
            if (!pool.TryGetValue(kind, out var stack)) pool[kind] = stack = new Stack<Transform>();
            Transform t = stack.Count > 0 ? stack.Pop() : Instantiate(kinds[kind].template, transform).transform;
            t.rotation = kinds[kind].template.transform.rotation;
            t.gameObject.SetActive(true);
            return t;
        }

        private void Release(int kind, Transform t)
        {
            t.gameObject.SetActive(false);
            pool[kind].Push(t);
        }
    }
}
