using System;
using UnityEngine;

namespace Jumpline.Level.Chunks
{
    /// <summary>Chunk içinde bir engelin yeri: z engelin koşucuya bakan ön kenarıdır (chunk başından metre).</summary>
    [Serializable]
    public struct ObstaclePlacement
    {
        [Tooltip("ObstacleLibrary kimliği; 'portal' = bölüm sonu portalı.")]
        public string id;
        [Tooltip("-1 sol, 0 orta, 1 sağ (kemer gibi 3 şeritlik engellerde yok sayılır).")]
        [Range(-1, 1)] public int lane;
        [Tooltip("Ön kenarın chunk başından uzaklığı (m).")]
        [Min(0f)] public float z;
        [Tooltip("Engel grubu (aynı anda geçilen engeller aynı numarayı taşır).")]
        [Min(0)] public int group;

        public ObstaclePlacement(string id, int lane, float z, int group)
        {
            this.id = id; this.lane = lane; this.z = z; this.group = group;
        }
    }

    /// <summary>Para dizisi: güvenli rotayı gösterir (Line: zıplamada yay / kaymada alçak hat otomatik; SCurve: şerit değişimi).</summary>
    [Serializable]
    public struct CoinRoute
    {
        public CoinShape shape;
        [Range(-1, 1)] public int lane;
        [Tooltip("SCurve'de varış şeridi.")]
        [Range(-1, 1)] public int toLane;
        [Tooltip("Başlangıç (chunk başından m).")]
        [Min(0f)] public float z;
        [Tooltip("Dizinin boyu (m).")]
        [Min(1f)] public float length;

        public CoinRoute(CoinShape shape, int lane, int toLane, float z, float length)
        {
            this.shape = shape; this.lane = lane; this.toLane = toLane; this.z = z; this.length = length;
        }
    }

    /// <summary>
    /// Elle tasarlanmış, test edilmiş parkur parçası (~30 m). Rastgelelik yalnız chunk'ların sırasındadır
    /// (<see cref="DifficultyDirector"/>); chunk'ın içi sabittir. Kurallar <see cref="ChunkRules"/> ile doğrulanır.
    /// Zemin ve kenar dekoru harita parçalarındadır (MapChain, 30 m); chunk yalnız oynanış katmanıdır, girişi ve çıkışı
    /// her zaman boş zemindir (en az <see cref="ChunkRules.EdgeClear"/> m) → her chunk her chunk'a bağlanır.
    /// </summary>
    [CreateAssetMenu(fileName = "Chunk_", menuName = "Jumpline/Level Chunk")]
    public class LevelChunk : ScriptableObject
    {
        public string id;
        [TextArea] public string notes;
        public ChunkDifficulty difficulty;
        public ChunkWorld world = ChunkWorld.Street;
        [Tooltip("Koşu yönünde boy (m): 30 (tren chunk'ları 45).")]
        [Min(10f)] public float length = 30f;
        public ObstaclePlacement[] obstacles = Array.Empty<ObstaclePlacement>();
        public CoinRoute[] coins = Array.Empty<CoinRoute>();
        [Header("Görsel canlılık")]
        [Tooltip("Chunk ortasındaki görsel vurgu (yolun dışında / üstünde).")]
        public VisualAccent accent;
        [Tooltip("En az bu kadar hareketli süs (sallanan kumaş, uçan kuş, yanan fener…).")]
        [Min(0)] public int movingDecor = 3;
        [Tooltip("Özel parça: yalnız istenince gelir (portal meydanı), rastgele havuza girmez.")]
        public bool special;
    }
}
