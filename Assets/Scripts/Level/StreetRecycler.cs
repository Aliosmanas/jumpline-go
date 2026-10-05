using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>Sonsuz sokak: koşucunun gerisinde kalan modülü en öne taşır.</summary>
    public class StreetRecycler : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField, Min(1f)] private float segmentLength = 60f;
        [Tooltip("Modül, bu kadar geride kalınca öne taşınır.")]
        [SerializeField, Min(0f)] private float behindMargin = 15f;

        private void LateUpdate()
        {
            if (player == null || transform.childCount == 0) return;
            float maxZ = float.MinValue;
            foreach (Transform t in transform) maxZ = Mathf.Max(maxZ, t.position.z);
            foreach (Transform t in transform)
            {
                if (t.position.z + segmentLength < player.position.z - behindMargin)
                {
                    t.position = new Vector3(t.position.x, t.position.y, maxZ + segmentLength);
                    maxZ = t.position.z;
                }
            }
        }
    }
}
