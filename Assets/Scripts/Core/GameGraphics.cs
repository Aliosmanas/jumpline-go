using Jumpline.Save;
using UnityEngine;

namespace Jumpline.Core
{
    /// <summary>
    /// Ayarlardaki Low/Med/High/Ultra seçimini uygular: Unity kalite seviyesi + çözünürlük ölçeği
    /// + kare hızı hedefi.
    /// </summary>
    public static class GameGraphics
    {
        // Low, Med, High, Ultra
        private static readonly int[] QualityLevel = { 0, 0, 1, 1 };   // ProjectSettings: 0 = Mobile, 1 = PC
        private static readonly int[] TargetFps = { 30, 60, 60, 60 };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => Apply(SaveManager.GraphicsQuality);

        public static void Apply(int quality)
        {
            quality = Mathf.Clamp(quality, 0, SaveManager.GraphicsQualityCount - 1);
            int level = Mathf.Min(QualityLevel[quality], QualitySettings.names.Length - 1);
            // telefonda masaüstü (PC) URP ayarı ağır gelir (MSAA 4x, 4 kademe yumuşak gölge) → hep mobil profil
            if (Application.isMobilePlatform) level = 0;
            QualitySettings.SetQualityLevel(level, true);
            Application.targetFrameRate = TargetFps[quality];
        }
    }
}
