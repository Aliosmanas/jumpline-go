using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>
    /// Arayüz parçacığı: bir öğenin çevresinde parlayıp sönen, dönen küçük yıldızlar (oyna düğmesi, haritada sıradaki
    /// durak). Yıldızlar açılışta bir kez oluşturulur (çalışırken ayırma yok); her biri kendi fazında doğar-söner ve
    /// yeniden doğarken çevre üstünde yeni bir yere geçer. Zaman ölçeğinden bağımsız.
    /// </summary>
    public class UISparkles : MonoBehaviour
    {
        [SerializeField] private Sprite sprite;
        [SerializeField, Min(1)] private int count = 10;
        [SerializeField] private Color color = new Color(1f, 0.93f, 0.6f, 1f);
        [SerializeField, Min(4f)] private float size = 34f;
        [Tooltip("Yıldızlar öğenin kenarından bu kadar dışarı taşabilir (px).")]
        [SerializeField] private float spread = 40f;
        [SerializeField, Min(0.2f)] private float life = 1.4f;

        private RectTransform[] stars;
        private Image[] images;
        private float[] phase;

        private void Awake()
        {
            stars = new RectTransform[count];
            images = new Image[count];
            phase = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Sparkle", typeof(RectTransform), typeof(Image));
                var r = (RectTransform)go.transform;
                r.SetParent(transform, false);
                r.sizeDelta = new Vector2(size, size);
                var img = go.GetComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                img.color = color;
                stars[i] = r;
                images[i] = img;
                phase[i] = Random.value;
                Place(i);
            }
        }

        private void Place(int i)
        {
            var rect = ((RectTransform)transform).rect;
            float hw = rect.width * 0.5f + Random.Range(-spread * 0.3f, spread);
            float hh = rect.height * 0.5f + Random.Range(-spread * 0.3f, spread);
            // çevre üstünde rastgele nokta (kenarlara yakın)
            float a = Random.value * Mathf.PI * 2f;
            stars[i].anchoredPosition = new Vector2(Mathf.Cos(a) * hw, Mathf.Sin(a) * hh);
            stars[i].localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 90f));
        }

        private void Update()
        {
            if (stars == null) return;
            float t = Time.unscaledTime / life;
            for (int i = 0; i < stars.Length; i++)
            {
                float p = t + phase[i];
                float k = p - Mathf.Floor(p);
                if (k < Time.unscaledDeltaTime / life + 0.001f) Place(i);   // yeni döngü: yeni yer
                float s = Mathf.Sin(k * Mathf.PI);
                stars[i].localScale = Vector3.one * s;
                stars[i].Rotate(0f, 0f, 90f * Time.unscaledDeltaTime);
                Color c = color;
                c.a = color.a * s;
                images[i].color = c;
            }
        }
    }
}
