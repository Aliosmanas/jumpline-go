using System.Collections;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Ekranın altında kısa süre görünüp kaybolan bilgi mesajı.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class Toast : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(0.01f)] private float fadeTime = 0.2f;
        [SerializeField, Min(0f)] private float holdTime = 1.6f;

        private CanvasGroup group;
        private Coroutine routine;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }

        public void Show(string message)
        {
            label.text = message;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return Fade(1f);
            yield return new WaitForSecondsRealtime(holdTime);
            yield return Fade(0f);
            routine = null;
        }

        private IEnumerator Fade(float to)
        {
            float from = group.alpha;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / fadeTime);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
