using UnityEngine;

namespace Jumpline.Level.Chunks
{
    /// <summary>
    /// Tek bir engel meshinin kimliği ve yerleşim kuralı. Mesh asla rastgele ölçeklenmez: prefab bu tanımdaki gerçek
    /// dünya boyutuna göre editörde bir kez kurulur (GameplaySceneBuilder.Obstacles), çarpışma profili prefab'daki
    /// <see cref="Obstacle"/> bileşenindedir. Renk/doku düzeltmesi meshe değil prefab'ın "Visual" çocuğuna yapılır
    /// (<see cref="ObstacleVisual"/> malzeme değişimi).
    /// </summary>
    [CreateAssetMenu(fileName = "Obs_", menuName = "Jumpline/Obstacle Definition")]
    public class ObstacleDefinition : ScriptableObject
    {
        [Tooltip("Chunk'ların kullandığı kısa kimlik (crates, bar, truck…).")]
        public string id;
        [Tooltip("Kök: Obstacle bileşeni (çarpışma profili). Çocuk 'Visual': mesh + malzeme değişimleri.")]
        public Obstacle prefab;
        [Tooltip("Aynı mesh ve ölçüde renk çeşitleri (prefab varyantı: yalnız malzeme farklı). Boşsa yalnız prefab.")]
        public Obstacle[] variants = System.Array.Empty<Obstacle>();
        public ObstacleKind kind;

        [Header("Gerçek dünya boyutu (m) — prefab bu ölçüye kurulur")]
        [Tooltip("x = genişlik, y = yükseklik, z = koşu yönünde boy.")]
        public Vector3 size = new Vector3(1.6f, 1f, 1f);
        [Tooltip("Engelin koşucuya bakan ön kenarından köke (pivot) mesafe (m). Chunk'taki z ön kenardır.")]
        public float frontToPivot = 0.5f;
        [Tooltip("Çarpışma kutusu: x = yarı genişlik (≤ şerit yarısı), y = yükseklik / tavan alt kenarı, z = boy.")]
        public Vector3 collider = new Vector3(0.9f, 1f, 1f);

        [Header("Yerleşim kuralı")]
        [Tooltip("Kapladığı şerit sayısı (1; kemer 3).")]
        [Range(1, 3)] public int laneSpan = 1;
        [Tooltip("Bu engelin kullanılabileceği en kolay chunk.")]
        public ChunkDifficulty minDifficulty = ChunkDifficulty.Easy;
        [Tooltip("Tavanı yürünebilir (araç): yan şeritteki araçtan / rampadan geçilir.")]
        public bool walkableTop;
        [Tooltip("Koşucunun geldiği uçta rampa var (yerden üstüne çıkılır).")]
        public bool ramp;
        [Tooltip("Tavan engeli (bariyer): alt kenarı collider.y, kalınlığı bu (0 = gökyüzüne kadar).")]
        [Min(0f)] public float ceilingThickness;

        [Header("Hareket (yalnız MovingObstacle)")]
        [Tooltip("Koşucuya doğru hız (m/sn).")]
        [Min(0f)] public float moveSpeed = 3f;
        [Tooltip("Koşucu bu kadar yaklaşınca harekete başlar (m).")]
        [Min(5f)] public float activateDistance = 45f;

        [Header("Görünüm")]
        [Tooltip("Okunabilirlik: kenar ve işaret rengi (renk kodu).")]
        public Color accentColor = Color.white;

        /// <summary>Yerleşim için prefab ya da renk çeşitlerinden biri.</summary>
        public Obstacle Pick()
        {
            if (variants == null || variants.Length == 0) return prefab;
            int i = Random.Range(0, variants.Length + 1);
            return i == 0 || variants[i - 1] == null ? prefab : variants[i - 1];
        }

        /// <summary>Koşu yönünde kapladığı boy (m).</summary>
        public float Length => size.z;

        /// <summary>Yerden yürüyen koşucuyu durdurur mu (şerit değiştirmek, zıplamak ya da rampadan çıkmak gerekir).</summary>
        public bool BlocksGround => kind == ObstacleKind.Dodge || kind == ObstacleKind.FullLane || kind == ObstacleKind.MovingObstacle || kind == ObstacleKind.Climb;

        /// <summary>Çarpışma alanı oluşturan gerçek engel mi (para/güvenli alan/süs değil).</summary>
        public bool IsObstacle => kind != ObstacleKind.CollectibleLine && kind != ObstacleKind.SafeZone && kind != ObstacleKind.Overhead;

        /// <summary>Renk kodu (kullanıcı kuralı): turuncu zıpla, turkuaz kay, kırmızı kaç, sarı dikkat, yeşil güvenli rota.</summary>
        public static Color CodeColor(ObstacleKind k) => k switch
        {
            ObstacleKind.Jump => new Color(1f, 0.54f, 0.12f),
            ObstacleKind.Slide => new Color(0.13f, 0.83f, 0.93f),
            ObstacleKind.Dodge or ObstacleKind.FullLane => new Color(1f, 0.23f, 0.23f),
            ObstacleKind.MovingObstacle => new Color(1f, 0.82f, 0.12f),
            ObstacleKind.Climb => new Color(0.23f, 1f, 0.42f),
            _ => Color.white,
        };
    }
}
