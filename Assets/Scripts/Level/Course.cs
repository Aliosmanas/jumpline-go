using Jumpline.Level.Chunks;
using Jumpline.Level.Designed;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Parkur üreticilerine (çatı chunk'ları + sokak tasarım haritaları) tek kapı: elmasla devamda / portal önünde engel
    /// temizleme, canavar kovalamasında portalı yok sayma, testlerde parkuru boşaltma.
    /// </summary>
    public static class Course
    {
        /// <summary>Etkin üreticilerde dünya z aralığındaki engelleri kaldırır.</summary>
        public static void ClearRange(float z0, float z1)
        {
            foreach (var sp in Object.FindObjectsByType<ChunkSpawner>(FindObjectsSortMode.None))
                if (sp.isActiveAndEnabled) sp.ClearRange(z0, z1);
            foreach (var dc in Object.FindObjectsByType<DesignedMapChain>(FindObjectsSortMode.None))
                if (dc.isActiveAndEnabled) dc.ClearRange(z0, z1);
        }

        /// <summary>Canavar kovalaması: parkurun kendi portalı konmaz.</summary>
        public static void IgnorePortal(bool ignore)
        {
            foreach (var sp in Object.FindObjectsByType<ChunkSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sp.IgnoreCoursePortal = ignore;
            foreach (var dc in Object.FindObjectsByType<DesignedMapChain>(FindObjectsInactive.Include, FindObjectsSortMode.None)) dc.IgnoreCoursePortal = ignore;
        }

        /// <summary>Testler / önizleme: bütün engelleri kaldır, isteğe bağlı üreticileri durdur (yalın koşu).</summary>
        public static void ClearAll(bool stopSpawning)
        {
            foreach (var sp in Object.FindObjectsByType<ChunkSpawner>(FindObjectsSortMode.None))
            {
                sp.ClearRange(-1e7f, 1e7f);
                if (stopSpawning) sp.enabled = false;
            }
            foreach (var dc in Object.FindObjectsByType<DesignedMapChain>(FindObjectsSortMode.None))
            {
                dc.ClearRange(-1e7f, 1e7f);
                if (stopSpawning) dc.StopObstacles();
            }
        }
    }
}
