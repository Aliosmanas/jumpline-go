using Jumpline.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>
    /// Etkin güçler (sol alt): her güç için ikon + kalan süre çubuğu. Güç başlayınca yuva zıplayarak belirir,
    /// son 2 saniyede yanıp söner, bitince kaybolur. Yuvalar sırayla yukarı dizilir.
    /// </summary>
    public class PowerUpHud : MonoBehaviour
    {
        [System.Serializable]
        public struct Slot
        {
            public PowerUpType type;
            public CanvasGroup group;
            public RectTransform fill;
            public float fullWidth;
        }

        [SerializeField] private PowerUps powerUps;
        [SerializeField] private Slot[] slots;
        [SerializeField, Min(10f)] private float rowHeight = 112f;
        [SerializeField, Min(0.1f)] private float blinkFrom = 2f;

        private readonly float[] shownAt = new float[8];

        private void Start()
        {
            if (powerUps == null) { enabled = false; return; }
            powerUps.Activated += t =>
            {
                for (int i = 0; i < slots.Length; i++) if (slots[i].type == t) shownAt[i] = Time.unscaledTime;
            };
            foreach (Slot s in slots) if (s.group != null) s.group.alpha = 0f;
        }

        private void Update()
        {
            int row = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot s = slots[i];
                if (s.group == null) continue;
                bool on = powerUps.IsActive(s.type);
                RectTransform rt = (RectTransform)s.group.transform;
                if (!on)
                {
                    s.group.alpha = Mathf.MoveTowards(s.group.alpha, 0f, Time.unscaledDeltaTime * 6f);
                    continue;
                }
                float k = powerUps.Remaining01(s.type);
                float left = k * powerUps.DurationOf(s.type);
                float blink = left < blinkFrom ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f)) : 1f;
                s.group.alpha = Mathf.MoveTowards(s.group.alpha, blink, Time.unscaledDeltaTime * 8f);
                if (s.fill != null) s.fill.sizeDelta = new Vector2(Mathf.Max(6f, s.fullWidth * k), s.fill.sizeDelta.y);
                float u = (Time.unscaledTime - shownAt[i]) / 0.3f;
                rt.localScale = Vector3.one * (u < 1f ? Mathf.Lerp(0.6f, 1f, u) + 0.2f * Mathf.Sin(u * Mathf.PI) : 1f);
                Vector2 a = rt.anchoredPosition;
                a.y = Mathf.Lerp(a.y, row * rowHeight, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                rt.anchoredPosition = a;
                row++;
            }
        }
    }
}
