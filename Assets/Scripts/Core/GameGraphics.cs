using Jumpline.Save;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Jumpline.Core
{
    /// <summary>
    /// Ayarlardaki Low/Med/High/Ultra seçimini uygular. Telefonda hep mobil URP profili kullanılır; seviyeler
    /// çözünürlük ölçeği, MSAA, HDR, gölge ve son işlem (bloom vb.) ile ayrışır.
    /// İlk açılışta (kayıtlı seçim yoksa) cihaz zayıfsa Low ile başlar. Oyun içinde kare hızı hedefin altında kalırsa
    /// <see cref="PerformanceGovernor"/> çözünürlüğü kademeli düşürür (<see cref="DynamicScale"/>).
    /// </summary>
    public static class GameGraphics
    {
        private struct Tier
        {
            public float renderScale;
            public int msaa;
            public bool hdr;
            public bool postFx;
            public float shadowDistance;
            public int shadowResolution;
            public int fps;
        }

        // Low, Med, High, Ultra (telefon)
        private static readonly Tier[] MobileTiers =
        {
            new Tier { renderScale = 0.6f, msaa = 1, hdr = false, postFx = false, shadowDistance = 0f, shadowResolution = 512, fps = 60 },
            new Tier { renderScale = 0.7f, msaa = 1, hdr = false, postFx = true, shadowDistance = 22f, shadowResolution = 512, fps = 60 },
            new Tier { renderScale = 0.8f, msaa = 2, hdr = true, postFx = true, shadowDistance = 30f, shadowResolution = 1024, fps = 60 },
            new Tier { renderScale = 1.0f, msaa = 2, hdr = true, postFx = true, shadowDistance = 30f, shadowResolution = 1024, fps = 60 },
        };
        private static readonly int[] DesktopLevel = { 0, 0, 1, 1 };   // ProjectSettings: 0 = Mobile, 1 = PC
        private static readonly int[] DesktopFps = { 30, 60, 60, 60 };

        /// <summary>RAM'i bunun altında olan telefon ilk açılışta Low ile başlar (MB).</summary>
        private const int WeakDeviceMemoryMb = 4500;
        /// <summary>Uyarlamalı çözünürlüğün en düşük çarpanı.</summary>
        public const float MinDynamicScale = 0.7f;

        private static int current = -1;

        /// <summary>Kasma olunca PerformanceGovernor'ın düşürdüğü çarpan (1 = seviyenin kendi ölçeği).</summary>
        public static float DynamicScale { get; private set; } = 1f;
        public static int TargetFps { get; private set; } = 60;
        public static bool OnDevice => !Application.isEditor && Application.isMobilePlatform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (OnDevice && !PlayerPrefs.HasKey(SaveManager.GraphicsQualityKey) && SystemInfo.systemMemorySize < WeakDeviceMemoryMb)
                SaveManager.GraphicsQuality = 0;
            Apply(SaveManager.GraphicsQuality);
            SceneManager.sceneLoaded += (_, _) => ApplyCameras();
            PerformanceGovernor.Ensure();
        }

        public static void Apply(int quality)
        {
            quality = Mathf.Clamp(quality, 0, SaveManager.GraphicsQualityCount - 1);
            if (quality != current) DynamicScale = 1f;
            current = quality;
            if (!OnDevice)
            {
                // editör/masaüstü: paylaşılan URP dosyalarını değiştirme, yalnız kalite seviyesi
                QualitySettings.SetQualityLevel(Mathf.Min(DesktopLevel[quality], QualitySettings.names.Length - 1), true);
                Application.targetFrameRate = TargetFps = DesktopFps[quality];
                return;
            }
            QualitySettings.SetQualityLevel(0, true);   // telefonda masaüstü profili (MSAA 4x, 4 kademe yumuşak gölge) ağır
            Tier t = MobileTiers[quality];
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = t.renderScale * DynamicScale;
                urp.msaaSampleCount = t.msaa;
                urp.supportsHDR = t.hdr;
                urp.shadowDistance = t.shadowDistance;
                urp.mainLightShadowmapResolution = t.shadowResolution;
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFps = t.fps;
            ApplyCameras();
        }

        /// <summary>PerformanceGovernor: çözünürlüğü bir kademe düşürür; daha düşemiyorsa false.</summary>
        public static bool StepDown()
        {
            if (!OnDevice || DynamicScale <= MinDynamicScale + 0.001f) return false;
            DynamicScale = Mathf.Max(MinDynamicScale, DynamicScale - 0.1f);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                urp.renderScale = MobileTiers[Mathf.Max(current, 0)].renderScale * DynamicScale;
            return true;
        }

        private static void ApplyCameras()
        {
            if (!OnDevice || current < 0) return;
            bool post = MobileTiers[current].postFx;
            foreach (Camera cam in Camera.allCameras)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null) data.renderPostProcessing = post;
            }
        }
    }
}
