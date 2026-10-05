using Jumpline.Level;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Jumpline.Player
{
    /// <summary>
    /// Koşucu: sürekli ileri (+Z) koşar. Basılı tut = zıplama şarjı, bırak = zıpla; basma süresi zıplama gücünü belirler.
    /// 3 şerit (Subway Surfers gibi): sağa/sola kaydır ya da A/D, ←/→ ile şerit değiştirir; havadayken de olur.
    /// Kaydırma o basışın şarjını iptal eder (zıplatmaz). Girdi: dokunma (Android), fare sol tuş + Boşluk (editör).
    /// Yüzey: <see cref="ObstacleField"/> — rampaya/kasaya çıkar, kenardan düşer; aşamadığı engele çarparsa ölür.
    /// Zipline (Level/Zipline.cs): halatın önünde ZIPLAYAN koşucu tutunur → kayar → bırakır; zıplamayan boşluğa düşer.
    /// Ölüm: çarpınca ölme animasyonu, boşluğa düşünce düşüş animasyonu → <see cref="Died"/> (oyun bitti paneli).
    /// Dünya geçişi (Level/WorldSwitcher.cs): Teleport ile sokak ↔ çatı.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        private enum State { Running, Charging, Airborne, ZipGrab, ZipSlide, Dead, FallingDead }

        [Header("Koşu")]
        [SerializeField, Min(0f)] private float runSpeed = 7f;

        [Header("Şeritler")]
        [Tooltip("Şerit merkezleri arası mesafe (Blender/scripts/build_street.py LANE_W).")]
        [SerializeField, Min(0.1f)] private float laneWidth = 2.2f;
        [Tooltip("Şerit geçiş keskinliği (büyük = daha hızlı geçiş).")]
        [SerializeField, Min(0.1f)] private float laneChangeSharpness = 14f;
        [Tooltip("Kaydırma sayılması için yatay hareket (ekran genişliğinin oranı).")]
        [SerializeField, Range(0.01f, 0.5f)] private float swipeThreshold = 0.06f;

        [Header("Zıplama")]
        [SerializeField, Min(0f)] private float minJumpForce = 5f;
        [SerializeField, Min(0f)] private float maxJumpForce = 11f;
        [SerializeField, Min(0.01f)] private float minChargeTime = 0.05f;
        [SerializeField, Min(0.02f)] private float maxChargeTime = 0.75f;
        [SerializeField, Min(0f)] private float gravity = 28f;
        [Tooltip("Şarj sırasında koşu hızı çarpanı (çömelirken biraz yavaşlar).")]
        [SerializeField, Range(0f, 1f)] private float chargeSpeedMultiplier = 0.85f;
        [Tooltip("Dünyanın zemin yüksekliği (sokak 0, çatı = çatı yüksekliği). Engeller bunun üstüne eklenir.")]
        [SerializeField] private float groundHeight = 0f;

        [Header("Yüzey / engel")]
        [Tooltip("Bu kadar yüksek basamağa koşarken çıkılır (rampa); daha yükseği çarpma.")]
        [SerializeField, Min(0f)] private float stepUp = 0.3f;
        [Tooltip("Koşarken bu kadar alçalan yüzeye yapışır (rampa inişi); daha fazlası düşüş.")]
        [SerializeField, Min(0f)] private float snapDown = 0.25f;

        [Header("Zipline")]
        [Tooltip("Halat üzerinde ileri hız (m/sn).")]
        [SerializeField, Min(0.1f)] private float ziplineSpeed = 13f;
        [Tooltip("Halattan bırakınca aşağı başlangıç hızı.")]
        [SerializeField] private float ziplineReleaseVelocity = 1.5f;
        [SerializeField] private ParticleSystem ziplineSparks;

        [Header("Ölüm")]
        [Tooltip("Çarpınca ileri kayarak durma süresi (yumuşak).")]
        [SerializeField, Min(0.01f)] private float deathSlideTime = 0.35f;
        [Tooltip("Ölüm animasyonundan sonra oyun bitti paneline kadar bekleme.")]
        [SerializeField, Min(0f)] private float gameOverDelay = 1.7f;
        [Tooltip("Zeminin bu kadar altına düşen koşucu ölür.")]
        [SerializeField, Min(0.5f)] private float fallDeathDepth = 3f;
        [Tooltip("Düşüşten oyun bitti paneline kadar bekleme.")]
        [SerializeField, Min(0f)] private float fallGameOverDelay = 0.9f;

        [Header("Görsel")]
        [SerializeField] private Animator animator;

        private State state = State.Running;
        private float chargeStart;
        private float verticalVelocity;
        private bool wasPressed;
        private int lane;                 // -1 sol, 0 orta, +1 sağ
        private Vector2 pressStart;
        private bool pressConsumed;       // bu basışta kaydırma yapıldı → bırakınca zıplama yok
        private float laneOriginX;        // orta şeridin dünya x'i (sokak 0, çatı dünyası başka x)
        private float deathTime;
        private float deathSpeed;
        private bool diedRaised;

        // zipline: kök (ayak) yolu
        private Vector3 zipA, zipB;
        private float zipSag;
        private float grabStartY, grabStartZ;

        private const int LaneCount = 3;
        private const int MaxLane = LaneCount / 2;

        /// <summary>Ölüm animasyonu bitti — oyun bitti paneli açılmalı.</summary>
        public event System.Action Died;

        public float Charge01 { get; private set; }
        public float RunSpeed => runSpeed;
        public int Lane => lane;
        public float LaneWidth => laneWidth;
        public float LaneOriginX => laneOriginX;
        public float GroundHeight => groundHeight;
        public bool OnZipline => state == State.ZipGrab || state == State.ZipSlide;
        public bool IsAirborne => state == State.Airborne;
        public bool IsDead => state == State.Dead || state == State.FallingDead;

        private static readonly int RunTrigger = Animator.StringToHash("Run");
        private static readonly int CrouchTrigger = Animator.StringToHash("Crouch");
        private static readonly int JumpTrigger = Animator.StringToHash("Jump");
        private static readonly int FallTrigger = Animator.StringToHash("Fall");
        private static readonly int LandTrigger = Animator.StringToHash("Land");
        private static readonly int ZiplineTrigger = Animator.StringToHash("Zipline");
        private static readonly int DeathTrigger = Animator.StringToHash("Death");
        private static readonly int[] AllTriggers = { RunTrigger, CrouchTrigger, JumpTrigger, FallTrigger, LandTrigger, ZiplineTrigger, DeathTrigger };

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            laneOriginX = transform.position.x;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // duraklatıldı

            if (IsDead)
            {
                UpdateDead(dt);
                return;
            }

            bool pressed = IsPressed(out Vector2 pointer, out bool hasPointer);
            bool down = pressed && !wasPressed;
            bool up = !pressed && wasPressed;
            wasPressed = pressed;

            if (down)
            {
                pressStart = pointer;
                pressConsumed = false;
            }
            if (pressed && hasPointer && !pressConsumed)
            {
                float dx = (pointer.x - pressStart.x) / Mathf.Max(1, Screen.width);
                if (Mathf.Abs(dx) >= swipeThreshold)
                {
                    pressConsumed = true;
                    ChangeLane(dx > 0f ? 1 : -1);
                    if (state == State.Charging) CancelCharge();
                }
            }
            ReadLaneKeys();

            Vector3 p = transform.position;
            float laneX = laneOriginX + lane * laneWidth;
            float laneK = 1f - Mathf.Exp(-laneChangeSharpness * dt);

            switch (state)
            {
                case State.Running:
                    if (down && !pressConsumed) BeginCharge();
                    break;
                case State.Charging:
                    Charge01 = Mathf.Clamp01((Time.time - chargeStart) / maxChargeTime);
                    if (up) Jump();
                    break;
                case State.Airborne:
                    verticalVelocity -= gravity * dt;
                    break;
                case State.ZipGrab:
                {
                    // halata doğru sıçrama: z ilerlerken yükseklik yumuşakça halat başına çıkar
                    p.z += runSpeed * dt;
                    float t = Mathf.InverseLerp(grabStartZ, zipA.z, p.z);
                    p.y = Mathf.Lerp(grabStartY, zipA.y, Mathf.SmoothStep(0f, 1f, t));
                    p.x = Mathf.Lerp(p.x, laneX, laneK);
                    transform.position = p;
                    if (t >= 1f) BeginSlide();
                    return;
                }
                case State.ZipSlide:
                {
                    p.z += ziplineSpeed * dt;
                    float t = Mathf.InverseLerp(zipA.z, zipB.z, p.z);
                    p.y = Mathf.Lerp(zipA.y, zipB.y, t) - zipSag * 4f * t * (1f - t);
                    p.x = Mathf.Lerp(p.x, laneX, laneK);
                    transform.position = p;
                    if (t >= 1f) ReleaseZipline();
                    return;
                }
            }

            float prevY = p.y;
            float speed = runSpeed * (state == State.Charging ? chargeSpeedMultiplier : 1f);
            p.z += speed * dt;
            p.x = Mathf.Lerp(p.x, laneX, laneK);
            p.y += verticalVelocity * dt;

            float surface = ObstacleField.SurfaceAt(p.x, p.z, groundHeight);
            if (state == State.Airborne)
            {
                if (verticalVelocity <= 0f && p.y <= surface)
                {
                    if (prevY < surface - stepUp)
                    {
                        // yanından/önünden girdi: çarptı
                        Crash(ref p, prevY);
                        return;
                    }
                    p.y = surface;
                    verticalVelocity = 0f;
                    state = State.Running;
                    Trigger(LandTrigger);
                }
                else if (surface > p.y + stepUp)
                {
                    Crash(ref p, prevY);   // yükselirken engelin yüzüne çarptı
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
                    // kenardan düştü (kamyon önü, çatı kenarı)
                    if (state == State.Charging) Charge01 = 0f;
                    state = State.Airborne;
                    verticalVelocity = 0f;
                    Trigger(FallTrigger);
                }
            }

            if (p.y < groundHeight - fallDeathDepth) BeginFallDeath();
            transform.position = p;
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
            if (transform.position.z >= zipA.z - 0.05f) { BeginSlide(); return; }   // tam halatın altında yakaladı
            state = State.ZipGrab;
            Charge01 = 0f;
            verticalVelocity = 0f;
            Trigger(JumpTrigger);
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
            Charge01 = 0f;
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

        private void Crash(ref Vector3 p, float prevY)
        {
            p.y = Mathf.Max(prevY, ObstacleField.SurfaceAt(p.x, p.z - 0.6f, groundHeight));
            p.z -= 0.15f;   // engelin içine girmesin
            transform.position = p;
            state = State.Dead;
            deathTime = Time.time;
            deathSpeed = runSpeed;
            verticalVelocity = 0f;
            Charge01 = 0f;
            if (ziplineSparks != null) ziplineSparks.Stop();
            Trigger(DeathTrigger);
        }

        private void BeginFallDeath()
        {
            if (IsDead) return;
            state = State.FallingDead;
            deathTime = Time.time;
            deathSpeed = runSpeed * 0.5f;
            Charge01 = 0f;
            Trigger(FallTrigger);
        }

        private void UpdateDead(float dt)
        {
            Vector3 p = transform.position;
            if (state == State.Dead)
            {
                // yumuşak duruş: hız sıfıra iner, ölme animasyonu oynar
                deathSpeed = Mathf.MoveTowards(deathSpeed, 0f, runSpeed / deathSlideTime * dt);
                p.z += deathSpeed * dt;
                float surface = ObstacleField.SurfaceAt(p.x, p.z + 0.4f, groundHeight);
                if (surface > p.y + stepUp) p.z -= deathSpeed * dt;   // engelin içine kaymasın
                verticalVelocity -= gravity * dt;
                p.y = Mathf.Max(p.y + verticalVelocity * dt, ObstacleField.SurfaceAt(p.x, p.z, groundHeight));
                if (p.y <= ObstacleField.SurfaceAt(p.x, p.z, groundHeight)) verticalVelocity = 0f;
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

        private void BeginSlide()
        {
            state = State.ZipSlide;
            Trigger(ZiplineTrigger);
            if (ziplineSparks != null) ziplineSparks.Play();
        }

        private void ReleaseZipline()
        {
            state = State.Airborne;
            verticalVelocity = -ziplineReleaseVelocity;
            Trigger(FallTrigger);
            if (ziplineSparks != null) ziplineSparks.Stop();
        }

        private void BeginCharge()
        {
            state = State.Charging;
            chargeStart = Time.time;
            Charge01 = 0f;
            Trigger(CrouchTrigger);
        }

        private void CancelCharge()
        {
            state = State.Running;
            Charge01 = 0f;
            Trigger(RunTrigger);
        }

        private void ChangeLane(int dir)
        {
            if (OnZipline || IsDead) return; // halattayken şerit sabit
            lane = Mathf.Clamp(lane + dir, -MaxLane, MaxLane); // en kenarda bir şey olmaz (çarpma yok)
        }

        private void ReadLaneKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame) ChangeLane(-1);
            if (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame) ChangeLane(1);
        }

        private void Jump()
        {
            float held = Mathf.Clamp(Time.time - chargeStart, minChargeTime, maxChargeTime);
            float t = Mathf.InverseLerp(minChargeTime, maxChargeTime, held);
            verticalVelocity = Mathf.Lerp(minJumpForce, maxJumpForce, t);
            state = State.Airborne;
            Charge01 = 0f;
            Trigger(JumpTrigger);
        }

        private void Trigger(int id)
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (int other in AllTriggers)
                if (other != id) animator.ResetTrigger(other);
            animator.SetTrigger(id);
        }

        /// <summary>Dokunma / fare / boşluk basılı mı? Arayüz butonlarına (duraklat vb.) basmak zıplatmaz.</summary>
        private static bool IsPressed(out Vector2 pointer, out bool hasPointer)
        {
            pointer = default;
            hasPointer = false;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (OverUI(touch.touchId.ReadValue())) return false;
                pointer = touch.position.ReadValue();
                hasPointer = true;
                return true;
            }
            if (Mouse.current != null && Mouse.current.leftButton.isPressed && !OverUI(-1))
            {
                pointer = Mouse.current.position.ReadValue();
                hasPointer = true;
                return true;
            }
            return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        }

        private static bool OverUI(int pointerId)
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject(pointerId);
        }
    }
}
