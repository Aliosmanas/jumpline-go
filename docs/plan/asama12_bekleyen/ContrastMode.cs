using Jumpline.Save;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Jumpline.UI
{
    /// <summary>
    /// Kontrastlı mod (oyun sahnesi): açıksa sahne rengi daha kontrastlı/doygun (son işlem açıksa) ve göstergedeki
    /// tüm yazılara kalın siyah kontur eklenir — engeller ve sayılar parlak ışıkta da seçilir.
    /// </summary>
    public class ContrastMode : MonoBehaviour
    {
        [SerializeField] private Volume volume;
        [SerializeField, Range(0f, 100f)] private float contrast = 30f;
        [SerializeField, Range(0f, 100f)] private float saturation = 20f;
        [SerializeField, Range(0f, 1f)] private float textOutline = 0.22f;

        private void Start()
        {
            if (!SaveManager.HighContrast) return;
            if (volume != null)
            {
                VolumeProfile profile = volume.profile;   // örnek kopya: kaynak dosya değişmez
                if (!profile.TryGet(out ColorAdjustments color)) color = profile.Add<ColorAdjustments>();
                color.contrast.Override(contrast);
                color.saturation.Override(saturation);
            }
            foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
            {
                t.fontMaterial.EnableKeyword("OUTLINE_ON");
                t.outlineWidth = Mathf.Max(t.outlineWidth, textOutline);
                t.outlineColor = new Color32(0, 0, 0, 255);
            }
        }
    }
}
