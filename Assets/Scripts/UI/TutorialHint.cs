using Jumpline.Player;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Koşunun başında kontrolleri hatırlatır (kaydırma yönleri), birkaç saniye sonra söner.</summary>
    public class TutorialHint : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0.5f)] private float showTime = 4f;
        [SerializeField, Min(0.1f)] private float fadeTime = 0.8f;

        private float start;

        private void OnEnable() => start = Time.unscaledTime;

        private void Update()
        {
            if (label == null) return;
            float t = Time.unscaledTime - start;
            label.alpha = t < showTime ? 1f : 1f - Mathf.Clamp01((t - showTime) / fadeTime);
            if (t > showTime + fadeTime) enabled = false;
        }
    }
}
