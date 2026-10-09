using System.Collections.Generic;
using Jumpline.Audio;
using UnityEditor;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Ses bankası (Assets/Resources/SoundBank.asset) ve içe aktarma ayarları. Sesler kullanıcının seçtiği Pixabay /
    /// Freesound (CC0) kayıtları — kaynak listesi docs/CREDITS.md. Efektler: belleğe açılır (gecikmesiz), mono;
    /// müzik: akışla (stream) çalınır, Vorbis %55 (APK boyutu).
    /// </summary>
    public static class AudioSetup
    {
        private const string SfxDir = "Assets/Audio/Sfx";
        private const string MusicDir = "Assets/Audio/Music";
        public const string BankPath = "Assets/Resources/SoundBank.asset";

        //   id, dosya, ses, perde, perde oynaması, en uzun süre (sn), bekleme
        private static readonly (string id, string file, float vol, float pitch, float jitter, float max, float cool)[] Sounds =
        {
            ("click", "click", 0.55f, 1f, 0.04f, 0f, 0.04f),
            ("purchase", "coin", 0.9f, 0.85f, 0f, 0f, 0.1f),
            ("coin", "coin", 0.42f, 1.05f, 0.06f, 0f, 0.07f),
            ("jump", "jump", 0.7f, 1f, 0.06f, 0f, 0.08f),
            ("slide", "slide", 0.8f, 1f, 0.05f, 0.8f, 0.15f),
            ("stumble", "stumble", 0.85f, 1f, 0.05f, 0.9f, 0.25f),
            ("crash", "crash", 0.95f, 1f, 0.03f, 1.2f, 0.3f),
            ("crate", "crate", 0.75f, 1f, 0.08f, 1.2f, 0.12f),
            ("footsteps", "footsteps", 0.24f, 1f, 0f, 0f, 0f),
            ("zipline", "zipline", 0.6f, 1f, 0f, 0f, 0f),
            ("portal", "portal", 0.75f, 1f, 0.02f, 1.6f, 0.4f),
            ("throw", "throw", 0.8f, 1f, 0.08f, 0.9f, 0.1f),
            ("splat", "melon_splat", 1f, 1f, 0.07f, 1.2f, 0.1f),
            ("heart_lost", "heart_lost", 0.75f, 1f, 0f, 1.4f, 0.3f),
            ("roar", "roar", 1f, 1f, 0.03f, 2.6f, 0.8f),
            ("tea", "tea_clink", 0.8f, 1f, 0.05f, 0f, 0.1f),
            ("pickup", "coin", 0.8f, 0.72f, 0.03f, 0f, 0.1f),
            ("powerup", "portal", 0.7f, 1.45f, 0.02f, 0.8f, 0.3f),
        };

        private static readonly (string id, string file, float vol)[] Tracks =
        {
            ("street", "street", 0.42f),
            ("roof", "roof", 0.45f),
            ("menu", "roof", 0.32f),       // kullanıcı kararı: menüde çatı müziği, kısık
            ("boss", "boss", 0.5f),
            ("victory", "victory", 0.55f),
            ("saz", "saz_riff", 0.16f),    // sokak müziğinin altında kısık saz katmanı
        };

        [MenuItem("Jumpline/Setup Audio")]
        public static SoundBank Setup()
        {
            ConfigureImports();
            UIKit.EnsureFolder("Assets/Resources");
            var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<SoundBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }
            var sounds = new List<SoundBank.Sound>();
            foreach (var s in Sounds)
                sounds.Add(new SoundBank.Sound
                {
                    id = s.id, clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{SfxDir}/{s.file}.mp3"),
                    volume = s.vol, pitch = s.pitch, pitchJitter = s.jitter, maxDuration = s.max, cooldown = s.cool,
                });
            var tracks = new List<SoundBank.Track>();
            foreach (var t in Tracks)
                tracks.Add(new SoundBank.Track { id = t.id, clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicDir}/{t.file}.mp3"), volume = t.vol });
            bank.Set(sounds, tracks);
            EditorUtility.SetDirty(bank);
            AssetDatabase.SaveAssets();
            foreach (var s in sounds) if (s.clip == null) Debug.LogWarning($"[Jumpline] Ses yok: {s.id}");
            return bank;
        }

        private static void ConfigureImports()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { SfxDir, MusicDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter imp) continue;
                bool music = path.StartsWith(MusicDir) && !path.EndsWith("saz_riff.mp3");
                imp.forceToMono = !music;
                imp.loadInBackground = music;
                var st = imp.defaultSampleSettings;
                st.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                st.compressionFormat = AudioCompressionFormat.Vorbis;
                st.quality = music ? 0.55f : 0.7f;
                st.preloadAudioData = !music;
                imp.defaultSampleSettings = st;
                imp.SaveAndReimport();
            }
        }
    }
}
