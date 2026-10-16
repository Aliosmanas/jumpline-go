using System;
using UnityEngine;

namespace Jumpline.Level.Designed
{
    /// <summary>
    /// Tasarım haritası (Blender/scripts/map_designs.py → Assets/Data/Designed/Harita_NN.json → prefab). Kullanıcının
    /// Blender'da yaptığı referans harita tarzında; engeller, kenar dekoru ve zemin prefab'ın içinde. Geçilebilirliği Blender'da
    /// simülasyonla kanıtlandı. Kök z = 0 haritanın başı, +Z koşu yönü, x = 0 yol ortası.
    /// </summary>
    public class DesignedMap : MonoBehaviour
    {
        [Serializable]
        public struct PortalSpot
        {
            [Range(-1, 1)] public int lane;
            [Tooltip("Haritanın başından (m).")] public float z;
            [Tooltip("Portalın tabanı (0 yer, > 0 araç tavanı).")] public float y;
        }

        [SerializeField, Min(10f)] private float length = 100f;
        [Tooltip("mahalle / sur / gecis (bölgeler sırayla).")]
        [SerializeField] private string[] themes = Array.Empty<string>();
        [SerializeField] private PortalSpot[] portals = Array.Empty<PortalSpot>();
        [SerializeField] private Obstacle[] obstacles = Array.Empty<Obstacle>();

        public float Length => length;
        public string[] Themes => themes;
        public PortalSpot[] Portals => portals;
        public Obstacle[] Obstacles => obstacles;
        /// <summary>Yalnız sur üstü (deniz) bölgesinden oluşuyor mu.</summary>
        public bool IsSeaOnly => themes.Length > 0 && Array.TrueForAll(themes, t => t == "sur" || t == "gecis");

        public void Configure(float len, string[] th, PortalSpot[] ps, Obstacle[] obs)
        {
            length = len; themes = th; portals = ps; obstacles = obs;
        }

        /// <summary>Havuzdan yeniden kullanılırken: elmasla devamda kaldırılan engeller geri gelir.</summary>
        public void ResetObstacles()
        {
            foreach (var o in obstacles)
                if (o != null && !o.gameObject.activeSelf) o.gameObject.SetActive(true);
        }

        /// <summary>Dünya z aralığındaki engelleri kaldırır (elmasla devam, portal önü).</summary>
        public void ClearRange(float z0, float z1)
        {
            foreach (var o in obstacles)
            {
                if (o == null || !o.gameObject.activeSelf) continue;
                if (o.MaxZ < z0 || o.MinZ > z1) continue;
                o.gameObject.SetActive(false);
            }
        }
    }
}
