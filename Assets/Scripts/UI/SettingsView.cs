using Jumpline.Audio;
using Jumpline.Core;
using Jumpline.Save;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>Ayarlar ekranı: değerleri kayda yazar ve hemen uygular.</summary>
    public class SettingsView : MonoBehaviour
    {
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private SwitchToggle muteToggle;
        [SerializeField] private SegmentedControl graphicsQuality;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private SwitchToggle vibrationToggle;
        [SerializeField] private SwitchToggle leftHandedToggle;
        [SerializeField] private SwitchToggle contrastToggle;

        private void Awake()
        {
            musicSlider.onValueChanged.AddListener(v => { SaveManager.MusicVolume = v; ApplyAudio(); });
            sfxSlider.onValueChanged.AddListener(v => { SaveManager.SfxVolume = v; ApplyAudio(); });
            muteToggle.Toggle.onValueChanged.AddListener(v => { SaveManager.Muted = v; ApplyAudio(); });
            graphicsQuality.OnChanged.AddListener(i => { SaveManager.GraphicsQuality = i; GameGraphics.Apply(i); });
            sensitivitySlider.onValueChanged.AddListener(v => SaveManager.Sensitivity = v);
            vibrationToggle.Toggle.onValueChanged.AddListener(v =>
            {
                SaveManager.Vibration = v;
                if (v) Haptics.Pulse();
            });
            if (leftHandedToggle != null) leftHandedToggle.Toggle.onValueChanged.AddListener(v => SaveManager.LeftHanded = v);
            if (contrastToggle != null) contrastToggle.Toggle.onValueChanged.AddListener(v => SaveManager.HighContrast = v);
        }

        private void OnEnable()
        {
            musicSlider.SetValueWithoutNotify(SaveManager.MusicVolume);
            sfxSlider.SetValueWithoutNotify(SaveManager.SfxVolume);
            muteToggle.Toggle.SetIsOnWithoutNotify(SaveManager.Muted);
            muteToggle.Snap();
            graphicsQuality.SetWithoutNotify(SaveManager.GraphicsQuality);
            sensitivitySlider.SetValueWithoutNotify(SaveManager.Sensitivity);
            vibrationToggle.Toggle.SetIsOnWithoutNotify(SaveManager.Vibration);
            vibrationToggle.Snap();
            if (leftHandedToggle != null) { leftHandedToggle.Toggle.SetIsOnWithoutNotify(SaveManager.LeftHanded); leftHandedToggle.Snap(); }
            if (contrastToggle != null) { contrastToggle.Toggle.SetIsOnWithoutNotify(SaveManager.HighContrast); contrastToggle.Snap(); }
        }

        private void OnDisable() => SaveManager.Flush();

        private static void ApplyAudio() => AudioManager.Instance.ApplySettings();
    }
}
