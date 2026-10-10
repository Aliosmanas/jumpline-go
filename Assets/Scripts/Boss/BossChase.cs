using System.Collections.Generic;
using Jumpline.CameraSystem;
using Jumpline.Core;
using Jumpline.Level;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Boss
{
    /// <summary>
    /// Ters kovalama (bölüm 1: Tepegöz). Canavar koşucunun önünde kaçar; arasındaki mesafe (gap) yavaşça açılır.
    ///  • Fırlatılan nesne isabet edince (<see cref="TakeHit"/>) bir kalp düşer ve canavar sendeler → mesafe kapanır.
    ///  • Koşucu sendeleyince mesafe açılır. Mesafe kaçma sınırına varırsa canavar kaçar → bölüm biter (<see cref="Escaped"/>).
    ///  • Fazlar (kalan kalbe göre): 4 sokak (arkasına engel fırlatır) · 3 portaldan çatıya kaçar · 2 öfke (uyararak kaçınır,
    ///    daha hızlı) · 1 son kalp (en fazla <see cref="finalMaxDodges"/> kaçınma). Son kalp düşünce devrilir → <see cref="Defeated"/>.
    ///  • Şerit değiştirmeden önce hep eğilerek uyarır (oyuncu nişan alabilsin); kaçınma da uyarılıdır, çok yakından atılan ıskalanmaz.
    /// Canavar fizik kullanmaz: koşucunun dünyasında (sokak/çatı) koşucuya göre konumlanır, engellerin üstünden sıçrar.
    /// Yalnız Kampanya modunda çalışır (Hızlı Yarış canavarsız).
    /// </summary>
    public class BossChase : MonoBehaviour
    {
        public enum Phase { Street, Rooftop, Fury, LastHeart }
        private enum State { Intro, Chasing, Defeated, Escaped, Done }

        [SerializeField] private PlayerController player;
        [SerializeField] private WorldSwitcher switcher;
        [SerializeField] private CameraFollow cameraFollow;
        [Tooltip("Sokak engel üreticisi: kovalamada parkurun kendi portal satırları kapatılır (çatıya canavarla gidilir).")]
        [SerializeField] private ObstacleSpawner streetSpawner;
        [Tooltip("Sokak → çatı portalı (canavar 3. kalpte buradan kaçar).")]
        [SerializeField] private Portal upPortal;
        [Tooltip("Çatı → sokak portalı (canavar çatıda bunu görünce aşağı iner).")]
        [SerializeField] private Portal downPortal;

        [Header("Görsel")]
        [Tooltip("Canavarın görünen gövdesi (kök: ayak hizası, +Z'ye bakar).")]
        [SerializeField] private Transform visual;
        [SerializeField] private Animator animator;
        [Tooltip("Fırlatılan engelin çıktığı el (yoksa omuz hizası).")]
        [SerializeField] private Transform throwHand;
        [Tooltip("Koşu animasyonunun 1x oynadığı hız (m/sn); koşucu hızlandıkça canavarın adımları da hızlanır.")]
        [SerializeField, Min(1f)] private float animReferenceSpeed = 11f;
        [Tooltip("İsabette kırmızı parlayan gövde parçaları.")]
        [SerializeField] private Renderer[] flashRenderers;
        [SerializeField] private Color hitFlash = new Color(1f, 0.25f, 0.2f, 1f);
        [SerializeField] private ParticleSystem hitFx;
        [SerializeField] private ParticleSystem portalFx;
        [Tooltip("Fırlatılan engel yere düşünce toz.")]
        [SerializeField] private ParticleSystem landFx;
        [Tooltip("İsabette kısa duraklama (gerçek sn) — vuruş hissi.")]
        [SerializeField, Min(0f)] private float hitStop = 0.07f;

        [Header("Kalpler")]
        [SerializeField, Range(1, 8)] private int maxHearts = 4;

        [Header("Mesafe (m)")]
        [Tooltip("Başlangıçta canavar yakından kükrer, sonra bu mesafeye kaçar.")]
        [SerializeField, Min(3f)] private float introGap = 11f;
        [SerializeField, Min(5f)] private float startGap = 22f;
        [Tooltip("Canavar bundan yakına gelmez (sendelese de).")]
        [SerializeField, Min(3f)] private float minGap = 9f;
        [Tooltip("Mesafe buna varınca canavar kaçar (bölüm biter).")]
        [SerializeField, Min(10f)] private float escapeGap = 52f;
        [Tooltip("Uyarı (KAÇIYOR!) eşiği: mesafe aralığının bu oranı.")]
        [SerializeField, Range(0.3f, 0.95f)] private float warnAt = 0.7f;
        [Tooltip("Mesafenin açılma hızı (m/sn) fazlara göre: sokak, çatı, öfke, son kalp.")]
        [SerializeField] private float[] driftPerPhase = { 0.42f, 0.5f, 0.62f, 0.72f };
        [Tooltip("İsabette mesafe bu kadar kapanır.")]
        [SerializeField, Min(0f)] private float hitPull = 12f;
        [Tooltip("İsabet çekmesinin uygulanma hızı (m/sn) — canavar sendeleyerek yavaşlar.")]
        [SerializeField, Min(1f)] private float pullRate = 22f;
        [Tooltip("Hız gücündeyken mesafe saniyede bu kadar kapanır.")]
        [SerializeField, Min(0f)] private float speedBoostClose = 2.6f;
        [Tooltip("Koşucu yandan çarpıp sendeleyince mesafe bu kadar açılır.")]
        [SerializeField, Min(0f)] private float stumblePush = 6f;
        [Tooltip("Girişte canavarın başlangıç mesafesine kaçma hızı (m/sn).")]
        [SerializeField, Min(0.5f)] private float introRunOff = 5f;
        [SerializeField, Min(0f)] private float introRoarTime = 1.75f;

        [Header("Şerit değiştirme")]
        [SerializeField] private Vector2 laneChangeInterval = new Vector2(2.6f, 5.2f);
        [Tooltip("Şerit değiştirmeden önce eğilme (uyarı) süresi.")]
        [SerializeField, Min(0f)] private float laneTelegraph = 0.45f;
        [SerializeField, Min(1f)] private float laneSharpness = 9f;
        [SerializeField, Range(0f, 30f)] private float leanAngle = 14f;

        [Header("Kaçınma (atışa karşı)")]
        [Tooltip("Fazlara göre kaçınma olasılığı (sokak, çatı, öfke, son kalp).")]
        [SerializeField] private float[] dodgeChance = { 0f, 0f, 0.45f, 0.6f };
        [Tooltip("Kaçınma uyarısı (eğilme) süresi; atış bundan + şerit geçişinden kısa sürede varırsa kaçınamaz.")]
        [SerializeField, Min(0f)] private float dodgeTelegraph = 0.28f;
        [Tooltip("Şerit geçişinin yarısı (sn) — isabet bundan sonra kaçar.")]
        [SerializeField, Min(0f)] private float dodgeMoveTime = 0.22f;
        [Tooltip("Son kalpte en fazla kaç kez kaçınır (sonra mutlaka isabet).")]
        [SerializeField, Min(0)] private int finalMaxDodges = 2;

        [Header("Engel fırlatma (arkasına)")]
        [SerializeField] private Obstacle thrownTemplate;
        [SerializeField] private Transform landingMarker;
        [SerializeField] private Vector2 throwInterval = new Vector2(8f, 13f);
        [Tooltip("Fazlara göre fırlatma sıklığı çarpanı (büyük = daha sık).")]
        [SerializeField] private float[] throwRatePerPhase = { 1f, 0.8f, 1.25f, 1.4f };
        [SerializeField, Min(0.1f)] private float throwWindUp = 0.55f;
        [SerializeField, Min(0.1f)] private float throwFlight = 0.75f;
        [Tooltip("Engel canavarın bu kadar arkasına düşer.")]
        [SerializeField, Min(1f)] private float landBehind = 4f;
        [Tooltip("Engel koşucuya bundan yakına düşmez (tepki süresi).")]
        [SerializeField, Min(5f)] private float minLandAhead = 16f;
        [SerializeField, Range(0f, 1f)] private float aimAtPlayerLane = 0.6f;

        [Header("Çatı fazı")]
        [Tooltip("3. kalpte portal canavarın bu kadar önüne açılır.")]
        [SerializeField, Min(8f)] private float portalLead = 26f;
        [Tooltip("Koşucu portalı kaçırırsa mesafe cezası.")]
        [SerializeField, Min(0f)] private float missedPortalPenalty = 8f;

        [Header("Hareket")]
        [SerializeField, Min(1f)] private float hopGravity = 34f;
        [SerializeField, Min(0f)] private float hopClearance = 0.5f;

        [Header("Yenilgi")]
        [SerializeField, Min(0f)] private float defeatDelay = 2.4f;
        [SerializeField, Range(0.05f, 1f)] private float defeatSlowMo = 0.35f;

        // --- durum ---
        private State state;
        private int hearts;
        private float gap;
        private float pull;                 // uygulanacak isabet çekmesi
        private int lane;
        private int pendingLane;
        private float laneX;                // yumuşatılmış yerel şerit kayması
        private float telegraphUntil = -1f;
        private float nextLaneChange;
        private float y, vy;
        private bool onRoof;                // canavarın dünyası (koşucunun dünyasından farklıysa görünmez)
        private float stateTime;
        private float nextThrow;
        private float throwStart = -1f;
        private int throwLane;
        private float throwLandZ;
        private int dodgesInRow;
        private int finalDodges;
        private float flashUntil;
        private bool upPortalOpen;
        private float upPortalZ;
        private float defeatedAt;
        private bool introRunning;
        private readonly List<Obstacle> thrown = new List<Obstacle>();
        private readonly Stack<Obstacle> thrownPool = new Stack<Obstacle>();
        private Transform flyingThrow;
        private MaterialPropertyBlock block;

        /// <summary>Kalan kalp değişti (kalan, en fazla).</summary>
        public event System.Action<int, int> HeartsChanged;
        /// <summary>İsabet aldı.</summary>
        public event System.Action Hit;
        /// <summary>Atış kaçınıldı (ıskaladı).</summary>
        public event System.Action Dodged;
        /// <summary>Canavar kaçtı — bölüm kaybedildi (elmasla devam edilebilir).</summary>
        public event System.Action Escaped;
        /// <summary>Son kalp düştü, devrilme bitti — bölüm kazanıldı.</summary>
        public event System.Action Defeated;
        public event System.Action<Phase> PhaseChanged;
        /// <summary>Fırlattığı engel yere düştü.</summary>
        public event System.Action ObstacleLanded;

        public bool IsActive => isActiveAndEnabled;
        /// <summary>true: konum/yenilgi akışını BossFinale yönetir (köprüden düşüş sinematiği).</summary>
        public bool ExternalControl { get; set; }
        /// <summary>true: atışlar isabet etmez (köprüye geçiş sırasında son kalp korunur).</summary>
        public bool Invulnerable { get; set; }
        /// <summary>Son kalp düştü (devrilme başladı) — final sinematiği bunu dinler.</summary>
        public event System.Action Defeating;
        public Transform Visual => visual;
        public Animator BodyAnimator => animator;
        public bool IsChasing => state == State.Chasing || state == State.Intro;
        public bool IsEscaped => state == State.Escaped;
        public bool IsDefeated => state == State.Defeated || state == State.Done;
        public int Hearts => hearts;
        public int MaxHearts => maxHearts;

        /// <summary>LevelDirector: kalp sayısı ve kaçma hızı çarpanı (koşu başlamadan).</summary>
        public void SetDifficulty(int hearts, float driftScale)
        {
            maxHearts = Mathf.Clamp(hearts, 1, 8);
            for (int i = 0; i < driftPerPhase.Length; i++) driftPerPhase[i] *= driftScale;
        }
        public float Gap => gap;
        /// <summary>0 = en yakın, 1 = kaçtı.</summary>
        public float Gap01 => Mathf.InverseLerp(minGap, escapeGap, gap);
        public bool Warning => state == State.Chasing && Gap01 >= warnAt;
        /// <summary>Canavar koşucunun dünyasında görünüyor mu (portaldan geçerken değil).</summary>
        public bool Visible => visual != null && visual.gameObject.activeSelf;
        public Vector3 Position => transform.position;
        public int Lane => lane;
        public Phase CurrentPhase => PhaseFor(hearts);

        private Phase PhaseFor(int h)
        {
            int lost = maxHearts - h;
            if (h <= 1) return Phase.LastHeart;
            if (lost >= 2) return Phase.Fury;
            if (lost >= 1) return Phase.Rooftop;
            return Phase.Street;
        }

        private float PhaseValue(float[] values)
        {
            if (values == null || values.Length == 0) return 0f;
            return values[Mathf.Clamp((int)CurrentPhase, 0, values.Length - 1)];
        }

        private bool PlayerOnRoof => switcher != null && !switcher.InStreet;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (GameSession.Mode != RunMode.Campaign)   // Hızlı Yarış / günlük: canavarsız
            {
                gameObject.SetActive(false);
                return;
            }
            if (streetSpawner != null) streetSpawner.IgnoreCoursePortal = true;
        }

        private void OnEnable()
        {
            if (player != null) player.Stumbled += OnStumbled;
            if (switcher != null) switcher.WorldChanged += OnWorldChanged;
        }

        private void OnDisable()
        {
            if (player != null) player.Stumbled -= OnStumbled;
            if (switcher != null) switcher.WorldChanged -= OnWorldChanged;
        }

        private void Start()
        {
            hearts = maxHearts;
            gap = introGap;
            state = State.Intro;
            stateTime = Time.time;
            lane = pendingLane = 0;
            nextLaneChange = Time.time + introRoarTime + Random.Range(laneChangeInterval.x, laneChangeInterval.y);
            nextThrow = Time.time + introRoarTime + Random.Range(throwInterval.x, throwInterval.y);
            if (landingMarker != null) landingMarker.gameObject.SetActive(false);
            y = player != null ? player.GroundHeight : 0f;
            Place();
            if (animator == null) Anim("Roar");   // canlandırıcıda varsayılan durum zaten kükreme
            if (cameraFollow != null) cameraFollow.Shake(0.6f);
            HeartsChanged?.Invoke(hearts, maxHearts);
            PhaseChanged?.Invoke(CurrentPhase);
        }

        private void Update()
        {
            if (player == null || ExternalControl) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            UpdateFlash();

            switch (state)
            {
                case State.Intro:
                    if (Time.time - stateTime > introRoarTime)
                    {
                        if (!introRunning) { introRunning = true; Anim("Run"); }   // kükreme bitti: dönüp kaçar
                        gap = Mathf.MoveTowards(gap, startGap, introRunOff * dt);
                        if (gap >= startGap - 0.01f) state = State.Chasing;
                    }
                    break;
                case State.Chasing:
                    if (!player.IsDead) UpdateGap(dt);
                    UpdateLanes();
                    UpdatePortals();
                    UpdateThrowing();
                    if (gap >= escapeGap) Escape();
                    break;
                case State.Defeated:
                    UpdateDefeat();
                    break;
                case State.Escaped:
                    gap += 14f * dt;   // koşarak uzaklaşır (panel açılınca dünya durur)
                    break;
            }
            UpdateThrownObstacles();
            Place();
        }

        // ------------------------------------------------------------------
        // Mesafe
        // ------------------------------------------------------------------

        private void UpdateGap(float dt)
        {
            gap += PhaseValue(driftPerPhase) * dt;
            if (player.PowerUpsRef != null && player.PowerUpsRef.IsActive(PowerUpType.SpeedBoost)) gap -= speedBoostClose * dt;
            if (pull > 0f)
            {
                float step = Mathf.Min(pull, pullRate * dt);
                pull -= step;
                gap -= step;
            }
            gap = Mathf.Max(gap, minGap);
        }

        private void OnStumbled()
        {
            if (state != State.Chasing) return;
            gap += stumblePush;
        }

        // ------------------------------------------------------------------
        // Şerit / kaçınma
        // ------------------------------------------------------------------

        private void UpdateLanes()
        {
            if (telegraphUntil > 0f)
            {
                if (Time.time < telegraphUntil) return;
                lane = pendingLane;
                telegraphUntil = -1f;
                nextLaneChange = Time.time + Random.Range(laneChangeInterval.x, laneChangeInterval.y);
                return;
            }
            int portalLane = PortalLaneAhead();
            if (portalLane != int.MinValue)
            {
                if (portalLane != lane) BeginLaneChange(portalLane, laneTelegraph);
                return;
            }
            if (Time.time < nextLaneChange || throwStart >= 0f) return;
            int next = lane == 0 ? (Random.value < 0.5f ? -1 : 1) : (Random.value < 0.6f ? 0 : -lane);
            BeginLaneChange(next, laneTelegraph);
        }

        private void BeginLaneChange(int target, float telegraph)
        {
            pendingLane = Mathf.Clamp(target, -1, 1);
            if (pendingLane == lane) return;
            telegraphUntil = Time.time + telegraph;
        }

        /// <summary>Koşucu atış yaptı: hangi şeride, kaç sn sonra varır. Fazına göre uyararak kaçınabilir.</summary>
        public void IncomingThrow(int targetLane, float eta)
        {
            if (state != State.Chasing || !Visible) return;
            bool coming = telegraphUntil > 0f ? pendingLane == targetLane : lane == targetLane;
            if (!coming) return;
            float chance = PhaseValue(dodgeChance);
            if (CurrentPhase == Phase.LastHeart && finalDodges >= finalMaxDodges) chance = 0f;
            if (dodgesInRow >= 1) chance *= 0.35f;                     // üst üste kaçınma nadir
            if (eta < dodgeTelegraph + dodgeMoveTime) chance = 0f;    // çok yakından atıldı: kaçamaz
            if (Random.value >= chance)
            {
                dodgesInRow = 0;
                return;
            }
            int away = targetLane == 0 ? (Random.value < 0.5f ? -1 : 1) : 0;
            BeginLaneChange(away, dodgeTelegraph);
            dodgesInRow++;
            if (CurrentPhase == Phase.LastHeart) finalDodges++;
        }

        /// <summary>Atış canavarın hizasına vardı: şerit tutuyorsa isabet.</summary>
        public bool TryHit(float x)
        {
            if (state != State.Chasing || !Visible || Invulnerable) return false;
            if (Mathf.Abs(x - transform.position.x) > player.LaneWidth * 0.55f)
            {
                Dodged?.Invoke();
                return false;
            }
            TakeHit();
            return true;
        }

        public void TakeHit()
        {
            if (state != State.Chasing || Invulnerable) return;
            hearts = Mathf.Max(0, hearts - 1);
            pull += hitPull;
            flashUntil = Time.time + 0.35f;
            if (hitFx != null) { hitFx.transform.position = transform.position + Vector3.up * 2.4f; hitFx.Emit(40); }
            if (cameraFollow != null) cameraFollow.Shake(0.5f);
            Haptics.Pulse(hearts > 0 ? Haptics.Medium : Haptics.Heavy, 220);
            if (hitStop > 0f && hearts > 0) StartCoroutine(HitStop());
            Anim(hearts > 0 ? "Hit" : "Defeat");
            Hit?.Invoke();
            HeartsChanged?.Invoke(hearts, maxHearts);
            if (hearts <= 0) { Defeat(); return; }
            Phase phase = CurrentPhase;
            PhaseChanged?.Invoke(phase);
            if (phase == Phase.Rooftop && !onRoof && !PlayerOnRoof) OpenUpPortal();
        }

        // ------------------------------------------------------------------
        // Portallar (çatı fazı)
        // ------------------------------------------------------------------

        private void OpenUpPortal()
        {
            if (upPortal == null) return;
            upPortalZ = transform.position.z + portalLead;
            if (streetSpawner != null) streetSpawner.ClearRange(upPortalZ - 8f, upPortalZ + 6f);   // portalın önü açık
            upPortal.PlaceAt(upPortalZ, lane);
            upPortalOpen = true;
        }

        /// <summary>Canavarın yöneldiği portalın şeridi (yoksa int.MinValue).</summary>
        private int PortalLaneAhead()
        {
            if (switcher != null && switcher.InArena) return int.MinValue;
            float z = transform.position.z;
            if (upPortalOpen && !onRoof && !PlayerOnRoof && upPortalZ - z < portalLead + 5f) return upPortal.Lane;
            if (onRoof && PlayerOnRoof && downPortal != null)
            {
                float dz = downPortal.transform.position.z - z;
                if (dz > 0f && dz < 45f) return downPortal.Lane;
            }
            return int.MinValue;
        }

        private void UpdatePortals()
        {
            if (switcher != null && switcher.InArena) return;   // köprüde portal yok
            float z = transform.position.z;
            float pz = player.transform.position.z;
            // canavar portaldan geçti → öbür dünyaya
            if (upPortalOpen && !onRoof && !PlayerOnRoof && z >= upPortalZ) SetBossWorld(true);
            if (onRoof && PlayerOnRoof && downPortal != null)
            {
                float dz = z - downPortal.transform.position.z;
                if (dz >= 0f && dz < 4f) SetBossWorld(false);
            }
            // koşucu yukarı portalı kaçırdı: ileride yeniden açılır, canavar uzaklaşır
            if (upPortalOpen && !PlayerOnRoof && onRoof && pz > upPortalZ + 3f)
            {
                gap += missedPortalPenalty;
                upPortalZ = pz + 45f;
                if (streetSpawner != null) streetSpawner.ClearRange(upPortalZ - 8f, upPortalZ + 6f);
                upPortal.PlaceAt(upPortalZ, Random.Range(-1, 2));
            }
        }

        private void SetBossWorld(bool roof)
        {
            if (portalFx != null) { portalFx.transform.position = transform.position + Vector3.up * 1.5f; portalFx.Emit(50); }
            onRoof = roof;
            ApplyVisibility();
        }

        private void OnWorldChanged(bool street)
        {
            if (!street) upPortalOpen = false;          // koşucu çatıya çıktı: yukarı portal işini bitirdi
            if (!street && !onRoof) onRoof = true;      // koşucu canavardan önce çıktıysa (parkur portalı) canavar da çıkar
            if (street && onRoof) onRoof = false;
            ClearThrown();
            y = player.GroundHeight;
            vy = 0f;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (visual == null) return;
            bool show = onRoof == PlayerOnRoof && state != State.Escaped;
            if (visual.gameObject.activeSelf != show) visual.gameObject.SetActive(show);
        }

        // ------------------------------------------------------------------
        // Engel fırlatma
        // ------------------------------------------------------------------

        private void UpdateThrowing()
        {
            if (thrownTemplate == null || !Visible) return;
            if (throwStart < 0f)
            {
                if (Time.time < nextThrow || telegraphUntil > 0f) return;
                if (!PickThrow()) { nextThrow = Time.time + 1.5f; return; }
                throwStart = Time.time;
                Anim("Throw");
                if (landingMarker != null)
                {
                    landingMarker.position = new Vector3(LaneWorldX(throwLane), player.GroundHeight + 0.03f, throwLandZ);
                    landingMarker.gameObject.SetActive(true);
                }
                return;
            }
            float t = Time.time - throwStart;
            if (t < throwWindUp) return;
            Vector3 from = throwHand != null ? throwHand.position : transform.position + Vector3.up * 3.2f;
            Vector3 to = new Vector3(LaneWorldX(throwLane), player.GroundHeight, throwLandZ);
            float k = Mathf.Clamp01((t - throwWindUp) / throwFlight);
            if (flyingThrow == null) flyingThrow = TakeThrown().transform;
            Vector3 p = Vector3.Lerp(from, to, k);
            p.y += 2.5f * 4f * k * (1f - k);
            flyingThrow.position = p;
            flyingThrow.rotation = Quaternion.Euler(k * 540f, 0f, k * 120f);
            if (k < 1f) return;
            // indi: gerçek engel olur (aşılabilir sandık)
            var obs = flyingThrow.GetComponent<Obstacle>();
            flyingThrow.rotation = Quaternion.identity;
            flyingThrow.position = to;
            obs.enabled = true;   // ObstacleField'a kaydolur
            thrown.Add(obs);
            flyingThrow = null;
            throwStart = -1f;
            if (landingMarker != null) landingMarker.gameObject.SetActive(false);
            if (cameraFollow != null) cameraFollow.Shake(0.25f);
            if (landFx != null) { landFx.transform.position = to + Vector3.up * 0.2f; landFx.Emit(26); }
            ObstacleLanded?.Invoke();
            nextThrow = Time.time + Random.Range(throwInterval.x, throwInterval.y) / Mathf.Max(0.1f, PhaseValue(throwRatePerPhase));
        }

        /// <summary>Düşme yeri: canavarın arkası, yeterince uzakta, şeritte boş ve düz zemin (her zaman zıplanabilir).</summary>
        private bool PickThrow()
        {
            float pz = player.transform.position.z;
            float z = transform.position.z - landBehind;
            if (z - pz < minLandAhead) return false;
            int first = Random.value < aimAtPlayerLane ? player.Lane : Random.Range(-1, 2);
            for (int i = 0; i < 3; i++)
            {
                int l = (first + 1 + i) % 3 - 1;
                float x = LaneWorldX(l);
                float g = player.GroundHeight;
                if (ObstacleField.AnySolid(x, 0.9f, z - 4f, z + 4f)) continue;
                if (Mathf.Abs(ObstacleField.SurfaceAt(x, z, g) - g) > 0.05f) continue;
                if (ObstacleField.HitsCeiling(x, z, g, g + 2.6f) || ObstacleField.HitsCeiling(x, z - 2f, g, g + 2.6f)) continue;
                throwLane = l;
                throwLandZ = z;
                return true;
            }
            return false;
        }

        private Obstacle TakeThrown()
        {
            Obstacle o = thrownPool.Count > 0 ? thrownPool.Pop() : Instantiate(thrownTemplate, transform.parent);
            o.enabled = false;           // havadayken engel değil
            o.gameObject.SetActive(true);
            return o;
        }

        private void UpdateThrownObstacles()
        {
            float pz = player.transform.position.z;
            for (int i = thrown.Count - 1; i >= 0; i--)
            {
                if (thrown[i].MaxZ > pz - 15f) continue;
                Release(thrown[i]);
                thrown.RemoveAt(i);
            }
        }

        private void ClearThrown()
        {
            foreach (Obstacle o in thrown) Release(o);
            thrown.Clear();
            if (flyingThrow != null) { Release(flyingThrow.GetComponent<Obstacle>()); flyingThrow = null; }
            throwStart = -1f;
            if (landingMarker != null) landingMarker.gameObject.SetActive(false);
        }

        private void Release(Obstacle o)
        {
            if (o == null) return;
            o.gameObject.SetActive(false);
            thrownPool.Push(o);
        }

        // ------------------------------------------------------------------
        // Kaçış / yenilgi / devam
        // ------------------------------------------------------------------

        private void Escape()
        {
            state = State.Escaped;
            ClearThrown();
            Anim("Run");
            Escaped?.Invoke();
        }

        /// <summary>Elmasla devam: canavar yeniden yakına gelir, kovalama sürer.</summary>
        public void Recover()
        {
            if (IsDefeated) return;
            ClearThrown();
            gap = startGap;
            pull = 0f;
            state = State.Chasing;
            onRoof = PlayerOnRoof;
            nextThrow = Time.time + Random.Range(throwInterval.x, throwInterval.y);
            ApplyVisibility();
            Anim("Run");
        }

        private System.Collections.IEnumerator HitStop()
        {
            if (Time.timeScale < 0.99f) yield break;   // başka bir yavaşlatma varken karışma
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(hitStop);
            if (Time.timeScale > 0.01f) Time.timeScale = 1f;   // duraklatılmadıysa
        }

        /// <summary>Final sinematiği bitti: bölüm tamam.</summary>
        public void FinishDefeat()
        {
            if (state == State.Done) return;
            state = State.Done;
            Time.timeScale = 1f;
            Defeated?.Invoke();
        }

        /// <summary>Köprüye geçince: canavar başlangıç mesafesine (görünür, aynı dünyada).</summary>
        public void ResetForArena()
        {
            ClearThrown();
            onRoof = false;
            upPortalOpen = false;
            gap = Mathf.Max(minGap + 4f, Mathf.Min(gap, startGap));
            pull = 0f;
            y = player.GroundHeight;
            vy = 0f;
            ApplyVisibility();
        }

        private void Defeat()
        {
            state = State.Defeated;
            Defeating?.Invoke();
            defeatedAt = Time.unscaledTime;
            ClearThrown();
            Time.timeScale = defeatSlowMo;
            if (cameraFollow != null) cameraFollow.Shake(1f);
            float z = transform.position.z;   // koşucu zafer anında rahat koşsun
            foreach (var sp in FindObjectsByType<ObstacleSpawner>(FindObjectsSortMode.None))
                if (sp.isActiveAndEnabled) sp.ClearRange(player.transform.position.z - 2f, z + 60f);
        }

        private void UpdateDefeat()
        {
            float t = Time.unscaledTime - defeatedAt;
            if (t > 1.1f && Time.timeScale < 1f && Time.timeScale > 0f) Time.timeScale = 1f;
            gap = Mathf.Max(minGap, gap - 6f * Time.deltaTime);   // devrilirken koşucu yaklaşır
            if (visual != null && animator == null)               // canlandırıcı yoksa: öne devril
            {
                float k = Mathf.Clamp01(t / 1.2f);
                visual.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 82f, k * k), 0f, 0f);
            }
            if (t >= defeatDelay && state == State.Defeated)
            {
                state = State.Done;   // bir kez
                Time.timeScale = 1f;
                Defeated?.Invoke();
            }
        }

        // ------------------------------------------------------------------
        // Konum / görsel
        // ------------------------------------------------------------------

        private float LaneWorldX(int l) => player.LaneOriginX + l * player.LaneWidth;

        private void Place()
        {
            Vector3 pp = player.transform.position;
            float z = pp.z + gap;
            float target = player.LaneOriginX + lane * player.LaneWidth;
            float dt = Time.deltaTime;
            float x = Mathf.Lerp(transform.position.x, target, 1f - Mathf.Exp(-laneSharpness * dt));
            if (Mathf.Abs(transform.position.x - target) > player.LaneWidth * 3f) x = target;   // dünya değişti
            // yüzey: engellerin üstünden sıçrar, boşlukların (çatı arası) üstünden atlar
            float g = player.GroundHeight;
            float surface = Mathf.Max(ObstacleField.SurfaceAt(x, z, g), ObstacleField.SurfaceAt(x, z + 1.5f, g), g);
            if (surface > y + 0.3f && vy <= 0f)
                vy = Mathf.Sqrt(2f * hopGravity * (surface - y + hopClearance));   // sıçra
            if (vy != 0f || y > surface)
            {
                vy -= hopGravity * dt;
                y += vy * dt;
                if (y <= surface) { y = surface; vy = 0f; }
            }
            else y = surface;
            transform.position = new Vector3(x, y, z);

            if (visual == null || state == State.Defeated) return;
            // uyarı: gideceği yöne eğilir; atışa hazırlanırken geriye döner gibi kollarını kaldırır (yan yatma)
            float lean = 0f;
            if (telegraphUntil > 0f) lean = -Mathf.Sign(pendingLane - lane) * leanAngle;
            else if (Mathf.Abs(x - target) > 0.05f) lean = -Mathf.Sign(target - x) * leanAngle * 0.5f;
            float pitch = animator != null ? 0f : (throwStart >= 0f ? -12f : (pull > 0f ? -18f : 0f));   // canlandırıcı varsa gövdeyi o eğer
            bool facePlayer = state == State.Intro && Time.time - stateTime < introRoarTime;   // girişte koşucuya dönüp kükrer
            Quaternion want = Quaternion.Euler(pitch, facePlayer ? 180f : 0f, lean);
            visual.localRotation = Quaternion.Slerp(visual.localRotation, want, 1f - Mathf.Exp((facePlayer ? -30f : -7f) * dt));
            if (animator != null) animator.SetFloat(RunSpeedParam, Mathf.Clamp(player.RunSpeed / animReferenceSpeed, 0.8f, 1.7f));
        }

        private void UpdateFlash()
        {
            if (flashRenderers == null || flashRenderers.Length == 0) return;
            float k = Mathf.Clamp01((flashUntil - Time.time) / 0.35f);
            foreach (Renderer r in flashRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", hitFlash * (k * 1.4f));
                r.SetPropertyBlock(block);
            }
        }

        private static readonly int RunSpeedParam = Animator.StringToHash("RunSpeed");

        private void Anim(string trigger)
        {
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) return;
            foreach (var p in animator.parameters)
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == trigger) { animator.SetTrigger(trigger); return; }
        }
    }
}
