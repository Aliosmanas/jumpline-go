using Jumpline.Level;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Jumpline.Player
{
    /// <summary>
    /// Koşucu (Subway Surfers kontrolleri): sürekli ileri (+Z) koşar, 3 şerit.
    ///  • Yukarı kaydır (W / ↑ / Boşluk): zıpla — sabit yükseklik; hız arttıkça hava süresi kısalır (iniş mesafesi dengeli).
    ///  • Aşağı kaydır (S / ↓): yerdeyse kay (bariyer altı), havadaysa hızla in ve kay.
    ///  • Sağa/sola kaydır (A/D, ←/→): şerit değiştir; havadayken de olur. Her kaydırma tek hareket.
    ///  • İnişten hemen önce yapılan yukarı kaydırma tamponlanır (inince zıplar).
    /// Yüzey: <see cref="ObstacleField"/> — rampaya/araca çıkar, kenardan düşer; aşamadığı engele çarparsa ölür.
    /// Tavan / bariyer: gövde (ayak..kafa) kalınlığıyla çakışırsa çarpar; kayarken kafa alçalır.
    /// Zipline (Level/Zipline.cs): halatın önünde ZIPLAYAN koşucu tutunur; zıplamayan boşluğa düşer.
    /// Ölüm: önden çarpınca geriye savrulur (ağır çekim + kamera sarsıntısı), sırtüstü düşer → <see cref="Died"/>.
    /// Yandan çarpma (şerit değiştirirken engelin yanına): ölmez — sarsılır, eski şeridine seker, koşmaya devam eder.
    /// Hız: doyan eğri (taban → tavan, τ sn); her %10 artışta <see cref="SpeedLevelChanged"/>. Puan için <see cref="Distance"/>.
    /// Dünya geçişi (Level/WorldSwitcher.cs): Teleport ile sokak ↔ çatı.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        private enum State { Running, Airborne, ZipGrab, ZipSlide, Dead, FallingDead, Sliding }

        [Header("Koşu / hız")]
        [Tooltip("Başlangıç koşu hızı (m/sn).")]
        [SerializeField, Min(0f)] private float runSpeed = 11f;
        [Tooltip("Ulaşılabilecek en yüksek hız (m/sn). 18 ≈ başlangıcın 1.6 katı — herkese hitap eden tavan.")]
        [SerializeField, Min(0f)] private float maxSpeed = 18f;
        [Tooltip("Hız eğrisi zaman sabiti (sn): hız = taban + (tavan − taban)·(1 − e^(−t/τ)). " +
                 "τ=110 → 30 sn 1.15x, 1 dk 1.27x, 2 dk 1.42x, 5 dk 1.6x. İlk dakikalar hızlı, sonra yavaşça doyar.")]
        [SerializeField, Min(1f)] private float speedTimeConstant = 110f;
        [Tooltip("\"HIZLANDI\" yazısı hız çarpanı her bu kadar arttığında çıkar (0.1 = %10).")]
        [SerializeField, Min(0.01f)] private float speedBannerStep = 0.1f;
        [Tooltip("Koşu animasyonunun bu hızda 1x oynadığı varsayılır; üstünde hızlanır.")]
        [SerializeField, Min(0.1f)] private float animReferenceSpeed = 9f;
        [SerializeField, Min(1f)] private float maxAnimSpeed = 1.8f;

        [Header("Şeritler / kaydırma")]
        [Tooltip("Şerit merkezleri arası mesafe (Blender/scripts/build_street.py LANE_W).")]
        [SerializeField, Min(0.1f)] private float laneWidth = 2.2f;
        [Tooltip("Şerit geçiş keskinliği (büyük = daha hızlı geçiş).")]
        [SerializeField, Min(0.1f)] private float laneChangeSharpness = 16f;
        [Tooltip("Kaydırma sayılması için parmak hareketi (ekran genişliğinin oranı).")]
        [SerializeField, Range(0.01f, 0.5f)] private float swipeThreshold = 0.05f;

        [Header("Zıplama (yukarı kaydır)")]
        [Tooltip("Zıplama tepe yüksekliği (m): alçak engeller (≤1.35), taksi kaputu, bariyer üstü aşılır; sur kapısına (3.7) kafa değmez.")]
        [SerializeField, Min(0.3f)] private float jumpHeight = 1.9f;
        [SerializeField, Min(0f)] private float gravity = 30f;
        [Tooltip("Hız arttıkça yerçekimi ve zıplama hızı birlikte artar: yükseklik aynı, hava süresi kısa (iniş noktası dengeli).")]
        [SerializeField] private bool scaleJumpWithSpeed = true;
        [Tooltip("İnişten bu kadar önce yapılan yukarı kaydırma inince zıplatır.")]
        [SerializeField, Min(0f)] private float jumpBuffer = 0.15f;
        [Tooltip("Dünyanın zemin yüksekliği (sokak 0, çatı = çatı yüksekliği). Engeller bunun üstüne eklenir.")]
        [SerializeField] private float groundHeight = 0f;

        [Header("Yüzey / engel")]
        [Tooltip("Bu kadar yüksek basamağa koşarken çıkılır (rampa); daha yükseği çarpma.")]
        [SerializeField, Min(0f)] private float stepUp = 0.3f;
        [Tooltip("Koşarken bu kadar alçalan yüzeye yapışır (rampa inişi); daha fazlası düşüş.")]
        [SerializeField, Min(0f)] private float snapDown = 0.25f;
        [Tooltip("Ayaktan kafaya yükseklik (tavana çarpma).")]
        [SerializeField, Min(0.5f)] private float headHeight = 1.7f;

        [Header("Kayma (aşağı kaydır)")]
        [Tooltip("Kayma süresi (sn). Animasyon bu süreye göre hızlandırılır/yavaşlatılır → tam senkron.")]
        [SerializeField, Min(0.2f)] private float slideDuration = 0.75f;
        [Tooltip("Slide klibinin gerçek uzunluğu (Blender build_anims.py: 24 kare / 30 fps).")]
        [SerializeField, Min(0.05f)] private float slideClipLength = 0.8f;
        [Tooltip("Kayarken kafa yüksekliği (bariyer altından geçiş).")]
        [SerializeField, Min(0.3f)] private float slideHeadHeight = 1.0f;
        [Tooltip("Havadayken aşağı kaydırınca yere iniş hızı (sonra kayar).")]
        [SerializeField, Min(0f)] private float fastDropSpeed = 26f;

        [Header("Zipline")]
        [Tooltip("Halat üzerinde ileri hız (m/sn).")]
        [SerializeField, Min(0.1f)] private float ziplineSpeed = 14f;
        [Tooltip("Halattan bırakınca aşağı başlangıç hızı.")]
        [SerializeField] private float ziplineReleaseVelocity = 1.5f;
        [SerializeField] private ParticleSystem ziplineSparks;
        [Tooltip("Kayarken ayak altından toz/kıvılcım (kayma süresince akar).")]
        [SerializeField] private ParticleSystem slideFx;

        [Header("Ölüm (geriye savrulma)")]
        [Tooltip("Çarpınca geriye savrulma hızı (m/sn).")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 4.5f;
        [Tooltip("Çarpınca yukarı savrulma hızı (m/sn).")]
        [SerializeField, Min(0f)] private float knockbackUp = 3.2f;

        [Header("Yandan çarpma (sendeleme)")]
        [Tooltip("Yandan çarpınca hız bu oranla düşer (kısa süre sonra toparlanır).")]
        [SerializeField, Range(0.3f, 1f)] private float stumbleSlow = 0.8f;
        [Tooltip("Sendeleme sonrası yeniden yandan çarpma için bekleme (sn).")]
        [SerializeField, Min(0f)] private float stumbleCooldown = 0.35f;
        [Tooltip("Yerde sürtünmeyle duruş (m/sn²).")]
        [SerializeField, Min(0f)] private float groundFriction = 14f;
        [Tooltip("Çarpma anında ağır çekim (zaman ölçeği) ve süresi (gerçek sn).")]
        [SerializeField, Range(0.05f, 1f)] private float deathSlowMo = 0.35f;
        [SerializeField, Min(0f)] private float deathSlowMoTime = 0.5f;
        [Tooltip("Ölüm animasyonundan sonra oyun bitti paneline kadar bekleme.")]
        [SerializeField, Min(0f)] private float gameOverDelay = 1.6f;
        [Tooltip("Zeminin bu kadar altına düşen koşucu ölür.")]
        [SerializeField, Min(0.5f)] private float fallDeathDepth = 3f;
        [Tooltip("Düşüşten oyun bitti paneline kadar bekleme.")]
        [SerializeField, Min(0f)] private float fallGameOverDelay = 0.9f;

        [Header("Görsel")]
        [SerializeField] private Animator animator;

        private State state = State.Running;
        private float verticalVelocity;
        private float gravityNow;
        private bool wasPressed;
        private int lane;                 // -1 sol, 0 orta, +1 sağ
        private Vector2 pressStart;
        private bool pressConsumed;       // bu basışta bir kaydırma işlendi
        private float laneOriginX;        // orta şeridin dünya x'i (sokak 0, çatı dünyası başka x)
        private float deathTime;
        private float deathSpeed;         // ölümde z hızı (savrulma: negatif)
        private bool diedRaised;
        private float slowMoUntil = -1f;
        private float currentSpeed;
        private float aliveTime;
        private int speedLevel;
        private float slideEnd;
        private float distance;          // koşulan yol (m) — puan: metre başına 10
        private bool slideQueued;         // havada aşağı kaydırıldı: inince kay
        private float jumpQueuedUntil = -1f;
        private int laneFrom;            // son şerit değişiminden önceki şerit (yandan çarpınca geri dönülür)
        private float lastStumble = -10f;

        // zipline: kök (ayak) yolu
        private Vector3 zipA, zipB;
        private float zipSag;
        private float grabStartY, grabStartZ;

        private const int LaneCount = 3;
        private const int MaxLane = LaneCount / 2;

        /// <summary>Ölüm animasyonu bitti — oyun bitti paneli açılmalı.</summary>
        public event System.Action Died;
        /// <summary>Çarpma anı (kamera sarsıntısı vb.).</summary>
        public event System.Action Crashed;
        /// <summary>Yandan çarpıp sendeledi (hafif sarsıntı).</summary>
        public event System.Action Stumbled;
        /// <summary>Hız kademesi arttı (1, 2, 3…).</summary>
        public event System.Action<int> SpeedLevelChanged;

        public float Charge01 => 0f;      // eski HUD uyumluluğu (şarj yok)
        /// <summary>false: kaydırma/tuş yok sayılır (final sinematiği). Koşu sürer.</summary>
        public bool InputEnabled { get; set; } = true;
        public float RunSpeed => currentSpeed;
        /// <summary>Bu koşuda katedilen yol (m).</summary>
        public float Distance => distance;
        public float SpeedFactor => runSpeed > 0f ? currentSpeed / runSpeed : 1f;
        public int Lane => lane;
        public float LaneWidth => laneWidth;
        public float LaneOriginX => laneOriginX;
        public float GroundHeight => groundHeight;
        public bool OnZipline => state == State.ZipGrab || state == State.ZipSlide;
        public bool IsAirborne => state == State.Airborne;
        public bool IsSliding => state == State.Sliding;
        public bool IsDead => state == State.Dead || state == State.FallingDead;

        private float JumpScale => scaleJumpWithSpeed ? Mathf.Sqrt(Mathf.Max(1f, SpeedFactor)) : 1f;

        private static readonly int RunTrigger = Animator.StringToHash("Run");
        private static readonly int CrouchTrigger = Animator.StringToHash("Crouch");
        private static readonly int JumpTrigger = Animator.StringToHash("Jump");
        private static readonly int FallTrigger = Animator.StringToHash("Fall");
        private static readonly int LandTrigger = Animator.StringToHash("Land");
        private static readonly int ZiplineTrigger = Animator.StringToHash("Zipline");
        private static readonly int DeathTrigger = Animator.StringToHash("Death");
        private static readonly int SlideTrigger = Animator.StringToHash("Slide");
        private static readonly int SlideSpeedParam = Animator.StringToHash("SlideSpeed");
        private static readonly int[] AllTriggers = { RunTrigger, CrouchTrigger, JumpTrigger, FallTrigger, LandTrigger, ZiplineTrigger, DeathTrigger, SlideTrigger };

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            laneOriginX = transform.position.x;
            currentSpeed = runSpeed;
            gravityNow = gravity;
        }

        private void OnDisable()
        {
            if (slowMoUntil > 0f) Time.timeScale = 1f;
        }

        private void Update()
        {
            UpdateSlowMo();
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // duraklatıldı

            if (IsDead)
            {
                if (animator != null) animator.speed = 1f;
                UpdateDead(dt);
                return;
            }
            UpdateSpeed(dt);
            if (InputEnabled)
            {
                ReadSwipes();
                ReadKeys();
            }

            Vector3 p = transform.position;
            float laneX = laneOriginX + lane * laneWidth;
            float laneK = 1f - Mathf.Exp(-laneChangeSharpness * dt);

            switch (state)
            {
                case State.Sliding:
                    if (Time.time >= slideEnd)
                    {
                        state = State.Running;
                        Trigger(RunTrigger);
                    }
                    break;
                case State.Airborne:
                    verticalVelocity -= gravityNow * dt;
                    break;
                case State.ZipGrab:
                {
                    // halata doğru sıçrama: z ilerlerken yükseklik yumuşakça halat başına çıkar
                    p.z += currentSpeed * dt;
                    float t = Mathf.InverseLerp(grabStartZ, zipA.z, p.z);
                    p.y = Mathf.Lerp(grabStartY, zipA.y, Mathf.SmoothStep(0f, 1f, t));
                    p.x = Mathf.Lerp(p.x, laneX, laneK);
                    transform.position = p;
                    if (t >= 1f) BeginZipSlide();
                    return;
                }
                case State.ZipSlide:
                {
                    p.z += Mathf.Max(ziplineSpeed, currentSpeed * 1.3f) * dt;
                    float t = Mathf.InverseLerp(zipA.z, zipB.z, p.z);
                    p.y = Mathf.Lerp(zipA.y, zipB.y, t) - zipSag * 4f * t * (1f - t);
                    p.x = Mathf.Lerp(p.x, laneX, laneK);
                    transform.position = p;
                    if (t >= 1f) ReleaseZipline();
                    return;
                }
            }

            float prevY = p.y;
            float oldX = p.x;
            p.z += currentSpeed * dt;
            p.x = Mathf.Lerp(p.x, laneX, laneK);
            p.y += verticalVelocity * dt;

            // yandan çarpma: yeni x engelin içine giriyor ama eski x'te yol açıksa → sendele, eski şeride dön (ölüm yok)
            if (Mathf.Abs(p.x - oldX) > 0.0005f && Blocked(p.x, p.z, p.y) && !Blocked(oldX, p.z, p.y))
            {
                p.x = oldX;
                Stumble();
            }

            float surface = ObstacleField.SurfaceAt(p.x, p.z, groundHeight);
            if (state == State.Airborne)
            {
                if (verticalVelocity <= 0f && p.y <= surface)
                {
                    if (prevY < surface - stepUp)
                    {
                        Crash(ref p, prevY);   // yanından/önünden girdi
                        return;
                    }
                    p.y = surface;
                    verticalVelocity = 0f;
                    state = State.Running;
                    if (slideQueued) StartSlide();                       // havada aşağı kaydırıldı
                    else if (Time.time <= jumpQueuedUntil) Jump();       // inmeden yukarı kaydırıldı
                    else Trigger(LandTrigger);
                }
                else if (surface > p.y + stepUp)
                {
                    Crash(ref p, prevY);       // yükselirken engelin yüzüne çarptı
                    return;
                }
            }
            else
            {
                if (surface > p.y + stepUp)
                {
                    Crash(ref p, prevY);
                    return;
                }
                if (surface >= p.y - snapDown) p.y = surface;   // düz zemin, rampa çıkışı/inişi
                else
                {
                    // kenardan düştü (araç önü, çatı kenarı)
                    slideQueued = state == State.Sliding;
                    state = State.Airborne;
                    verticalVelocity = 0f;
                    gravityNow = gravity * JumpScale * JumpScale;
                    Trigger(FallTrigger);
                }
            }

            if (state == State.Sliding && slideFx != null && Time.frameCount % 2 == 0) slideFx.Emit(2);
            // tavan / bariyer: gövde (ayak..kafa) sur kapısının ya da kalasın içine giriyorsa çarpar; kayarken kafa alçak
            float head = state == State.Sliding ? slideHeadHeight : headHeight;
            if (ObstacleField.HitsCeiling(p.x, p.z, p.y, p.y + head))
            {
                Crash(ref p, prevY);
                return;
            }
            if (p.y < groundHeight - fallDeathDepth) BeginFallDeath();
            transform.position = p;
        }

        // ------------------------------------------------------------------
        // Girdi
        // ------------------------------------------------------------------

        /// <summary>Kaydırma: eşik aşılınca baskın yön tek hareket olarak işlenir (yukarı/aşağı/sağ/sol).</summary>
        private void ReadSwipes()
        {
            bool pressed = IsPressed(out Vector2 pointer);
            if (pressed && !wasPressed)
            {
                pressStart = pointer;
                pressConsumed = false;
            }
            wasPressed = pressed;
            if (!pressed || pressConsumed) return;
            float w = Mathf.Max(1, Screen.width);
            float dx = (pointer.x - pressStart.x) / w;
            float dy = (pointer.y - pressStart.y) / w;
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) < swipeThreshold) return;
            pressConsumed = true;
            if (Mathf.Abs(dy) > Mathf.Abs(dx))
            {
                if (dy > 0f) RequestJump();
                else RequestSlide();
            }
            else ChangeLane(dx > 0f ? 1 : -1);
        }

        private void ReadKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) ChangeLane(-1);
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) ChangeLane(1);
            if (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) RequestJump();
            if (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame) RequestSlide();
        }

        private void RequestJump()
        {
            switch (state)
            {
                case State.Running:
                case State.Sliding:
                    Jump();
                    break;
                case State.Airborne:
                    jumpQueuedUntil = Time.time + jumpBuffer;
                    break;
            }
        }

        /// <summary>Aşağı kaydırma: yerdeyse hemen kay; havadaysa hızla in, değince kay.</summary>
        private void RequestSlide()
        {
            switch (state)
            {
                case State.Running:
                    StartSlide();
                    break;
                case State.Airborne:
                    verticalVelocity = Mathf.Min(verticalVelocity, -fastDropSpeed * JumpScale);
                    slideQueued = true;
                    jumpQueuedUntil = -1f;
                    break;
            }
        }

        private void Jump()
        {
            float k = JumpScale;
            gravityNow = gravity * k * k;
            verticalVelocity = Mathf.Sqrt(2f * gravityNow * jumpHeight);   // tepe = jumpHeight (hızdan bağımsız)
            state = State.Airborne;
            slideQueued = false;
            jumpQueuedUntil = -1f;
            Trigger(JumpTrigger);
        }

        private void StartSlide()
        {
            if (slideFx != null) slideFx.Emit(24);
            slideQueued = false;
            state = State.Sliding;
            slideEnd = Time.time + slideDuration;
            if (animator != null)
            {
                animator.speed = 1f;
                animator.SetFloat(SlideSpeedParam, slideClipLength / slideDuration);   // klip tam kayma süresince oynar
            }
            Trigger(SlideTrigger);
        }

        private void ChangeLane(int dir)
        {
            if (OnZipline || IsDead) return; // halattayken şerit sabit
            int next = Mathf.Clamp(lane + dir, -MaxLane, MaxLane); // en kenarda bir şey olmaz (çarpma yok)
            if (next == lane) return;
            laneFrom = lane;
            lane = next;
        }

        /// <summary>Bu noktada gövde bir engelle çakışıyor mu (yüzey basamaktan yüksek ya da tavan/bariyer).</summary>
        private bool Blocked(float x, float z, float y)
        {
            float head = state == State.Sliding ? slideHeadHeight : headHeight;
            return ObstacleField.SurfaceAt(x, z, groundHeight) > y + stepUp || ObstacleField.HitsCeiling(x, z, y, y + head);
        }

        /// <summary>Yandan çarpma: eski şeride sek, kısa yavaşla, kamerayı hafif salla.</summary>
        private void Stumble()
        {
            lane = laneFrom;
            if (Time.time - lastStumble < stumbleCooldown) return;
            lastStumble = Time.time;
            Jumpline.Core.Haptics.Pulse(Jumpline.Core.Haptics.Medium, 150);
            currentSpeed *= stumbleSlow;    // UpdateSpeed hedef hıza yumuşakça geri çıkarır
            if (state == State.Running) Trigger(LandTrigger);   // kısa sendeleme (diz kırma)
            Stumbled?.Invoke();
        }

        private void UpdateSpeed(float dt)
        {
            aliveTime += dt;
            distance += currentSpeed * dt;
            // doyan hız eğrisi: başta belirgin, sonra yavaş artar; tavan maxSpeed (ani 2x yok)
            currentSpeed = runSpeed + (Mathf.Max(runSpeed, maxSpeed) - runSpeed) * (1f - Mathf.Exp(-aliveTime / speedTimeConstant));
            int level = Mathf.FloorToInt((SpeedFactor - 1f) / speedBannerStep + 0.0001f);
            if (level > speedLevel)
            {
                speedLevel = level;
                SpeedLevelChanged?.Invoke(level);
            }
            if (animator != null && state != State.Sliding)
                animator.speed = state == State.Running ? Mathf.Clamp(currentSpeed / animReferenceSpeed, 1f, maxAnimSpeed) : 1f;
        }

        // ------------------------------------------------------------------
        // Dışarıdan çağrılanlar
        // ------------------------------------------------------------------

        /// <summary>Zipline başlat: halat başı/sonu (orta şerit, dünya), sarkma, el yüksekliği (ayaktan ele).</summary>
        public void BeginZipline(Vector3 ropeStart, Vector3 ropeEnd, float sag, float handHeight)
        {
            if (OnZipline || IsDead) return;
            zipA = ropeStart - Vector3.up * handHeight;
            zipB = ropeEnd - Vector3.up * handHeight;
            zipSag = sag;
            grabStartY = transform.position.y;
            grabStartZ = Mathf.Min(transform.position.z, zipA.z - 0.05f);
            slideQueued = false;
            jumpQueuedUntil = -1f;
            if (transform.position.z >= zipA.z - 0.05f) { BeginZipSlide(); return; }   // tam halatın altında yakaladı
            state = State.ZipGrab;
            verticalVelocity = 0f;
            Trigger(JumpTrigger);
        }

        /// <summary>Elmasla devam: ölüm yerinde (güvenli noktada) koşmaya döner. Önündeki engelleri çağıran temizler.</summary>
        public void Revive(Vector3 position)
        {
            if (!IsDead) return;
            transform.position = new Vector3(laneOriginX + lane * laneWidth, position.y, position.z);
            state = State.Running;
            verticalVelocity = 0f;
            gravityNow = gravity;
            deathSpeed = 0f;
            diedRaised = false;
            slideQueued = false;
            jumpQueuedUntil = -1f;
            if (animator != null) animator.speed = 1f;
            Trigger(RunTrigger);
        }

        /// <summary>Başka dünyaya ışınla (sokak ↔ çatı). Şerit korunur.</summary>
        public void Teleport(Vector3 position, float ground, float originX)
        {
            if (IsDead) return;
            laneOriginX = originX;
            groundHeight = ground;
            position.x = originX + lane * laneWidth;
            transform.position = position;
            verticalVelocity = 0f;
            slideQueued = false;
            jumpQueuedUntil = -1f;
            if (ziplineSparks != null) ziplineSparks.Stop();
            if (position.y > ground + 0.01f)
            {
                state = State.Airborne;
                Trigger(FallTrigger);
            }
            else
            {
                state = State.Running;
                Trigger(RunTrigger);
            }
        }

        // ------------------------------------------------------------------
        // Ölüm
        // ------------------------------------------------------------------

        private void Crash(ref Vector3 p, float prevY)
        {
            Obstacle hit = ObstacleField.SolidAt(p.x, p.z);
            if (hit != null && hit.TryGetComponent(out Breakable breakable)) breakable.Break(currentSpeed);
            p.y = prevY;    // yüksekliği koru (yan şeridin araç tavanına ışınlanma hatası düzeltildi)
            p.z -= 0.25f;   // engelin içine girmesin
            transform.position = p;
            state = State.Dead;
            deathTime = Time.time;
            deathSpeed = -knockbackSpeed;          // geriye savrul
            verticalVelocity = knockbackUp;
            gravityNow = gravity;
            if (ziplineSparks != null) ziplineSparks.Stop();
            Jumpline.Core.Haptics.Pulse(Jumpline.Core.Haptics.Heavy, 255);
            Trigger(DeathTrigger);
            if (deathSlowMo < 1f && deathSlowMoTime > 0f)
            {
                Time.timeScale = deathSlowMo;
                slowMoUntil = Time.unscaledTime + deathSlowMoTime;
            }
            Crashed?.Invoke();
        }

        private void UpdateSlowMo()
        {
            if (slowMoUntil < 0f || Time.unscaledTime < slowMoUntil) return;
            slowMoUntil = -1f;
            if (Time.timeScale > 0f) Time.timeScale = 1f;   // duraklatılmadıysa normale dön
        }

        private void BeginFallDeath()
        {
            if (IsDead) return;
            state = State.FallingDead;
            deathTime = Time.time;
            deathSpeed = currentSpeed * 0.5f;
            Trigger(FallTrigger);
        }

        private void UpdateDead(float dt)
        {
            Vector3 p = transform.position;
            if (state == State.Dead)
            {
                // geriye savrulma: önce havada geri uçar, yere düşünce sürtünmeyle durur
                verticalVelocity -= gravityNow * dt;
                float nz = p.z + deathSpeed * dt;
                float ahead = ObstacleField.SurfaceAt(p.x, nz, groundHeight);
                if (ahead <= p.y + stepUp) p.z = nz;       // geride engel varsa ona yaslan
                p.y += verticalVelocity * dt;
                float floor = ObstacleField.SurfaceAt(p.x, p.z, groundHeight);
                if (p.y <= floor)
                {
                    p.y = floor;
                    verticalVelocity = 0f;
                    deathSpeed = Mathf.MoveTowards(deathSpeed, 0f, groundFriction * dt);
                }
                transform.position = p;
                RaiseDied(gameOverDelay);
            }
            else
            {
                // boşluğa düşüş: düşme animasyonuyla aşağı
                verticalVelocity -= gravity * dt;
                p.z += deathSpeed * dt;
                p.y += verticalVelocity * dt;
                float floor = ObstacleField.SurfaceAt(p.x, p.z, groundHeight);
                if (p.y < floor) { p.y = floor; deathSpeed = 0f; }
                transform.position = p;
                RaiseDied(fallGameOverDelay);
            }
        }

        private void RaiseDied(float delay)
        {
            if (diedRaised || Time.time - deathTime < delay) return;
            diedRaised = true;
            Died?.Invoke();
        }

        // ------------------------------------------------------------------

        private void BeginZipSlide()
        {
            state = State.ZipSlide;
            Trigger(ZiplineTrigger);
            if (ziplineSparks != null) ziplineSparks.Play();
        }

        private void ReleaseZipline()
        {
            state = State.Airborne;
            verticalVelocity = -ziplineReleaseVelocity;
            gravityNow = gravity;
            Trigger(FallTrigger);
            if (ziplineSparks != null) ziplineSparks.Stop();
        }

        private void Trigger(int id)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (int other in AllTriggers)
                if (other != id) animator.ResetTrigger(other);
            animator.SetTrigger(id);
        }

        /// <summary>Dokunma / fare basılı mı (konumuyla)? Arayüz butonlarına (duraklat vb.) basmak sayılmaz.</summary>
        private static bool IsPressed(out Vector2 pointer)
        {
            pointer = default;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (OverUI(touch.touchId.ReadValue())) return false;
                pointer = touch.position.ReadValue();
                return true;
            }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed && !OverUI(-1))
            {
                pointer = Mouse.current.position.ReadValue();
                return true;
            }
            return false;
        }

        private static bool OverUI(int pointerId)
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject(pointerId);
        }
    }
}
