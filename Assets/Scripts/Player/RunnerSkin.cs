using UnityEngine;

namespace Jumpline.Player
{
    /// <summary>
    /// Koşucu modelinin malzeme slotlarını (M_Primary, M_Secondary...) kostüm renkleriyle boyar.
    /// Malzeme kopyalamaz; MaterialPropertyBlock kullanır.
    /// </summary>
    public class RunnerSkin : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        [SerializeField] private Renderer target;

        private MaterialPropertyBlock block;

        public void Apply(SkinPreset skin)
        {
            if (skin == null) return;
            if (target == null) target = GetComponentInChildren<Renderer>();
            block ??= new MaterialPropertyBlock();

            Material[] mats = target.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name;
                Color baseColor = Color.white, emission = Color.black;
                float metallic = skin.metallic, smooth = skin.smoothness;

                if (n.StartsWith("M_Primary")) baseColor = skin.primary;
                else if (n.StartsWith("M_Secondary")) { baseColor = skin.secondary; emission = skin.glow * skin.secondaryGlow; }
                else if (n.StartsWith("M_Armor")) { baseColor = skin.armor; metallic = Mathf.Max(metallic, 0.5f); smooth = 0.6f; }
                else if (n.StartsWith("M_Glove")) { baseColor = skin.glove; metallic = 0f; smooth = 0.3f; }
                else if (n.StartsWith("M_Glow")) { baseColor = skin.glow; emission = skin.glow * skin.glowIntensity; }
                else if (n.StartsWith("M_Visor")) { baseColor = skin.visor; smooth = 0.85f; if (skin.visorGlows) emission = skin.glow * skin.glowIntensity * 0.6f; }

                target.GetPropertyBlock(block, i);
                block.SetColor(BaseColor, baseColor);
                block.SetColor(EmissionColor, emission);
                block.SetFloat(Metallic, metallic);
                block.SetFloat(Smoothness, smooth);
                target.SetPropertyBlock(block, i);
            }
        }
    }
}
