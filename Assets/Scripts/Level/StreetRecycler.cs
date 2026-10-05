using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Sonsuz yol: koşucunun gerisinde kalan modülü en öne taşır. Işınlanma gibi büyük sıçramalarda modüller
    /// koşucuya yetişene kadar art arda taşınır (dünya kapalıyken geride kalmış olabilir).
    /// </summary>
    public class StreetRecycler : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField, Min(1f)] private float segmentLength = 60f;
        [Tooltip("Modül, bu kadar geride kalınca öne taşınır.")]
        [SerializeField, Min(0f)] private float behindMargin = 15f;

        private void OnEnable() => Recycle();

        private void LateUpdate() => Recycle();

        /// <summary>Gerideki modülleri öne alır (ayırma yapmaz: her karede çalışır).</summary>
        public void Recycle()
        {
            if (player == null) return;
            int count = transform.childCount;
            if (count == 0) return;
            float limit = player.position.z - behindMargin - segmentLength;
            for (int guard = 0; guard < 4096; guard++)
            {
                Transform back = null, front = null;
                for (int i = 0; i < count; i++)
                {
                    Transform t = transform.GetChild(i);
                    if (back == null || t.position.z < back.position.z) back = t;
                    if (front == null || t.position.z > front.position.z) front = t;
                }
                if (back.position.z >= limit) return;
                Vector3 p = back.position;
                back.position = new Vector3(p.x, p.y, front.position.z + segmentLength);
            }
        }
    }
}
