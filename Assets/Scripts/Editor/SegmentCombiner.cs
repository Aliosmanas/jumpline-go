using UnityEditor;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Performans (aşama 14): modüllere <see cref="Jumpline.Level.CombineOnStart"/> ekler ve kaynak FBX'leri okunabilir
    /// yapar (çalışırken birleştirme Read/Write ister). Birleştirme sahne dosyasında yapılmaz (dosya şişiyordu).
    /// </summary>
    public static class SegmentCombiner
    {
        public static void Combine(Transform segment)
        {
            if (segment.GetComponent<Jumpline.Level.CombineOnStart>() == null)
                segment.gameObject.AddComponent<Jumpline.Level.CombineOnStart>();
        }

        public static void EnsureReadable(params string[] modelPaths)
        {
            foreach (string path in modelPaths)
            {
                if (AssetImporter.GetAtPath(path) is not ModelImporter imp || imp.isReadable) continue;
                imp.isReadable = true;
                imp.SaveAndReimport();
            }
        }
    }
}
