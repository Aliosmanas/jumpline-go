using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Basılıyken butonu hafifçe küçültür ve (varsa) üstündeki parıltıyı yumuşakça yakar.</summary>
    public class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.95f;
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
            float k = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.one * Mathf.Lerp(transform.localScale.x, pressed ? pressedScale : 1f, k);
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
