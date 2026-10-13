using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Kayan topuzlu aç/kapa anahtarı. Değer Unity Toggle'da tutulur.</summary>
    [RequireComponent(typeof(Toggle))]
    public class SwitchToggle : MonoBehaviour
    {
        [SerializeField] private RectTransform knob;
        [SerializeField] private float offX = -32f;
        [SerializeField] private float onX = 32f;
        [SerializeField, Min(0f)] private float followSpeed = 18f;
        [Tooltip("Açıkken görünen iz (yeşil); kapalıyken alttaki kırmızı iz görünür. Boşsa yalnız topuz kayar.")]
        [SerializeField] private GameObject onTrack;

        private Toggle toggle;

        public Toggle Toggle => toggle != null ? toggle : toggle = GetComponent<Toggle>();

        private void OnEnable() => Snap();

        public void Snap()
        {
            if (knob == null) return;
            knob.anchoredPosition = new Vector2(Toggle.isOn ? onX : offX, knob.anchoredPosition.y);
            if (onTrack != null) onTrack.SetActive(Toggle.isOn);
        }

        private void Update()
        {
            if (knob == null) return;
            float target = Toggle.isOn ? onX : offX;
            if (onTrack != null && onTrack.activeSelf != Toggle.isOn) onTrack.SetActive(Toggle.isOn);
            float k = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);
            Vector2 p = knob.anchoredPosition;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(p.x, target, k), p.y);
        }
    }
}
