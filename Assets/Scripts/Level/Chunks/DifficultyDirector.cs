using System.Collections.Generic;
using UnityEngine;

namespace Jumpline.Level.Chunks
{
    /// <summary>
    /// Bölüm içindeki mesafeye göre sıradaki chunk'ı seçer (kullanıcı kuralı, 16 Eki 2026):
    ///  • ilk <see cref="easyUntil"/> m yalnız kolay, <see cref="mediumUntil"/> m'ye kadar kolay + orta, sonra orta + zor;
    ///  • aynı chunk üst üste en fazla 2 kez; zordan sonra mutlaka kolay ya da orta (nefes).
    /// İleri bölümlerde (LevelDirector → <see cref="SetLevel"/>) eşikler öne çekilir. Geçersiz chunk havuza hiç girmez.
    /// </summary>
    public class DifficultyDirector : MonoBehaviour
    {
        [SerializeField] private ObstacleLibrary library;
        [SerializeField] private LevelChunk[] chunks;
        [Header("Mesafe eşikleri (bölüm 1, m)")]
        [SerializeField, Min(0f)] private float easyUntil = 300f;
        [SerializeField, Min(0f)] private float mediumUntil = 800f;
        [Tooltip("Her sonraki bölümde eşikler bu kadar öne çekilir (m).")]
        [SerializeField] private Vector2 shiftPerLevel = new Vector2(30f, 60f);
        [Tooltip("Eşiklerin inebileceği en düşük değer (m).")]
        [SerializeField] private Vector2 minThresholds = new Vector2(120f, 400f);
        [Tooltip("Orta + zor evresinde zor chunk olasılığı (başta → mediumUntil + 1000 m'de).")]
        [SerializeField] private Vector2 hardChance = new Vector2(0.35f, 0.6f);
        [SerializeField, Min(1)] private int maxRepeat = 2;

        private readonly List<LevelChunk> valid = new List<LevelChunk>();
        private readonly List<LevelChunk> pick = new List<LevelChunk>();
        private LevelChunk last;
        private int repeat;
        private float easy, medium;

        public ObstacleLibrary Library => library;
        public float EasyUntil => easy;
        public float MediumUntil => medium;
        public int ValidCount => valid.Count;

        private void Awake() => Init();

        /// <summary>Havuzu kurar: yalnız kuralları geçen chunk'lar (testler de çağırır).</summary>
        public void Init()
        {
            SetLevel(1);
            valid.Clear();
            if (chunks == null) return;
            foreach (var c in chunks)
            {
                if (c == null) continue;
                var errors = ChunkRules.Validate(c, library);
                if (errors.Count == 0) valid.Add(c);
                else Debug.LogWarning("[Jumpline] Chunk havuza alınmadı: " + string.Join(" | ", errors));
            }
        }

        /// <summary>Bölüm numarası (1 tabanlı): ileri bölümlerde kolay evre kısalır, zor evre erken gelir.</summary>
        public void SetLevel(int level)
        {
            int k = Mathf.Max(0, level - 1);
            easy = Mathf.Max(minThresholds.x, easyUntil - shiftPerLevel.x * k);
            medium = Mathf.Max(Mathf.Max(minThresholds.y, easy + 200f), mediumUntil - shiftPerLevel.y * k);
        }

        public void ResetSequence() { last = null; repeat = 0; }

        /// <summary>Özel chunk (portal meydanı) — kimliğiyle.</summary>
        public LevelChunk Special(string id)
        {
            if (chunks == null) return null;
            foreach (var c in chunks) if (c != null && c.special && c.id == id) return c;
            return null;
        }

        /// <summary>Bölüm içindeki mesafeye ve dünyaya göre sıradaki chunk.</summary>
        public LevelChunk Next(float distance, ChunkWorld world)
        {
            bool afterHard = last != null && last.difficulty == ChunkDifficulty.Hard;
            ChunkDifficulty want;
            if (distance < easy) want = ChunkDifficulty.Easy;
            else if (distance < medium) want = Random.value < 0.5f ? ChunkDifficulty.Easy : ChunkDifficulty.Medium;
            else
            {
                float t = Mathf.Clamp01((distance - medium) / 1000f);
                want = Random.value < Mathf.Lerp(hardChance.x, hardChance.y, t) ? ChunkDifficulty.Hard : ChunkDifficulty.Medium;
            }
            if (afterHard && want == ChunkDifficulty.Hard) want = distance < medium ? ChunkDifficulty.Easy : ChunkDifficulty.Medium;

            LevelChunk c = Pick(want, world) ?? Pick(want == ChunkDifficulty.Hard ? ChunkDifficulty.Medium : ChunkDifficulty.Easy, world)
                           ?? Pick(ChunkDifficulty.Easy, world);
            if (c == null) return null;
            repeat = c == last ? repeat + 1 : 1;
            last = c;
            return c;
        }

        private LevelChunk Pick(ChunkDifficulty d, ChunkWorld world)
        {
            pick.Clear();
            foreach (var c in valid)
            {
                if (c.special || c.difficulty != d) continue;
                if (c.world != ChunkWorld.Both && c.world != world) continue;
                if (c == last && repeat >= maxRepeat) continue;
                pick.Add(c);
            }
            return pick.Count > 0 ? pick[Random.Range(0, pick.Count)] : null;
        }

        /// <summary>Editör (sahne kurucu): havuz.</summary>
        public void Configure(ObstacleLibrary lib, LevelChunk[] all)
        {
            library = lib;
            chunks = all;
        }
    }
}
