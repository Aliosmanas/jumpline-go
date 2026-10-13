using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>
    /// Düğme yüzeyinde belli aralıklarla soldan sağa kayan çapraz ışık şeridi (tema "Efsane"). Yüzey Mask taşır;
    /// şerit onun çocuğudur. Zaman ölçeğinden bağımsız (duraklatma panelinde de parlar).
    /// </summary>
    public class UIShine : MonoBehaviour
    {
        [SerializeField] private RectTransform band;
        [SerializeField, Min(0.2f)] private float sweepTime = 0.7f;
        [SerializeField, Min(0.5f)] private float interval = 3.2f;
        [SerializeField, Min(0f)] private float startDelay;

        private RectTransform rt;
        private float t0;

        private void OnEnable()
        {
            rt = (RectTransform)transform;
            t0 = Time.unscaledTime + startDelay;
        }

        private void Update()
        {
            if (band == null) return;
            float w = rt.rect.width;
            float t = Time.unscaledTime - t0;
            if (t < 0f) { band.anchoredPosition = new Vector2(-w, 0f); return; }
            float cycle = t % (sweepTime + interval);
            float k = Mathf.Clamp01(cycle / sweepTime);
            k = k * k * (3f - 2f * k);
            band.anchoredPosition = new Vector2(Mathf.Lerp(-w * 0.75f, w * 0.75f, k), 0f);
        }
    }
}
