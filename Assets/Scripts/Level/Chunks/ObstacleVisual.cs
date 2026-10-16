using System;
using UnityEngine;

namespace Jumpline.Level.Chunks
{
    /// <summary>
    /// Engel prefab'ının "Visual" çocuğu: renk/doku düzeltmesi meshin içine değil buraya yapılır. Malzeme yuvası adına
    /// göre değişim (ör. taksinin T_Yellow'u → beyaz) editörde prefab kurulurken uygulanır ve prefab'a kaydolur;
    /// meshe ve kaynak FBX'e dokunulmaz. Renk kodu (<see cref="codeColor"/>) işaret ve kenar vurgusu içindir.
    /// </summary>
    public class ObstacleVisual : MonoBehaviour
    {
        [Serializable]
        public struct Override
        {
            [Tooltip("Değişecek malzemenin adı (FBX'teki yuva).")] public string slot;
            public Material material;
        }

        [SerializeField] private Override[] overrides = Array.Empty<Override>();
        [SerializeField] private Color codeColor = Color.white;

        public Color CodeColor => codeColor;
        public Override[] Overrides => overrides;

        /// <summary>Editör: değişimleri kaydet ve uygula (sharedMaterials; prefab'a yazılır).</summary>
        public void Configure(Override[] list, Color code)
        {
            overrides = list ?? Array.Empty<Override>();
            codeColor = code;
            Apply();
        }

        public void Apply()
        {
            if (overrides.Length == 0) return;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                    foreach (var o in overrides)
                        if (mats[i] != null && o.material != null && mats[i].name == o.slot) { mats[i] = o.material; changed = true; }
                if (changed) r.sharedMaterials = mats;
            }
        }
    }
}
