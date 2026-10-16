using System.Collections.Generic;
using Jumpline.Player;
using Jumpline.Save;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Toplanabilir paralar (Subway Surfers mantığı). Para YALNIZCA koşucunun ulaşabileceği yere konur:
    /// her dizi şeritte 25 cm adımlarla "yürünür" — rampadan çıkılabilen yüzey yol sayılır (araç tavanı da), zıplanabilen
    /// engelin (≤ jumpable) üstünden paralar YAY çizer, kayılacak bariyerin altında alçak durur; zıplanamayan engelde
    /// (duvar, yerden çıkılmayan araç) dizi engelden önce biter. Dizi yerden başlar (ulaşılamaz tavanda başlamaz).
    /// Tek bileşen tüm paraları döndürür ve toplar. Toplanan paralar koşu bitince (sahne kapanınca) altına eklenir.
    /// </summary>
    public class CoinField : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [Tooltip("Kapalı şablon (Coin.fbx).")]
        [SerializeField] private GameObject coinTemplate;
        [SerializeField] private ParticleSystem pickupFx;
        [Tooltip("Mıknatıs / 2x altın gücü (yoksa etkisiz).")]
        [SerializeField] private PowerUps powerUps;
        [Tooltip("Mıknatıs: önde bu kadar (m) ve yanlarda bu kadar (m) içindeki paralar çekilir.")]
        [SerializeField] private Vector2 magnetReach = new Vector2(14f, 5.5f);
        [SerializeField, Min(1f)] private float magnetSpeed = 30f;
        [Tooltip("Hızlı Yarış'ta her para bu kadar sayılır (para kasma modu).")]
        [SerializeField, Min(1)] private int quickRaceMultiplier = 2;

        [Header("Dizi")]
        [Tooltip("Her 100 m'ye düşen dizi sayısı.")]
        [SerializeField] private Vector2 trailsPer100m = new Vector2(2f, 4f);
        [SerializeField] private Vector2Int coinsPerTrail = new Vector2Int(6, 12);
        [SerializeField, Min(0.5f)] private float spacing = 2.2f;
        [Tooltip("Yürünen yüzeyden para yüksekliği.")]
        [SerializeField, Min(0.1f)] private float hoverHeight = 1.0f;
        [Tooltip("Bariyer/kemer altındaki para yüksekliği (kayarken alınır).")]
        [SerializeField, Min(0.1f)] private float lowHeight = 0.5f;

        [Header("Ulaşılabilirlik (PlayerController ile uyumlu)")]
        [Tooltip("Yürürken çıkılabilen basamak (rampa adımı).")]
        [SerializeField, Min(0.05f)] private float stepUp = 0.3f;
        [Tooltip("Bu yüksekliğe kadar engelin üstünden zıplanır (zıplama tepesi 1.9).")]
        [SerializeField, Min(0.2f)] private float jumpable = 1.45f;
        [Tooltip("Yayın tepe yüksekliği (yürünen yüzeyden).")]
        [SerializeField, Min(0.5f)] private float arcPeak = 2.3f;
        [Tooltip("Yay, engelin bu kadar önünde başlar / arkasında biter.")]
        [SerializeField, Min(0.5f)] private float arcLead = 3.2f;
        [SerializeField, Min(0.05f)] private float probeStep = 0.25f;

        [Header("Görsel / toplama")]
        [SerializeField] private float spinSpeed = 220f;
        [SerializeField, Min(0.1f)] private float pickupRadius = 0.85f;
        [SerializeField, Min(1f)] private float despawnBehind = 12f;

        private readonly List<Transform> active = new List<Transform>();
        private readonly Stack<Transform> pool = new Stack<Transform>();
        private readonly List<float> zs = new List<float>();
        private readonly List<float> ys = new List<float>();
        private int bankedCount;

        public int RunCoins { get; private set; }
        /// <summary>Para toplandı (koşudaki toplam).</summary>
        public event System.Action<int> Collected;

        /// <summary>[z0, z1) aralığına ulaşılabilir para dizileri koyar (engeller yerleştikten sonra çağrılır).</summary>
        public void Populate(float z0, float z1, float originX, float ground, float laneWidth)
        {
            if (coinTemplate == null || z1 <= z0) return;
            int n = Mathf.RoundToInt((z1 - z0) / 100f * Random.Range(trailsPer100m.x, trailsPer100m.y));
            for (int i = 0; i < n; i++)
            {
                int count = Random.Range(coinsPerTrail.x, coinsPerTrail.y + 1);
                float start = Random.Range(z0, Mathf.Max(z0, z1 - count * spacing));
                PlanTrail(originX + Random.Range(-1, 2) * laneWidth, start, count, ground);
            }
        }

        /// <summary>
        /// Chunk para rotası (yön gösterir): aynı şeritte düz dizi — önündeki zıplanır engelin üstünden yay, bariyerin
        /// altında alçak hat (PlanTrail); farklı şeritte yumuşak S (şerit değiştirme rotası, yüzeyi izler).
        /// </summary>
        public void PlaceRoute(float xFrom, float xTo, float zStart, float length, float ground)
        {
            if (coinTemplate == null || length <= 0f) return;
            int count = Mathf.Max(2, Mathf.RoundToInt(length / spacing) + 1);
            if (Mathf.Abs(xTo - xFrom) < 0.01f) { PlanTrail(xFrom, zStart, count, ground); return; }
            for (int k = 0; k < count; k++)
            {
                float z = zStart + k * spacing;
                float t = Mathf.SmoothStep(0f, 1f, (float)k / (count - 1));
                float x = Mathf.Lerp(xFrom, xTo, t);
                float s = ObstacleField.SurfaceAt(x, z, ground);
                if (s > ground + stepUp && ObstacleField.SolidAt(x, z) != null) break;   // S rotası engelden önce biter
                bool low = ObstacleField.HitsCeiling(x, z, s, s + 1.7f);
                Spawn(new Vector3(x, s + (low ? lowHeight : hoverHeight), z));
            }
        }

        /// <summary>Şeritte yürüyerek dizinin para yüksekliklerini hesaplar ve koyar.</summary>
        private void PlanTrail(float x, float start, int count, float ground)
        {
            zs.Clear();
            ys.Clear();
            float cur = ObstacleField.SurfaceAt(x, start, ground);
            if (cur > ground + stepUp || cur < ground - 0.5f) return;      // dizi yerden başlar
            var spans = new List<Vector2>();                                   // yay çizilecek engel aralıkları (z)
            float z = start;
            for (int k = 0; k < count; k++)
            {
                float target = start + k * spacing;
                bool stop = false;
                while (z < target - 1e-3f)                                       // iki para arası ince yürüyüş
                {
                    float nz = Mathf.Min(z + probeStep, target);
                    float s = ObstacleField.SurfaceAt(x, nz, ground);
                    if (s < ground - 0.5f) { stop = true; break; }               // boşluk (çatı arası)
                    if (s <= cur + stepUp) { cur = s; z = nz; continue; }        // düz, rampa, iniş
                    // engel: sonunu bul, yüksekliğine bak
                    float a = nz, top = s, b = nz;
                    while (b < start + count * spacing + 6f)
                    {
                        float sb = ObstacleField.SurfaceAt(x, b + probeStep, ground);
                        top = Mathf.Max(top, sb);
                        b += probeStep;
                        if (sb <= cur + stepUp) break;
                    }
                    if (top - cur > jumpable) { stop = true; break; }            // aşılamaz: dizi burada biter
                    spans.Add(new Vector2(a, b));
                    z = b;                                                       // engelin üstünden atlandı, öbür yanda devam
                }
                if (stop) break;
                if (ObstacleField.SurfaceAt(x, target, ground) > cur + stepUp) break;
                bool low = ObstacleField.HitsCeiling(x, target, cur, cur + 1.7f);
                zs.Add(target);
                ys.Add(cur + (low ? lowHeight : hoverHeight));
            }
            // yaylar: engelin önünden arkasına bombeli geçiş (yürünen yüzeyden arcPeak yükseğe)
            foreach (Vector2 sp in spans)
            {
                float za = sp.x - arcLead, zb = sp.y + arcLead;
                for (int i = 0; i < zs.Count; i++)
                {
                    if (zs[i] < za || zs[i] > zb) continue;
                    float t = Mathf.InverseLerp(za, zb, zs[i]);
                    float baseY = ys[i] - hoverHeight;
                    ys[i] = Mathf.Max(ys[i], baseY + hoverHeight + (arcPeak - hoverHeight) * Mathf.Sin(t * Mathf.PI));
                }
            }
            for (int i = 0; i < zs.Count; i++) Spawn(new Vector3(x, ys[i], zs[i]));
        }

        /// <summary>Dünya değişince (sokak ↔ çatı) eski paraları topla.</summary>
        public void Clear()
        {
            for (int i = active.Count - 1; i >= 0; i--) Release(i);
        }

        private void Update()
        {
            if (player == null) return;
            Vector3 p = player.transform.position;
            float head = player.IsSliding ? 1.0f : 1.7f;
            Quaternion spin = Quaternion.Euler(0f, spinSpeed * Time.deltaTime, 0f);
            bool magnet = powerUps != null && powerUps.IsActive(PowerUpType.Magnet) && !player.IsDead;
            Vector3 chest = p + Vector3.up * 1.1f;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Transform c = active[i];
                Vector3 cp = c.position;
                if (cp.z < p.z - despawnBehind) { Release(i); continue; }
                c.rotation = spin * c.rotation;
                if (player.IsDead) continue;
                bool pulled = magnet && cp.z - p.z < magnetReach.x && cp.z > p.z - 2f && Mathf.Abs(cp.x - p.x) < magnetReach.y;
                if (pulled)
                {
                    cp = Vector3.MoveTowards(cp, chest, (magnetSpeed + player.RunSpeed) * Time.deltaTime);
                    c.position = cp;
                }
                if ((pulled && (cp - chest).sqrMagnitude < 0.7f) ||
                    (Mathf.Abs(cp.x - p.x) < pickupRadius && Mathf.Abs(cp.z - p.z) < pickupRadius &&
                    cp.y > p.y - 0.3f && cp.y < p.y + head + 0.3f))
                {
                    if (pickupFx != null)
                    {
                        pickupFx.transform.position = cp;
                        pickupFx.Emit(10);
                    }
                    Release(i);
                    int mode = Jumpline.Core.GameSession.Mode == Jumpline.Core.RunMode.QuickRace ? quickRaceMultiplier : 1;
                    RunCoins += (powerUps != null ? powerUps.CoinMultiplier : 1) * mode;
                    Collected?.Invoke(RunCoins);
                }
            }
        }

        private void Spawn(Vector3 pos)
        {
            Transform c = pool.Count > 0 ? pool.Pop() : Instantiate(coinTemplate, transform).transform;
            c.position = pos;
            c.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * coinTemplate.transform.rotation;
            c.gameObject.SetActive(true);
            active.Add(c);
        }

        private void Release(int i)
        {
            Transform c = active[i];
            active.RemoveAt(i);
            c.gameObject.SetActive(false);
            pool.Push(c);
        }

        // Koşu biter (yeniden başlat / ana menü / uygulama kapanır): elmasla devam edilebildiği için ölümde değil, çıkışta eklenir.
        private void OnDestroy() => Bank();
        private void OnApplicationPause(bool paused) { if (paused) Bank(); }

        /// <summary>Henüz eklenmemiş toplanan paraları kalıcı altına ekle.</summary>
        public void Bank()
        {
            int add = RunCoins - bankedCount;
            if (add <= 0) return;
            bankedCount = RunCoins;
            SaveManager.Gold += add;
            SaveManager.Flush();
        }
    }
}
