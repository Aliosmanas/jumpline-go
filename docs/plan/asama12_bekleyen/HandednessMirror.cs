using Jumpline.Save;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Solak modu: açıksa bu öğe ekranın öbür yanına aynalanır (atma düğmesi sola, duraklat sola).</summary>
    [RequireComponent(typeof(RectTransform))]
    public class HandednessMirror : MonoBehaviour
    {
        private RectTransform rt;
        private Vector2 original;
        private bool captured;

        private void OnEnable()
        {
            if (!captured)
            {
                rt = (RectTransform)transform;
                original = rt.anchoredPosition;
                captured = true;
            }
            Apply();
        }

        public void Apply()
        {
            if (rt == null) return;
            rt.anchoredPosition = new Vector2(SaveManager.LeftHanded ? -original.x : original.x, original.y);
        }
    }
}
