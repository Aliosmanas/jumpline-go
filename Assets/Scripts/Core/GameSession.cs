namespace Jumpline.Core
{
    public enum RunMode { Campaign, QuickRace, Daily }

    /// <summary>Menüden oyun sahnesine taşınan seçim: hangi bölüm, hangi mod.</summary>
    public static class GameSession
    {
        public static int Level { get; set; } = 1;
        public static RunMode Mode { get; set; } = RunMode.Campaign;
    }
}
