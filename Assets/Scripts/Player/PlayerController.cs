using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Jumpline.Player
{
    /// <summary>
    /// Koşucu: sürekli ileri (+Z) koşar. Basılı tut = zıplama şarjı, bırak = zıpla; basma süresi zıplama gücünü belirler.
    /// 3 şerit (Subway Surfers gibi): sağa/sola kaydır ya da A/D, ←/→ ile şerit değiştirir; havadayken de olur.
    /// Kaydırma o basışın şarjını iptal eder (zıplatmaz). Girdi: dokunma (Android), fare sol tuş + Boşluk (editör).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        private enum State { Running, Charging, Airborne }

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
        [SerializeField] private float groundHeight = 0f;

        [Header("Görsel")]
        [SerializeField] private Animator animator;

        private State state = State.Running;
        private float chargeStart;
        private float verticalVelocity;
        private bool wasPressed;
        private int lane;                 // -1 sol, 0 orta, +1 sağ
        private Vector2 pressStart;
        private bool pressConsumed;       // bu basışta kaydırma yapıldı → bırakınca zıplama yok

        private const int LaneCount = 3;
        private const int MaxLane = LaneCount / 2;

        public float Charge01 { get; private set; }
        public float RunSpeed => runSpeed;

        private static readonly int RunTrigger = Animator.StringToHash("Run");
        private static readonly int CrouchTrigger = Animator.StringToHash("Crouch");
        private static readonly int JumpTrigger = Animator.StringToHash("Jump");
        private static readonly int LandTrigger = Animator.StringToHash("Land");

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // duraklatıldı

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
            }

            float speed = runSpeed * (state == State.Charging ? chargeSpeedMultiplier : 1f);
            Vector3 p = transform.position;
            p.z += speed * dt;
            p.x = Mathf.Lerp(p.x, lane * laneWidth, 1f - Mathf.Exp(-laneChangeSharpness * dt));
            p.y += verticalVelocity * dt;
            if (state == State.Airborne && p.y <= groundHeight && verticalVelocity < 0f)
            {
                p.y = groundHeight;
                verticalVelocity = 0f;
                state = State.Running;
                Trigger(LandTrigger);
            }
            transform.position = p;
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
            if (id != RunTrigger) animator.ResetTrigger(RunTrigger);
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
