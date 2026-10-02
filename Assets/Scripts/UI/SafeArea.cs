using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Çocuklarını telefonun güvenli alanına (çentik / yuvarlak köşe dışı) sığdırır.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform rect;
        private Rect applied;
        private Vector2Int screenSize;

        private void Awake()
        {
            rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != applied || screenSize.x != Screen.width || screenSize.y != Screen.height) Apply();
        }

        private void Apply()
        {
            applied = Screen.safeArea;
            screenSize = new Vector2Int(Screen.width, Screen.height);
            if (screenSize.x <= 0 || screenSize.y <= 0) return;

            rect.anchorMin = new Vector2(applied.xMin / screenSize.x, applied.yMin / screenSize.y);
            rect.anchorMax = new Vector2(applied.xMax / screenSize.x, applied.yMax / screenSize.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
