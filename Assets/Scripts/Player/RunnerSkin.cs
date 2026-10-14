using Jumpline.Save;
using UnityEngine;

namespace Jumpline.Player
{
    /// <summary>
    /// Koşucu modelinin malzeme slotlarını (M_Primary, M_Secondary...) kostüm renkleriyle boyar ve
    /// takılı / çıkarılmış ekipmanı (kask, eldiven, bot) gösterir. Malzeme kopyalamaz; MaterialPropertyBlock kullanır.
    /// </summary>
    public class RunnerSkin : MonoBehaviour
    {
        public const string Helmet = "helmet";
        public const string Gloves = "gloves";
        public const string Boots = "boots";

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        [Tooltip("Ekipman çeşidi renkleri için (stealth / velocity / titanium).")]
        [SerializeField] private SkinLibrary library;

        private MaterialPropertyBlock block;
        private Renderer[] renderers;
        private SkinPreset currentSkin;

        private Renderer[] Renderers => renderers ??= GetComponentsInChildren<Renderer>(true);

        private void OnEnable()
        {
            SaveManager.GearChanged += ApplyGear;
            ApplyGear();
        }

        private void OnDisable() => SaveManager.GearChanged -= ApplyGear;

        /// <summary>Ekipman nesneleri: Runner_Helmet / Runner_Gloves / Runner_Boots; bot yoksa Runner_Feet görünür.</summary>
        public void ApplyGear()
        {
            foreach (Renderer r in Renderers)
            {
                switch (r.name)
                {
                    case "Runner_Helmet": r.enabled = SaveManager.IsGearEquipped(Helmet); break;
                    case "Runner_Gloves": r.enabled = SaveManager.IsGearEquipped(Gloves); break;
                    case "Runner_Boots": r.enabled = SaveManager.IsGearEquipped(Boots); break;
                    case "Runner_Feet": r.enabled = !SaveManager.IsGearEquipped(Boots); break;
                }
            }
            if (currentSkin != null) Apply(currentSkin);
        }

        private static string SlotOf(string rendererName) => rendererName switch
        {
            "Runner_Helmet" => Helmet,
            "Runner_Gloves" => Gloves,
            "Runner_Boots" => Boots,
            _ => null,
        };

        private const string BodyPrefix = "Runner_Body_";
        private const string DefaultBody = "rookie";

        /// <summary>
        /// Kostüm: Meshy modelleri ayrı gövde (Runner_Body_&lt;id&gt;); seçilen açılır, diğerleri kapanır.
        /// Kendi gövdesi olmayan kostüm (glitch, frost) varsayılan gövdeyi kostüm rengiyle hafifçe tonlar.
        /// Eski tek-model koşucu (M_Primary… slotları) için renk boyama yolu korunur.
        /// </summary>
        public void Apply(SkinPreset skin)
        {
            if (skin == null) return;
            currentSkin = skin;
            block ??= new MaterialPropertyBlock();
            bool hasBodies = false, hasOwn = false;
            foreach (Renderer r in Renderers)
            {
                if (!r.name.StartsWith(BodyPrefix)) continue;
                hasBodies = true;
                if (r.name == BodyPrefix + skin.id) hasOwn = true;
            }
            if (hasBodies)
            {
                string show = hasOwn ? skin.id : DefaultBody;
                foreach (Renderer r in Renderers)
                {
                    if (!r.name.StartsWith(BodyPrefix)) continue;
                    bool on = r.name == BodyPrefix + show;
                    r.enabled = on;
                    if (!on) continue;
                    r.GetPropertyBlock(block);
                    block.Clear();
                    if (!hasOwn) block.SetColor(BaseColor, Color.Lerp(Color.white, skin.primary, tintStrength));
                    r.SetPropertyBlock(block);
                }
                return;
            }
            foreach (Renderer target in Renderers)
            {
                string slot = SlotOf(target.name);
                SkinPreset gear = slot != null && library != null ? library.FindGear(SaveManager.GetGearVariant(slot)) : null;
                Apply(gear ?? skin, target);
            }
        }

        [Tooltip("Kendi gövdesi olmayan kostümde varsayılan gövdeye karışan renk oranı.")]
        [SerializeField, Range(0f, 1f)] private float tintStrength = 0.45f;

        private void Apply(SkinPreset skin, Renderer target)
        {
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
                else if (n.StartsWith("M_GlowAlt")) { baseColor = skin.primary; emission = Color.Lerp(skin.primary, Color.white, 0.25f) * skin.glowIntensity * 0.7f; }
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
