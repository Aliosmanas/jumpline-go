using System;
using System.Collections.Generic;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level.Chunks
{
    /// <summary>
    /// Koşucunun önüne chunk dizer, gerideki engelleri havuza geri alır (dünya başına bir tane: sokak / çatı).
    /// Sıra <see cref="DifficultyDirector"/>'dan gelir; chunk'ın içi elle tasarlanmış ve <see cref="ChunkRules"/> ile
    /// doğrulanmıştır. Engel konumu = chunk başı + yerleşimin ön kenarı + tanımın ön kenar–pivot mesafesi.
    /// Her chunk'la: para rotaları (CoinField), görsel vurgu (yolun dışında / üstünde) ve hareketli süsler.
    /// Bölüm içinde <see cref="portalDistance"/> m'de bir portal meydanı chunk'ı gelir (canavar kovalamasında yok sayılır).
    /// </summary>
    public class ChunkSpawner : MonoBehaviour
    {
        [Serializable]
        public struct AccentEntry
        {
            public VisualAccent accent;
            public GameObject prefab;
            [Tooltip("Chunk ortasına göre yerel konum (x yolun dışında, |x| ≥ 4.6; kemer x = 0 ve yüksekte).")]
            public Vector3 offset;
            [Tooltip("Yolun iki yanına aynalı konur.")]
            public bool mirrored;
        }

        [SerializeField] private PlayerController player;
        [SerializeField] private WorldSwitcher switcher;
        [SerializeField] private DifficultyDirector director;
        [SerializeField] private CoinField coins;
        [Tooltip("true: çatı dünyası (çatı chunk'ları; zipline boşluğu boş kalır).")]
        [SerializeField] private bool rooftop;

        [Header("Yerleşim")]
        [SerializeField, Min(5f)] private float startDistance = 24f;
        [SerializeField, Min(20f)] private float spawnAhead = 130f;
        [SerializeField, Min(2f)] private float despawnBehind = 15f;

        [Header("Portal (bölüm sonu)")]
        [SerializeField] private Portal coursePortal;
        [Tooltip("Bölüm içinde portal meydanının geleceği mesafe (m); LevelDirector bölüme göre ayarlar.")]
        [SerializeField, Min(100f)] private float portalDistance = 1400f;
        [SerializeField] private string portalChunkId = "portal_plaza";
        [Tooltip("Koşucunun girdiği ilk portaldan sonra yenisi bu kadar ileride (m).")]
        [SerializeField, Min(100f)] private float portalRepeat = 900f;
        [Tooltip("Çatıda engel konmayan modül içi aralık (zipline boşluğu + tutunma payı).")]
        [SerializeField] private Vector2 rooftopNoSpawn = new Vector2(28f, 70f);

        [Header("Okunabilirlik işaretleri (renk kodu)")]
        [SerializeField] private Material hintJump;
        [SerializeField] private Material hintSlide;
        [SerializeField] private Material hintBlock;
        [SerializeField] private Material hintClimb;
        [SerializeField] private Material hintMoving;
        [SerializeField, Min(0.3f)] private float hintLength = 2.2f;
        [SerializeField, Min(0f)] private float hintGap = 0.6f;

        [Header("Görsel canlılık")]
        [SerializeField] private AccentEntry[] accents = Array.Empty<AccentEntry>();
        [Tooltip("Hareketli süs şablonları (kuş, bayrak, fener…; AmbientMotion taşır). Şablonun kendi x'i yolun ortasından yan " +
                 "mesafe (|x| ≥ 4.6: oyun alanına taşmaz), y'si yükseklik; iki yana sırayla konur.")]
        [SerializeField] private GameObject[] ambientDecor = Array.Empty<GameObject>();
        [Tooltip("Chunk kendi vurgusunu taşımıyorsa en geç bu kadar metrede bir sıradaki vurgu konur (her 100 m'de vurgu).")]
        [SerializeField, Min(30f)] private float accentEvery = 90f;
        [Tooltip("Yedek vurgular (sırayla; araç tavanına değmeyenler).")]
        [SerializeField] private VisualAccent[] fallbackAccents = { VisualAccent.Lanterns, VisualAccent.Column, VisualAccent.ShopFront, VisualAccent.BridgePier };

        private struct Live
        {
            public Obstacle obstacle;
            public float maxZ;
        }

        private readonly List<Live> active = new List<Live>();
        private readonly List<(Transform t, float z)> decor = new List<(Transform, float)>();
        private ObjectPool<Obstacle> pool;
        private ObjectPool<Transform> decorPool;
        private float nextZ = float.NaN;
        private float levelStartZ = float.NaN;
        private float nextPortalAt;
        private float lastAccentZ = float.NegativeInfinity;
        private int fallbackIndex;

        /// <summary>Canavar kovalamasında portal meydanı konmaz (çatıya canavarla birlikte gidilir).</summary>
        public bool IgnoreCoursePortal { get; set; }

        public bool IsRooftop => rooftop;

        /// <summary>Canavarın fırlattığı engel (aşılabilir sandık).</summary>
        public Obstacle ThrownTemplate => director != null && director.Library != null ? director.Library.Get("crates")?.prefab : null;

        private float OriginX => switcher == null ? 0f : rooftop ? switcher.RooftopOriginX : switcher.StreetOriginX;
        private float Ground => switcher == null ? 0f : rooftop ? switcher.RooftopGround : switcher.StreetGround;
        private ChunkWorld World => rooftop ? ChunkWorld.Rooftop : ChunkWorld.Street;

        /// <summary>LevelDirector: bölüm (zorluk eşikleri) ve portal meydanı mesafesi.</summary>
        public void SetLevel(int level, float portalAt)
        {
            if (director != null) director.SetLevel(level);
            portalDistance = Mathf.Max(100f, portalAt);
        }

        private void Awake()
        {
            pool = new ObjectPool<Obstacle>(transform);
            decorPool = new ObjectPool<Transform>(transform);
        }

        private void OnEnable()
        {
            // dünyaya (yeniden) girildi: eski engelleri topla, ilk chunk koşucunun biraz önünde
            ReleaseAll();
            nextZ = float.NaN;
            if (director != null) director.ResetSequence();
            if (coins != null) coins.Clear();
        }

        private void Update()
        {
            if (player == null || director == null) return;
            float pz = player.transform.position.z;
            if (float.IsNaN(levelStartZ)) { levelStartZ = pz; nextPortalAt = portalDistance; }
            if (float.IsNaN(nextZ)) nextZ = pz + startDistance;
            int guard = 0;
            while (nextZ < pz + spawnAhead && guard++ < 8)
            {
                float from = nextZ;
                nextZ += SpawnNext(nextZ);
                if (nextZ <= from) nextZ = from + 30f;
            }
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (active[i].obstacle == null) { active.RemoveAt(i); continue; }
                if (active[i].obstacle.MaxZ >= pz - despawnBehind) continue;
                pool.Release(active[i].obstacle);
                active.RemoveAt(i);
            }
            for (int i = decor.Count - 1; i >= 0; i--)
            {
                if (decor[i].z >= pz - despawnBehind) continue;
                decorPool.Release(decor[i].t);
                decor.RemoveAt(i);
            }
        }

        /// <summary>z'den başlayarak sıradaki chunk'ı dizer; kapladığı uzunluğu döndürür.</summary>
        private float SpawnNext(float z)
        {
            float dist = z - levelStartZ;
            LevelChunk chunk = null;
            if (!rooftop && !IgnoreCoursePortal && coursePortal != null && dist >= nextPortalAt)
            {
                chunk = director.Special(portalChunkId);
                nextPortalAt = dist + portalRepeat;
            }
            if (chunk == null) chunk = director.Next(dist, World);
            if (chunk == null) return 30f;
            if (rooftop && switcher != null)
            {   // zipline boşluğu (çatı arası) boş kalır: chunk boşluğa taşıyorsa boşluğun bitiminden başlar
                float rel = Mathf.Repeat(z - switcher.RooftopRootZ, switcher.RooftopModuleLength);
                if (rel + chunk.length > rooftopNoSpawn.x && rel < rooftopNoSpawn.y) return rooftopNoSpawn.y - rel + 0.5f;
            }
            Spawn(chunk, z);
            return chunk.length;
        }

        private void Spawn(LevelChunk chunk, float z0)
        {
            var lib = director.Library;
            foreach (var p in chunk.obstacles)
            {
                if (p.id == "portal")
                {
                    if (coursePortal != null && !IgnoreCoursePortal) coursePortal.PlaceAt(z0 + p.z, p.lane);
                    continue;
                }
                var def = lib != null ? lib.Get(p.id) : null;
                if (def == null || def.prefab == null) continue;
                float x = def.laneSpan >= 3 ? OriginX : OriginX + p.lane * player.LaneWidth;
                Place(def, x, z0 + p.z + def.frontToPivot);
            }
            if (coins != null)
                foreach (var r in chunk.coins)
                    coins.PlaceRoute(OriginX + r.lane * player.LaneWidth, OriginX + (r.shape == CoinShape.SCurve ? r.toLane : r.lane) * player.LaneWidth,
                                     z0 + r.z, r.length, Ground);
            SpawnAccent(chunk, z0);
            SpawnAmbient(chunk, z0);
        }

        private void Place(ObstacleDefinition def, float x, float z)
        {
            Obstacle o = pool.Take(def.Pick());
            o.transform.position = new Vector3(x, Ground, z);
            o.enabled = true;   // güçle parçalanıp kapatılmış olabilir
            o.gameObject.SetActive(true);
            if (o.TryGetComponent(out MovingObstacle mover)) { mover.Configure(def.moveSpeed, def.activateDistance); mover.Arm(player); }
            ObstacleHint.ApplyAction(o, HintFor(def.kind), HintMaterial(def.kind), hintLength, hintGap);
            active.Add(new Live { obstacle = o, maxZ = o.MaxZ });
        }

        private static ObstacleHint.Action HintFor(ObstacleKind k) => k switch
        {
            ObstacleKind.Jump => ObstacleHint.Action.Jump,
            ObstacleKind.Slide => ObstacleHint.Action.Slide,
            ObstacleKind.Dodge or ObstacleKind.FullLane => ObstacleHint.Action.Block,
            ObstacleKind.Climb => ObstacleHint.Action.Climb,
            ObstacleKind.MovingObstacle => ObstacleHint.Action.Moving,
            _ => ObstacleHint.Action.None,
        };

        private Material HintMaterial(ObstacleKind k) => k switch
        {
            ObstacleKind.Jump => hintJump,
            ObstacleKind.Slide => hintSlide,
            ObstacleKind.Dodge or ObstacleKind.FullLane => hintBlock,
            ObstacleKind.Climb => hintClimb,
            ObstacleKind.MovingObstacle => hintMoving,
            _ => null,
        };

        private void SpawnAccent(LevelChunk chunk, float z0)
        {
            VisualAccent want = chunk.accent;
            if (want == VisualAccent.None && fallbackAccents.Length > 0 && z0 - lastAccentZ >= accentEvery)
                want = fallbackAccents[fallbackIndex++ % fallbackAccents.Length];
            if (want == VisualAccent.None) return;
            lastAccentZ = z0;
            foreach (var a in accents)
            {
                if (a.accent != want || a.prefab == null) continue;
                float zc = z0 + chunk.length * 0.5f;
                AddDecor(a.prefab, new Vector3(OriginX + a.offset.x, Ground + a.offset.y, zc + a.offset.z), false);
                if (a.mirrored) AddDecor(a.prefab, new Vector3(OriginX - a.offset.x, Ground + a.offset.y, zc + a.offset.z), true);
            }
        }

        private void SpawnAmbient(LevelChunk chunk, float z0)
        {
            if (ambientDecor.Length == 0) return;
            for (int i = 0; i < chunk.movingDecor; i++)
            {
                var prefab = ambientDecor[UnityEngine.Random.Range(0, ambientDecor.Length)];
                if (prefab == null) continue;
                float side = i % 2 == 0 ? 1f : -1f;
                Vector3 local = prefab.transform.position;
                float x = OriginX + side * Mathf.Max(4.6f, Mathf.Abs(local.x));
                float z = z0 + (i + UnityEngine.Random.Range(0.25f, 0.75f)) * chunk.length / Mathf.Max(1, chunk.movingDecor);
                AddDecor(prefab, new Vector3(x, Ground + local.y, z), side < 0f);
            }
        }

        private void AddDecor(GameObject prefab, Vector3 pos, bool mirror)
        {
            Transform t = decorPool.Take(prefab.transform);
            t.position = pos;
            t.rotation = prefab.transform.rotation * (mirror ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);
            t.gameObject.SetActive(true);
            decor.Add((t, pos.z));
        }

        /// <summary>Elmasla devam: [z0, z1] aralığındaki engelleri kaldır (koşucu güvenle yeniden başlasın).</summary>
        public void ClearRange(float z0, float z1)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var o = active[i].obstacle;
                if (o == null) { active.RemoveAt(i); continue; }
                if (o.MaxZ < z0 || o.MinZ > z1) continue;
                pool.Release(o);
                active.RemoveAt(i);
            }
        }

        private void ReleaseAll()
        {
            if (pool != null) foreach (var l in active) pool.Release(l.obstacle);
            active.Clear();
            if (decorPool != null) foreach (var d in decor) decorPool.Release(d.t);
            decor.Clear();
        }

        /// <summary>Testler / araçlar: tanımdaki engeli doğrudan koy.</summary>
        public Obstacle SpawnForTest(string id, int lane, float frontZ)
        {
            var def = director != null && director.Library != null ? director.Library.Get(id) : null;
            if (def == null || player == null) return null;
            Place(def, OriginX + lane * player.LaneWidth, frontZ + def.frontToPivot);
            return active[active.Count - 1].obstacle;
        }
    }
}
