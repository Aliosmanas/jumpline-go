using UnityEngine;

namespace Jumpline.Core
{
    /// <summary>
    /// Kare hızı bekçisi (sahneler arası yaşar). Oyun akarken (timeScale &gt; 0) ortalama FPS'i ölçer; hedefin
    /// <see cref="slowRatio"/> katının altında kalırsa çözünürlüğü bir kademe düşürür (<see cref="GameGraphics.StepDown"/>).
    /// Her ölçümü "[Perf]" ile loga yazar → telefonda `adb logcat -s Unity` ile okunur.
    /// </summary>
    public class PerformanceGovernor : MonoBehaviour
    {
        [Tooltip("Ölçüm penceresi (sn).")]
        [SerializeField, Min(1f)] private float window = 4f;
        [Tooltip("Ortalama FPS hedefin bu katının altındaysa çözünürlük düşer.")]
        [SerializeField, Range(0.5f, 1f)] private float slowRatio = 0.85f;
        [Tooltip("Sahne açılışındaki yükleme takılmalarını saymamak için bekleme (sn).")]
        [SerializeField, Min(0f)] private float warmup = 3f;

        private static PerformanceGovernor instance;
        private float elapsed, sinceStart;
        private int frames;

        public static float LastFps { get; private set; }

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject(nameof(PerformanceGovernor));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PerformanceGovernor>();
        }

        private void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += Reset;
        private void OnDisable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded -= Reset;

        private void Reset(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
        {
            sinceStart = 0f;
            elapsed = 0f;
            frames = 0;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            sinceStart += dt;
            if (sinceStart < warmup || Time.timeScale <= 0.01f) { elapsed = 0f; frames = 0; return; }
            elapsed += dt;
            frames++;
            if (elapsed < window) return;
            LastFps = frames / elapsed;
            elapsed = 0f;
            frames = 0;
            bool slow = LastFps < GameGraphics.TargetFps * slowRatio;
            bool stepped = slow && GameGraphics.StepDown();
            if (GameGraphics.OnDevice || Debug.isDebugBuild)
                Debug.Log($"[Perf] fps={LastFps:F1} hedef={GameGraphics.TargetFps} ölçek={GameGraphics.DynamicScale:F2}{(stepped ? " → düşürüldü" : "")}");
        }
    }
}
