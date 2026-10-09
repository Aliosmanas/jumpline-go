using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jumpline.Audio
{
    /// <summary>
    /// Oyunun tüm sesleri ve müzikleri (Assets/Resources/SoundBank.asset — AudioManager kendisi yükler).
    /// Kaynaklar ve lisanslar: docs/CREDITS.md (Pixabay içerik lisansı, Freesound CC0).
    /// </summary>
    [CreateAssetMenu(fileName = "SoundBank", menuName = "Jumpline/Sound Bank")]
    public class SoundBank : ScriptableObject
    {
        [Serializable]
        public class Sound
        {
            public string id;
            public AudioClip clip;
            [Range(0f, 1.5f)] public float volume = 1f;
            [Tooltip("Temel perde (aynı kaydın farklı kullanımı: ör. güç alma = portal sesi tiz).")]
            [Range(0.3f, 2f)] public float pitch = 1f;
            [Tooltip("Her çalışta perde bu kadar rastgele oynar (tekrar eden seslerde makine hissini kırar).")]
            [Range(0f, 0.4f)] public float pitchJitter = 0.05f;
            [Tooltip("> 0: bu kadar sn sonra kısılarak kesilir (uzun kayıtlar).")]
            [Min(0f)] public float maxDuration;
            [Tooltip("Aynı ses bu kadar sn içinde yeniden çalmaz.")]
            [Min(0f)] public float cooldown = 0.03f;
        }

        [Serializable]
        public class Track
        {
            public string id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 0.5f;
        }

        [SerializeField] private List<Sound> sounds = new List<Sound>();
        [SerializeField] private List<Track> tracks = new List<Track>();

        public Sound FindSound(string id) => sounds.Find(s => s.id == id);
        public Track FindTrack(string id) => tracks.Find(t => t.id == id);

        public void Set(List<Sound> s, List<Track> t)
        {
            sounds = s;
            tracks = t;
        }
    }
}
