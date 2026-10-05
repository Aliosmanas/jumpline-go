using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Jumpline.Player
{
    /// <summary>
    /// Koşucu: sürekli ileri (+Z) koşar. Basılı tut = zıplama şarjı, bırak = zıpla; basma süresi zıplama gücünü belirler.
    /// Girdi: dokunma (Android), fare sol tuş ve Boşluk (editör).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        private enum State { Running, Charging, Airborne }

        [Header("Koşu")]
        [SerializeField, Min(0f)] private float runSpeed = 7f;

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

            bool pressed = IsPressed();
            bool down = pressed && !wasPressed;
            bool up = !pressed && wasPressed;
            wasPressed = pressed;

            switch (state)
            {
                case State.Running:
                    if (down) BeginCharge();
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
            animator.ResetTrigger(RunTrigger);
            animator.SetTrigger(id);
        }

        /// <summary>Dokunma / fare / boşluk basılı mı? Arayüz butonlarına (duraklat vb.) basmak zıplatmaz.</summary>
        private static bool IsPressed()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) return true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
                return !OverUI(Touchscreen.current.primaryTouch.touchId.ReadValue());
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return !OverUI(-1);
            return false;
        }

        private static bool OverUI(int pointerId)
        {
            var es = EventSystem.current;
            return es != null && es.IsPointerOverGameObject(pointerId);
        }
    }
}
