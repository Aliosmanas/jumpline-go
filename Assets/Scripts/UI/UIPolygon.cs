using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>
    /// Noktaları RectTransform içinde normalize (0..1, sol-üst köken) verilen çokgen.
    /// Dolgu ilk noktadan yelpaze olarak üçgenlenir (ilk noktadan tüm köşeler görünmeli).
    /// Çizgi modunda noktalar açık bir çizgi olarak çizilir.
    /// </summary>
    public class UIPolygon : MaskableGraphic
    {
        [SerializeField] private Vector2[] points = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        [SerializeField] private bool stroke;
        [SerializeField, Min(0.1f)] private float thickness = 3f;

        public void Configure(Vector2[] normalizedPoints, bool asStroke = false, float strokeThickness = 3f)
        {
            points = normalizedPoints;
            stroke = asStroke;
            thickness = strokeThickness;
            SetVerticesDirty();
        }

        private Vector2 ToLocal(Vector2 p)
        {
            Rect r = rectTransform.rect;
            return new Vector2(r.xMin + p.x * r.width, r.yMax - p.y * r.height);
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points == null || points.Length < 2) return;
            Color32 c = color;

            if (!stroke)
            {
                for (int i = 0; i < points.Length; i++) vh.AddVert(ToLocal(points[i]), c, Vector4.zero);
                for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(0, i, i + 1);
                return;
            }

            float half = thickness * 0.5f;
            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector2 a = ToLocal(points[i]), b = ToLocal(points[i + 1]);
                Vector2 dir = (b - a).normalized;
                Vector2 n = new Vector2(-dir.y, dir.x) * half;
                Vector2 ext = dir * half; // birleşim yerlerinde boşluk kalmasın
                int start = vh.currentVertCount;
                vh.AddVert(a - ext - n, c, Vector4.zero);
                vh.AddVert(a - ext + n, c, Vector4.zero);
                vh.AddVert(b + ext + n, c, Vector4.zero);
                vh.AddVert(b + ext - n, c, Vector4.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
