using System.Collections;
using System.Collections.Generic;
using Jumpline.Save;
using UnityEngine;

namespace Jumpline.Audio
{
    /// <summary>
    /// Müzik ve efekt sesleri; ayarlardaki seviyeleri uygular. İlk erişimde kendini oluşturur ve sahneler arası yaşar.
    ///  • Efekt: <see cref="Sfx(string, float, float)"/> — SoundBank'tan id ile; havuzdaki kaynaklardan biriyle çalar,
    ///    perde çeşitlemesi, en uzun süre (kısarak keser) ve tekrar beklemesi uygulanır.
    ///  • Müzik: <see cref="Music"/> — iki kaynak arasında yumuşak geçiş. <see cref="Layer"/>: ana müziğin altında
    ///    döngüdeki ikinci katman (sokakta kısık saz).
    ///  • Döngü: <see cref="Loop"/> — sürekli çalan efekt (adımlar, zipline), ses/perde koddan ayarlanır.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private static AudioManager instance;

        private const int PoolSize = 10;
        private AudioSource music, musicB, layer, stinger;
        private readonly List<AudioSource> pool = new List<AudioSource>();
        private readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private SoundBank bank;
        private string currentTrack, currentLayer;
        private float trackVolume = 0.5f, layerVolume;
        private Coroutine fade;
        private int next;

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
            bank = Resources.Load<SoundBank>("SoundBank");
            music = NewSource(true);
            musicB = NewSource(true);
            layer = NewSource(true);
            stinger = NewSource(false);
            for (int i = 0; i < PoolSize; i++) pool.Add(NewSource(false));
            ApplySettings();
        }

        private AudioSource NewSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Instance.ApplySettings();

        private static float MusicLevel => SaveManager.MusicVolume;
        private static float SfxLevel => SaveManager.SfxVolume;

        public void ApplySettings()
        {
            AudioListener.volume = SaveManager.Muted ? 0f : 1f;
            if (music != null) music.volume = trackVolume * MusicLevel;
            if (layer != null) layer.volume = layerVolume * MusicLevel;
            foreach (var l in loops.Values) if (l != null) l.volume = Mathf.Min(l.volume, SfxLevel);
        }

        // ------------------------------------------------------------------
        // Efekt
        // ------------------------------------------------------------------

        /// <summary>Kısa yol: AudioManager.Instance.Sfx(id).</summary>
        public static void Play(string id, float volume = 1f, float pitch = 1f)
        {
            if (!Application.isPlaying) return;   // edit modu testleri / editör araçları
            Instance.Sfx(id, volume, pitch);
        }

        public void Sfx(string id, float volume = 1f, float pitch = 1f)
        {
            SoundBank.Sound s = bank != null ? bank.FindSound(id) : null;
            if (s == null || s.clip == null) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float t) && now - t < s.cooldown) return;
            lastPlayed[id] = now;
            AudioSource src = pool[next];
            next = (next + 1) % pool.Count;
            src.Stop();
            src.clip = s.clip;
            src.volume = s.volume * volume * SfxLevel;
            src.pitch = s.pitch * pitch * (1f + Random.Range(-s.pitchJitter, s.pitchJitter));
            src.Play();
            if (s.maxDuration > 0f) StartCoroutine(CutOff(src, s.clip, s.maxDuration));
        }

        public void PlayClip(AudioClip clip, float volume = 1f)
        {
            if (clip != null) pool[next = (next + 1) % pool.Count].PlayOneShot(clip, volume * SfxLevel);
        }

        /// <summary>Geriye uyum (eski çağrılar).</summary>
        public void PlaySfx(AudioClip clip) => PlayClip(clip);

        private static IEnumerator CutOff(AudioSource src, AudioClip clip, float after)
        {
            yield return new WaitForSecondsRealtime(after);
            if (src.clip != clip || !src.isPlaying) yield break;
            float v = src.volume;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
            {
                if (src.clip != clip) yield break;
                src.volume = Mathf.Lerp(v, 0f, t / 0.25f);
                yield return null;
            }
            if (src.clip == clip) src.Stop();
        }

        /// <summary>Sürekli efekt: açık/kapalı, ses ve perde (adımlar koşu hızıyla).</summary>
        public void Loop(string id, bool on, float volume = 1f, float pitch = 1f)
        {
            if (!loops.TryGetValue(id, out AudioSource src) || src == null)
            {
                SoundBank.Sound s = bank != null ? bank.FindSound(id) : null;
                if (!on || s == null || s.clip == null) return;
                src = NewSource(true);
                src.clip = s.clip;
                loops[id] = src;
            }
            SoundBank.Sound def = bank.FindSound(id);
            src.volume = (def != null ? def.volume : 1f) * volume * SfxLevel;
            src.pitch = pitch;
            if (on && !src.isPlaying) src.Play();
            else if (!on && src.isPlaying) src.Pause();
        }

        public void StopAllLoops()
        {
            foreach (var l in loops.Values) if (l != null) l.Stop();
        }

        // ------------------------------------------------------------------
        // Müzik
        // ------------------------------------------------------------------

        /// <summary>Müziğe yumuşak geçiş (aynı parça çalıyorsa bir şey yapmaz). id null: sustur.</summary>
        public void Music(string id, float fadeTime = 1.2f)
        {
            if (id == currentTrack) return;
            currentTrack = id;
            SoundBank.Track tr = id != null && bank != null ? bank.FindTrack(id) : null;
            if (fade != null)
            {
                // yarıda kalan geçiş: susmakta olanı hemen kes, gelmekte olanı "çalan" say (iki parça üst üste binmesin)
                StopCoroutine(fade);
                music.Stop();
                (music, musicB) = (musicB, music);
                fade = null;
            }
            fade = StartCoroutine(CrossFade(tr, fadeTime));
        }

        private IEnumerator CrossFade(SoundBank.Track tr, float time)
        {
            AudioSource from = music, to = musicB;
            float fromVol = from.volume;
            if (tr != null && tr.clip != null)
            {
                to.clip = tr.clip;
                to.volume = 0f;
                to.Play();
            }
            trackVolume = tr != null ? tr.volume : 0f;
            for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
            {
                float k = t / time;
                from.volume = Mathf.Lerp(fromVol, 0f, k);
                if (tr != null) to.volume = Mathf.Lerp(0f, trackVolume * MusicLevel, k);
                yield return null;
            }
            from.Stop();
            if (tr != null) to.volume = trackVolume * MusicLevel;
            music = to;
            musicB = from;
            fade = null;
        }

        /// <summary>Ana müziğin altındaki döngü katmanı (sokakta kısık saz). id null: kapat.</summary>
        public void Layer(string id)
        {
            if (id == currentLayer) return;
            currentLayer = id;
            SoundBank.Track tr = id != null && bank != null ? bank.FindTrack(id) : null;
            if (tr == null || tr.clip == null)
            {
                layerVolume = 0f;
                layer.Stop();
                return;
            }
            layer.clip = tr.clip;
            layerVolume = tr.volume;
            layer.volume = layerVolume * MusicLevel;
            layer.Play();
        }

        /// <summary>Kısa müzik parçası (zafer): ana müziği ve katmanı keser, bir kez çalar.</summary>
        public void Stinger(string id)
        {
            Music(null, 0.4f);
            Layer(null);
            SoundBank.Track tr = bank != null ? bank.FindTrack(id) : null;
            if (tr == null || tr.clip == null) return;
            stinger.Stop();
            stinger.clip = tr.clip;
            stinger.volume = tr.volume * MusicLevel;
            stinger.Play();
        }

        public void StopStinger() => stinger.Stop();
    }
}
