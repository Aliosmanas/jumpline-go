using System.Collections.Generic;
using Jumpline.Player;
using Jumpline.Save;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Toplanabilir paralar (Subway Surfers mantığı): engel sıralarının arasına para dizileri koyar — şeritte düz çizgi,
    /// alçak engelin üstünde yay (zıplarken alınır), bariyer altında alçak (kayarken alınır), araç tavanında (trende koşarken).
    /// Yerden aşılamayan engelin içine para konmaz. Tek bileşen tüm paraları döndürür ve toplar (para başına Update yok).
    /// Toplanan paralar koşu bitince (ölüm) kalıcı altına eklenir.
    /// </summary>
    public class CoinField : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [Tooltip("Kapalı şablon (Coin.fbx).")]
        [SerializeField] private GameObject coinTemplate;
        [SerializeField] private ParticleSystem pickupFx;

        [Header("Dizi")]
        [Tooltip("Her 100 m'ye düşen dizi sayısı.")]
        [SerializeField] private Vector2 trailsPer100m = new Vector2(2f, 4f);
        [SerializeField] private Vector2Int coinsPerTrail = new Vector2Int(6, 12);
        [SerializeField, Min(0.5f)] private float spacing = 2.2f;
        [Tooltip("Zeminden / üstüne basılan yüzeyden para yüksekliği.")]
        [SerializeField, Min(0.1f)] private float hoverHeight = 1.0f;
        [Tooltip("Bariyer/kemer altındaki para yüksekliği (kayarken alınır).")]
        [SerializeField, Min(0.1f)] private float lowHeight = 0.5f;
        [Tooltip("Bu kadar yükselen yüzey duvar sayılır, para konmaz.")]
        [SerializeField, Min(0.5f)] private float wallHeight = 3.2f;

        [Header("Görsel / toplama")]
        [SerializeField] private float spinSpeed = 220f;
        [SerializeField, Min(0.1f)] private float pickupRadius = 0.85f;
        [SerializeField, Min(1f)] private float despawnBehind = 12f;

        private readonly List<Transform> active = new List<Transform>();
        private readonly Stack<Transform> pool = new Stack<Transform>();
        private bool banked;

        public int RunCoins { get; private set; }
        /// <summary>Para toplandı (koşudaki toplam).</summary>
        public event System.Action<int> Collected;

        private void Start()
        {
            if (player != null) player.Died += Bank;
        }

        private void OnDisable() => Bank();

        /// <summary>[z0, z1) aralığına rastgele para dizileri koyar (engeller yerleştikten sonra çağrılır).</summary>
        public void Populate(float z0, float z1, float originX, float ground, float laneWidth)
        {
            if (coinTemplate == null || z1 <= z0) return;
            int n = Mathf.RoundToInt((z1 - z0) / 100f * Random.Range(trailsPer100m.x, trailsPer100m.y));
            for (int i = 0; i < n; i++)
            {
                int lane = Random.Range(-1, 2);
                int count = Random.Range(coinsPerTrail.x, coinsPerTrail.y + 1);
                float start = Random.Range(z0, Mathf.Max(z0, z1 - count * spacing));
                float x = originX + lane * laneWidth;
                for (int k = 0; k < count; k++)
                {
                    float z = start + k * spacing;
                    float surface = ObstacleField.SurfaceAt(x, z, ground);
                    if (surface - ground > wallHeight) continue;                         // duvarın / aracın gövdesinin içi
                    if (surface < ground - 0.5f) continue;                               // çatı boşluğu (zipline)
                    float y = surface + hoverHeight;
                    if (ObstacleField.HitsCeiling(x, z, surface, surface + 1.7f)) y = surface + lowHeight;   // kay
                    Spawn(new Vector3(x, y, z));
                }
            }
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
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Transform c = active[i];
                Vector3 cp = c.position;
                if (cp.z < p.z - despawnBehind) { Release(i); continue; }
                c.rotation = spin * c.rotation;
                if (player.IsDead) continue;
                if (Mathf.Abs(cp.x - p.x) < pickupRadius && Mathf.Abs(cp.z - p.z) < pickupRadius &&
                    cp.y > p.y - 0.3f && cp.y < p.y + head + 0.3f)
                {
                    if (pickupFx != null)
                    {
                        pickupFx.transform.position = cp;
                        pickupFx.Emit(10);
                    }
                    Release(i);
                    RunCoins++;
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

        /// <summary>Koşu bitti: toplanan paraları kalıcı altına ekle (bir kez).</summary>
        private void Bank()
        {
            if (banked || RunCoins == 0) return;
            banked = true;
            SaveManager.Gold += RunCoins;
            SaveManager.Flush();
        }
    }
}
