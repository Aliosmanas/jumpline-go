namespace Jumpline.Level.Chunks
{
    /// <summary>
    /// Engelin oyuncudan istediği hareket. Renk kodu sabittir (ObstacleHint işaretleri + ObstacleVisual):
    /// Jump turuncu, Slide turkuaz, Dodge/FullLane kırmızı, MovingObstacle sarı-siyah, Climb yeşil.
    /// </summary>
    public enum ObstacleKind
    {
        /// <summary>Üstünden zıplanır (yükseklik 0.8–1.2 m).</summary>
        Jump,
        /// <summary>Altından kayılır (alt boşluk 1.2–1.5 m).</summary>
        Slide,
        /// <summary>Şerit değiştirerek kaçılır; tek şerit, yerden aşılamaz (tezgâh, araba, taksi).</summary>
        Dodge,
        /// <summary>Tek şeridi boydan boya kapatır, mutlaka yandan geçilir (durak, minibüs, otobüs; tavanı yürünür).</summary>
        FullLane,
        /// <summary>Şeridinde koşucuya doğru yavaşça gelir (basit hareket, fizik yok).</summary>
        MovingObstacle,
        /// <summary>Para / toplama dizisi; engel değildir (chunk'ta CoinRoute olarak tutulur).</summary>
        CollectibleLine,
        /// <summary>Engelsiz, görsel olarak zengin alan.</summary>
        SafeZone,
        /// <summary>Rampalı araç: rampadan üstüne çıkılır, tavanda koşulur (Subway'deki tren).</summary>
        Climb,
        /// <summary>Yolun üstünden geçen süs (çarşı kemeri): yerde engel değil, yalnız araç tavanındaki koşucuya tavan olur.</summary>
        Overhead,
    }

    public enum ChunkDifficulty { Easy, Medium, Hard }

    public enum ChunkWorld { Street, Rooftop, Both }

    /// <summary>Para dizisinin biçimi: yön gösterir (zıplamada yay, kaymada alçak hat, şerit değişiminde S).</summary>
    public enum CoinShape
    {
        /// <summary>Şeritte düz; önündeki zıplanır engelin üstünden kendiliğinden yay çizer, bariyerin altında alçalır.</summary>
        Line,
        /// <summary>Bir şeritten ötekine yumuşak S (şerit değiştirme rotası).</summary>
        SCurve,
    }

    /// <summary>Chunk'ın görsel vurgusu (yolun dışında / üstünde; oyun alanına taşmaz).</summary>
    public enum VisualAccent { None, Arch, Lanterns, Sign, ShopFront, Column, BridgePier, Rooftop }
}
