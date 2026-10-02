using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Graphic'in köşe renklerini iki renk arasında geçişle boyar (Image.color ile çarpılır).</summary>
    [RequireComponent(typeof(Graphic))]
    public class UIGradient : BaseMeshEffect
    {
        [SerializeField] private Color start = Color.white;
        [SerializeField] private Color end = Color.white;
        [Tooltip("Kapalı: alttan (start) üste (end). Açık: soldan (start) sağa (end).")]
        [SerializeField] private bool horizontal;

        public void SetColors(Color startColor, Color endColor, bool isHorizontal = false)
        {
            start = startColor;
            end = endColor;
            horizontal = isHorizontal;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            Rect rect = graphic.rectTransform.rect;
            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float t = horizontal
                    ? Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x)
                    : Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
                vertex.color = (Color)vertex.color * Color.Lerp(start, end, t);
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
