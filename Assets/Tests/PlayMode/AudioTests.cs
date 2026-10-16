using System.Collections;
using Jumpline.Audio;
using Jumpline.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>Ses bankası eksiksiz, oyun sahnesinde müzik çalıyor, efektler hata vermeden çalınıyor.</summary>
    public class AudioTests
    {
        [Test]
        public void SoundBank_HasEveryClip()
        {
            var bank = Resources.Load<SoundBank>("SoundBank");
            Assert.IsNotNull(bank, "Resources/SoundBank yok");
            foreach (string id in new[] { "click", "purchase", "coin", "jump", "slide", "stumble", "crash", "crate", "footsteps", "zipline",
                                          "portal", "throw", "splat", "heart_lost", "roar", "tea", "pickup", "powerup" })
                Assert.IsNotNull(bank.FindSound(id)?.clip, "ses eksik: " + id);
            foreach (string id in new[] { "street", "roof", "menu", "boss", "victory", "saz" })
                Assert.IsNotNull(bank.FindTrack(id)?.clip, "müzik eksik: " + id);
        }

        [SetUp]
        public void AudibleSettings()
        {
            // oyuncunun kayıtlı ses ayarı (sessiz / müzik 0) testleri etkilemesin
            Jumpline.Save.SaveManager.Muted = false;
            Jumpline.Save.SaveManager.MusicVolume = 1f;
            AudioManager.Instance.ApplySettings();
        }

        /// <summary>Çalan müzik parçası sayısı (efekt döngüleri hariç).</summary>
        private static int PlayingMusic()
        {
            var bank = Resources.Load<SoundBank>("SoundBank");
            int n = 0;
            foreach (var src in AudioManager.Instance.GetComponents<AudioSource>())
            {
                if (!src.isPlaying || src.clip == null) continue;
                foreach (string id in new[] { "street", "roof", "menu", "boss", "saz" })
                    if (bank.FindTrack(id)?.clip == src.clip && src.volume > 0.001f) { n++; break; }
            }
            return n;
        }

        [UnityTest]
        public IEnumerator Gameplay_PlaysExactlyOneMusicTrack()
        {
            GameSession.Mode = RunMode.Campaign;
            UnityEngine.PlayerPrefs.SetInt("jl.introSeen", 1); UnityEngine.PlayerPrefs.SetInt("jl.tutorialDone", 1);
            // yalnız hata/uyarı sayılır: sahne açılışındaki bilgi günlükleri (ör. [Perf] fps) testi bozmasın
            var problems = new System.Collections.Generic.List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert || type == LogType.Warning) problems.Add(type + ": " + msg);
            }
            Application.logMessageReceived += OnLog;
            try
            {
                SceneManager.LoadScene("Gameplay");
                yield return new WaitForSeconds(2f);
                Assert.IsNotNull(Object.FindAnyObjectByType<GameplayAudio>(), "GameplayAudio yok");
                Assert.AreEqual(1, PlayingMusic(), "tek müzik çalmalı (üst üste parça yok)");
                AudioManager.Play("coin");
                AudioManager.Play("roar");
                yield return null;
            }
            finally { Application.logMessageReceived -= OnLog; }
            CollectionAssert.IsEmpty(problems, "ses sisteminden hata/uyarı");
        }

        [UnityTest]
        public IEnumerator Music_InterruptedCrossFade_LeavesOneTrack()
        {
            var a = AudioManager.Instance;
            a.Music("street", 1f);
            yield return new WaitForSecondsRealtime(0.4f);
            a.Music("roof", 1f);          // ilk geçiş bitmeden
            yield return new WaitForSecondsRealtime(0.3f);
            a.Music("boss", 1f);          // ikinci geçiş de bitmeden
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(1, PlayingMusic(), "yarıda kesilen geçişten sonra tek parça kalmalı");
            a.Music(null, 0.1f);
            yield return new WaitForSecondsRealtime(0.3f);
        }
    }
}
