using Jumpline.Boss;
using Jumpline.Level;
using Jumpline.Player;
using UnityEngine;

namespace Jumpline.Audio
{
    /// <summary>
    /// Oyun sahnesinin sesleri ve müziği — olaylara bağlanır (kod içinde ses çağrısı dağılmasın):
    ///  Müzik: sokak "Rojê Nû" (saz katmanı kaldırıldı: farklı tempoda iki parça üst üste biniyordu) · çatı "Moon Over Fez" · canavarın öfke/son kalp fazı ve köprü
    ///  "Kingdom - Ancient" · bölüm sonu "Anthem of Victory" (bir kez).
    ///  Efekt: adım döngüsü (koşu hızıyla perde), zıplama, kayma, para, sendeleme, çarpma, zipline, portal,
    ///  atış, karpuz isabeti, kalp düşmesi, kükreme, fırlatılan sandık, güç toplama.
    /// </summary>
    public class GameplayAudio : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private CoinField coins;
        [SerializeField] private WorldSwitcher switcher;
        [SerializeField] private ThrowSystem throws;
        [SerializeField] private BossChase boss;
        [SerializeField] private PowerUps powerUps;
        [SerializeField] private LevelFlowController flow;
        [Tooltip("Adım sesinin 1x perdeyle çaldığı koşu hızı (m/sn).")]
        [SerializeField, Min(1f)] private float stepReferenceSpeed = 11f;

        private bool bossMusic;
        private bool finished;

        private static AudioManager A => AudioManager.Instance;
        /// <summary>Bölümün canavarına göre kükreme perdesi (LevelDirector).</summary>
        private static float Roar => Jumpline.Level.LevelDirector.Active != null ? Jumpline.Level.LevelDirector.Active.roarPitch : 1f;

        private void Start()
        {
            if (player != null)
            {
                player.Jumped += () => A.Sfx("jump");
                player.SlideStarted += () => A.Sfx("slide");
                player.Stumbled += () => A.Sfx("stumble");
                player.Crashed += () => { A.Sfx("crash"); A.Sfx("crate", 0.6f); };
                player.ZiplineChanged += on => A.Loop("zipline", on, 0.8f);
            }
            if (coins != null) coins.Collected += _ => A.Sfx("coin", 0.7f);
            if (switcher != null) switcher.WorldChanged += OnWorldChanged;
            if (throws != null)
            {
                throws.Thrown += () => A.Sfx("throw");
                throws.PickedUp += () => A.Sfx("pickup");
            }
            if (powerUps != null)
            {
                powerUps.Activated += _ => A.Sfx("powerup");
                powerUps.Absorbed += () => A.Sfx("crate");
            }
            bool chase = boss != null && boss.gameObject.activeSelf;
            if (chase)
            {
                A.Sfx("roar", 0.9f, Roar);
                boss.Hit += () => { A.Sfx("splat"); A.Sfx("heart_lost", 0.8f); };
                boss.Dodged += () => A.Sfx("throw", 0.5f, 0.8f);
                boss.PhaseChanged += OnPhase;
                boss.Defeating += () => A.Sfx("roar", 1f, 0.8f * Roar);
                boss.Escaped += () => A.Sfx("roar", 0.7f, 1.15f * Roar);
                boss.ObstacleLanded += () => A.Sfx("crate", 0.55f);
            }
            if (flow != null) flow.LevelCompleted += OnComplete;
            A.StopStinger();
            A.Music("street", 0.8f);
            A.Layer(null);
        }

        private void OnDestroy()
        {
            if (AudioManager.Instance == null) return;
            A.Loop("footsteps", false);
            A.Loop("zipline", false);
        }

        private void OnWorldChanged(bool street)
        {
            A.Sfx("portal");
            if (finished) return;
            if (switcher != null && switcher.InArena) { bossMusic = true; A.Music("boss"); A.Layer(null); return; }
            if (bossMusic) return;
            A.Music(street ? "street" : "roof");
            A.Layer(null);
        }

        private void OnPhase(BossChase.Phase phase)
        {
            if (phase == BossChase.Phase.Rooftop) A.Sfx("roar", 0.8f, 1.05f * Roar);
            if (phase < BossChase.Phase.Fury || bossMusic || finished) return;
            bossMusic = true;   // öfke fazından itibaren gerilim müziği
            A.Sfx("roar", 1f, Roar);
            A.Music("boss");
            A.Layer(null);
        }

        private void OnComplete()
        {
            finished = true;
            A.Loop("footsteps", false);
            A.Stinger("victory");
        }

        private void Update()
        {
            if (player == null) return;
            bool running = !player.IsDead && !player.IsAirborne && !player.IsSliding && !player.OnZipline && Time.timeScale > 0.5f && !finished;
            A.Loop("footsteps", running, 0.55f, Mathf.Clamp(player.RunSpeed / stepReferenceSpeed, 0.85f, 1.5f));
        }
    }
}
