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

        [UnityTest]
        public IEnumerator Gameplay_PlaysStreetMusicAndSaz()
        {
            GameSession.Mode = RunMode.Campaign;
            SceneManager.LoadScene("Gameplay");
            yield return new WaitForSeconds(2f);
            Assert.IsNotNull(Object.FindAnyObjectByType<GameplayAudio>(), "GameplayAudio yok");
            int playing = 0;
            foreach (var src in AudioManager.Instance.GetComponents<AudioSource>())
                if (src.isPlaying && src.loop && src.clip != null) playing++;
            Assert.GreaterOrEqual(playing, 2, "müzik + saz katmanı (+ adımlar) çalmalı");
            AudioManager.Play("coin");
            AudioManager.Play("roar");
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}
