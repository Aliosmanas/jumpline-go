using System.Collections;
using Jumpline.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jumpline.UI
{
    /// <summary>
    /// Sahne geçişlerinde yükleme ekranı (Resources/LoadingScreen prefab'ı; sahneler arası yaşar):
    /// karartma → sahne arka planda yüklenir (ilerleme çubuğu, Diyarbakır deseni, rastgele ipucu) → kısa bekleme → aydınlanma.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform barFill;
        [SerializeField, Min(10f)] private float barWidth = 600f;
        [SerializeField] private TMP_Text tipLabel;
        [SerializeField, Min(0.05f)] private float fadeTime = 0.25f;
        [Tooltip("Çok hızlı yüklemelerde ipucu okunabilsin diye en az bu kadar görünür (sn).")]
        [SerializeField, Min(0f)] private float minShowTime = 0.9f;
        [SerializeField] private string[] tips =
        {
            "Throw the melon into the monster's lane!",
            "After its second heart, Tepegoz dodges. Throw from up close!",
            "The magnet pulls nearby coins to you.",
            "A shield absorbs one crash.",
            "With the speed boost you smash through obstacles.",
            "Coins count double in Quick Race.",
            "Swipe down in the air to drop fast.",
            "Upgrade power-ups to make them last longer.",
        };

        private static LoadingScreen instance;
        private bool busy;

        public static void Load(string scene)
        {
            if (instance == null)
            {
                var prefab = Resources.Load<LoadingScreen>("LoadingScreen");
                if (prefab == null) { SceneManager.LoadScene(scene); return; }
                instance = Instantiate(prefab);
                DontDestroyOnLoad(instance.gameObject);
            }
            instance.StartCoroutine(instance.Run(scene));
        }

        private IEnumerator Run(string scene)
        {
            if (busy) yield break;
            busy = true;
            gameObject.SetActive(true);
            if (tipLabel != null && tips.Length > 0) tipLabel.text = Localization.Get(tips[Random.Range(0, tips.Length)]);
            SetBar(0f);
            group.blocksRaycasts = true;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime) { group.alpha = t / fadeTime; yield return null; }
            group.alpha = 1f;
            float shown = Time.unscaledTime;
            AsyncOperation op = SceneManager.LoadSceneAsync(scene);
            op.allowSceneActivation = false;
            while (op.progress < 0.9f || Time.unscaledTime - shown < minShowTime)
            {
                SetBar(Mathf.Min(op.progress / 0.9f, (Time.unscaledTime - shown) / minShowTime));
                yield return null;
            }
            SetBar(1f);
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;
            yield return null;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / fadeTime; yield return null; }
            group.alpha = 0f;
            group.blocksRaycasts = false;
            busy = false;
        }

        private void SetBar(float k)
        {
            if (barFill != null) barFill.sizeDelta = new Vector2(Mathf.Max(8f, barWidth * Mathf.Clamp01(k)), barFill.sizeDelta.y);
        }
    }
}
