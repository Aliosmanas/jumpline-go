using System.Collections.Generic;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Yolda süzülen güç kutuları (mıknatıs, kalkan, 2x altın, hız). Belli aralıkla (m), koşucunun önüne engelsiz,
    /// düz zeminli, tavansız bir şeride konur; etkin olan güç yeniden çıkmaz. Dokununca PowerUps.Activate.
    /// Tek bileşen tüm dünyalarda çalışır (koşucunun o anki zemini ve şerit kökeniyle).
    /// </summary>
    public class PowerUpField : MonoBehaviour
    {
        [System.Serializable]
        public struct Kind
        {
            public PowerUpType type;
            [Tooltip("Kapalı şablon (ikon + parıltı).")] public GameObject template;
            public float weight;
        }

        [SerializeField] private PlayerController player;
        [SerializeField] private PowerUps powerUps;
        [SerializeField] private Kind[] kinds;
        [SerializeField] private Vector2 spacing = new Vector2(260f, 380f);
        [Tooltip("Koşunun başında ilk güç bu mesafeden önce çıkmaz.")]
        [SerializeField, Min(0f)] private float firstAfter = 140f;
        [SerializeField, Min(20f)] private float spawnAhead = 90f;
        [SerializeField, Min(0.3f)] private float hoverHeight = 1.15f;
        [SerializeField, Min(0.2f)] private float pickupRadius = 1.05f;
        [Tooltip("İkonun sağa-sola sallanma açısı (derece).")]
        [SerializeField] private float wobble = 9f;
        [SerializeField] private ParticleSystem pickupFx;

        private struct Item { public Transform t; public int kind; public float baseY; }
        private readonly List<Item> items = new List<Item>();
        private readonly Dictionary<int, Stack<Transform>> pool = new Dictionary<int, Stack<Transform>>();
        private float nextDistance;
        private Camera cam;

        private void Start() => nextDistance = firstAfter + Random.Range(0f, spacing.x * 0.3f);

        private void Update()
        {
            if (player == null || powerUps == null || kinds == null || kinds.Length == 0) return;
            if (player.Distance >= nextDistance)
                nextDistance = player.Distance + (TrySpawn() ? Random.Range(spacing.x, spacing.y) : 8f);
            UpdateItems();
        }

        private bool TrySpawn()
        {
            int kind = Pick();
            if (kind < 0) return false;
            float z = player.transform.position.z + spawnAhead;
            float g = player.GroundHeight;
            int first = Random.Range(-1, 2);
            for (int i = 0; i < 3; i++)
            {
                int lane = (first + 1 + i) % 3 - 1;
                float x = player.LaneOriginX + lane * player.LaneWidth;
                if (ObstacleField.AnySolid(x, 0.9f, z - 3.5f, z + 3.5f)) continue;
                if (Mathf.Abs(ObstacleField.SurfaceAt(x, z, g) - g) > 0.05f) continue;
                if (ObstacleField.HitsCeiling(x, z, g, g + 2.2f)) continue;
                Transform t = Take(kind);
                t.position = new Vector3(x, g + hoverHeight, z);
                items.Add(new Item { t = t, kind = kind, baseY = g + hoverHeight });
                return true;
            }
            return false;
        }

        /// <summary>Ağırlığa göre, etkin olmayan bir güç (hepsi etkinse -1).</summary>
        private int Pick()
        {
            float sum = 0f;
            for (int i = 0; i < kinds.Length; i++) if (!powerUps.IsActive(kinds[i].type)) sum += Mathf.Max(0f, kinds[i].weight);
            if (sum <= 0f) return -1;
            float r = Random.value * sum;
            for (int i = 0; i < kinds.Length; i++)
            {
                if (powerUps.IsActive(kinds[i].type)) continue;
                r -= Mathf.Max(0f, kinds[i].weight);
                if (r <= 0f) return i;
            }
            return -1;
        }

        private void UpdateItems()
        {
            Vector3 p = player.transform.position;
            float head = player.IsSliding ? 1.0f : 1.7f;
            float bob = Mathf.Sin(Time.time * 2.6f) * 0.14f;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                Item it = items[i];
                Vector3 cp = it.t.position;
                bool otherWorld = Mathf.Abs(it.baseY - (player.GroundHeight + hoverHeight)) > 1f || Mathf.Abs(cp.x - player.LaneOriginX) > 6f;
                if (cp.z < p.z - 10f || otherWorld) { Release(it); items.RemoveAt(i); continue; }
                cp.y = it.baseY + bob;
                it.t.position = cp;
                if (cam == null) cam = Camera.main;   // ikon kameraya bakar, hafifçe sallanır
                if (cam != null) it.t.rotation = Quaternion.LookRotation(cp - cam.transform.position) * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 3f + i) * wobble);
                if (player.IsDead) continue;
                if (Mathf.Abs(cp.x - p.x) < pickupRadius && Mathf.Abs(cp.z - p.z) < pickupRadius && cp.y > p.y - 0.4f && cp.y < p.y + head + 0.5f)
                {
                    if (pickupFx != null) { pickupFx.transform.position = cp; pickupFx.Emit(30); }
                    powerUps.Activate(kinds[it.kind].type);
                    Jumpline.Core.Haptics.Pulse(Jumpline.Core.Haptics.Light, 160);
                    Release(it);
                    items.RemoveAt(i);
                }
            }
        }

        private Transform Take(int kind)
        {
            if (!pool.TryGetValue(kind, out var st)) pool[kind] = st = new Stack<Transform>();
            Transform t = st.Count > 0 ? st.Pop() : Instantiate(kinds[kind].template, transform).transform;
            t.gameObject.SetActive(true);
            return t;
        }

        private void Release(Item it)
        {
            it.t.gameObject.SetActive(false);
            pool[it.kind].Push(it.t);
        }
    }
}
