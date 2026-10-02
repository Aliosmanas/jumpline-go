using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Graphic'i yatay eğer (italik plaka / paralelkenar görünümü).</summary>
    [RequireComponent(typeof(Graphic))]
    public class UISkew : BaseMeshEffect
    {
        [SerializeField, Range(-45f, 45f)] private float angle = -10f;

        public void SetAngle(float degrees)
        {
            angle = degrees;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            float centerY = graphic.rectTransform.rect.center.y;
            float shear = -Mathf.Tan(angle * Mathf.Deg2Rad);
            UIVertex vertex = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                vertex.position.x += (vertex.position.y - centerY) * shear;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
