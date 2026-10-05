using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Çatı modülündeki zipline (Blender/scripts/build_rooftops.py ZIP_* ile aynı değerler). Koşucu halat başına
    /// yaklaşınca otomatik tutunur; şeridi neyse o şeridin halatından kayar. Modül öne taşınınca yeniden kurulur.
    /// </summary>
    public class Zipline : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [Header("Halat (modül içi, çatı yüzeyi h=0)")]
        [SerializeField] private float startZ = 43f;
        [SerializeField] private float endZ = 66f;
        [SerializeField] private float startHeight = 4.65f;
        [SerializeField] private float endHeight = 3f;
        [SerializeField, Min(0f)] private float sag = 0.25f;
        [Header("Tutunma")]
        [Tooltip("Ayaktan yukarı uzanmış ele mesafe (halat ile kök arası).")]
        [SerializeField, Min(0f)] private float handHeight = 2.25f;
        [Tooltip("Halat başından bu kadar önce sıçrar.")]
        [SerializeField, Min(0.1f)] private float grabLead = 3f;
        [Tooltip("Koşucu bu modülün dünyasında mı (yatay/dikey tolerans).")]
        [SerializeField, Min(0f)] private float worldTolerance = 8f;

        private float usedAtZ = float.NaN;

        public float StartZ => startZ;
        public float EndZ => endZ;

        private void Update()
        {
            if (player == null || player.OnZipline) return;
            Vector3 o = transform.position;
            Vector3 p = player.transform.position;
            if (Mathf.Abs(p.x - o.x) > worldTolerance || Mathf.Abs(p.y - o.y) > worldTolerance) return;
            float zA = o.z + startZ;
            if (usedAtZ == zA) return;   // bu konumda zaten kullanıldı (modül taşınınca zA değişir)
            if (p.z < zA - grabLead || p.z > zA) return;
            usedAtZ = zA;
            float x = player.LaneOriginX;
            player.BeginZipline(new Vector3(x, o.y + startHeight, zA), new Vector3(x, o.y + endHeight, o.z + endZ), sag, handHeight);
        }
    }
}
