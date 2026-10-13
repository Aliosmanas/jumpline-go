using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Basılıyken butonu hafifçe küçültür ve (varsa) üstündeki parıltıyı yumuşakça yakar.</summary>
    public class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.92f;
        [Tooltip("Bırakınca yaylanma sertliği / sönümü (tema 'Efsane': tombul düğme hissi).")]
        [SerializeField, Min(1f)] private float springStiffness = 420f;
        [SerializeField, Min(0f)] private float springDamping = 16f;
        private float scale = 1f, velocity;
        [SerializeField, Min(0f)] private float followSpeed = 22f;
        [Tooltip("Basınca parlayan katman (isteğe bağlı).")]
        [SerializeField] private Graphic flash;
        [SerializeField, Range(0f, 1f)] private float flashAlpha = 0.22f;

        private Selectable selectable;
        private bool pressed;

        private void Awake()
        {
            selectable = GetComponent<Selectable>();
            SetFlash(0f);
        }

        private void OnDisable()
        {
            pressed = false;
            scale = 1f;
            velocity = 0f;
            transform.localScale = Vector3.one;
            SetFlash(0f);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (selectable != null && !selectable.IsInteractable()) return;
            pressed = true;
            Jumpline.Audio.AudioManager.Play("click");
        }

        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        public void OnPointerExit(PointerEventData eventData) => pressed = false;

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float k = 1f - Mathf.Exp(-followSpeed * dt);
            if (pressed) { scale = Mathf.Lerp(scale, pressedScale, k); velocity = 0f; }
            else
            {
                // yay: bırakınca hafif taşıp yerine oturur
                velocity += ((1f - scale) * springStiffness - velocity * springDamping) * dt;
                scale += velocity * dt;
            }
            transform.localScale = Vector3.one * scale;
            if (flash != null) SetFlash(Mathf.Lerp(flash.color.a, pressed ? flashAlpha : 0f, k));
        }

        private void SetFlash(float alpha)
        {
            if (flash == null) return;
            Color c = flash.color;
            c.a = alpha;
            flash.color = c;
        }
    }
}
