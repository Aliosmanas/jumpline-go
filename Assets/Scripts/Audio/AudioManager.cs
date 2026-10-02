using Jumpline.Save;
using UnityEngine;

namespace Jumpline.Audio
{
    /// <summary>
    /// Müzik ve efekt sesleri; ayarlardaki seviyeleri uygular. İlk erişimde kendini oluşturur
    /// ve sahneler arası yaşar. Ses dosyaları henüz yok; geldiğinde PlayMusic / PlaySfx ile çalınır.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;

        private AudioSource music;
        private AudioSource sfx;

        public static AudioManager Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject(nameof(AudioManager));
                DontDestroyOnLoad(go);
                instance = go.AddComponent<AudioManager>();
                return instance;
            }
        }

        private void Awake()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            ApplySettings();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Instance.ApplySettings();

        public void ApplySettings()
        {
            AudioListener.volume = SaveManager.Muted ? 0f : 1f;
            music.volume = SaveManager.MusicVolume;
            sfx.volume = SaveManager.SfxVolume;
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || music.clip == clip) return;
            music.clip = clip;
            music.Play();
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip != null) sfx.PlayOneShot(clip);
        }
    }
}
