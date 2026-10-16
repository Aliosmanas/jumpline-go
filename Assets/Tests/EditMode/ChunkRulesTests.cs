using System.Collections.Generic;
using Jumpline.Level.Chunks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Jumpline.Tests
{
    /// <summary>
    /// Chunk parkur kuralları: üretilmiş bütün chunk'lar geçerli; kasıtlı bozuk chunk'lar yakalanır (denetleyici gerçekten
    /// denetliyor); zorluk yöneticisi mesafe evrelerine, tekrar ve nefes kurallarına uyuyor.
    /// </summary>
    public class ChunkRulesTests
    {
        private const string LibPath = "Assets/Data/Obstacles/ObstacleLibrary.asset";
        private ObstacleLibrary lib;

        [SetUp]
        public void Load()
        {
            lib = AssetDatabase.LoadAssetAtPath<ObstacleLibrary>(LibPath);
            Assert.IsNotNull(lib, "engel kütüphanesi yok — Jumpline/Build All Scenes çalıştır");
        }

        private static List<LevelChunk> AllChunks()
        {
            var list = new List<LevelChunk>();
            foreach (string g in AssetDatabase.FindAssets("t:LevelChunk", new[] { "Assets/Data/Chunks" }))
                list.Add(AssetDatabase.LoadAssetAtPath<LevelChunk>(AssetDatabase.GUIDToAssetPath(g)));
            return list;
        }

        private LevelChunk Make(ChunkDifficulty d, float len, params ObstaclePlacement[] obs)
        {
            var c = ScriptableObject.CreateInstance<LevelChunk>();
            c.id = "test";
            c.difficulty = d;
            c.length = len;
            c.obstacles = obs;
            return c;
        }

        [Test]
        public void Definitions_AreSizedForLanes()
        {
            Assert.GreaterOrEqual(lib.Definitions.Count, 15);
            foreach (var d in lib.Definitions)
                CollectionAssert.IsEmpty(ChunkRules.ValidateDefinition(d), d.id);
        }

        [Test]
        public void AllChunks_PassRules_AndPoolIsBigEnough()
        {
            var all = AllChunks();
            int street = 0;
            foreach (var c in all)
            {
                CollectionAssert.IsEmpty(ChunkRules.Validate(c, lib), c.id);
                if (c.world == ChunkWorld.Street && !c.special) street++;
            }
            Assert.GreaterOrEqual(street, 20, "sokak havuzu en az 20 chunk");
            foreach (ChunkDifficulty d in System.Enum.GetValues(typeof(ChunkDifficulty)))
                Assert.IsTrue(all.Exists(c => c.difficulty == d && c.world == ChunkWorld.Street && !c.special), $"{d} chunk yok");
        }

        [Test]
        public void Rejects_ThreeLanesClosed()
        {
            var c = Make(ChunkDifficulty.Hard, 30, new ObstaclePlacement("teacart", -1, 10, 0), new ObstaclePlacement("melonstall", 0, 10, 0),
                         new ObstaclePlacement("simitcart", 1, 10, 0));
            Assert.IsTrue(ChunkRules.Validate(c, lib).Exists(e => e.Contains("üç şerit")));
        }

        [Test]
        public void Rejects_TwoLanesClosedInEasy()
        {
            var c = Make(ChunkDifficulty.Easy, 30, new ObstaclePlacement("teacart", -1, 10, 0), new ObstaclePlacement("melonstall", 1, 10, 0));
            Assert.IsTrue(ChunkRules.Validate(c, lib).Exists(e => e.Contains("iki şerit")));
        }

        [Test]
        public void Rejects_JumpWithoutSafeNeighbour()
        {
            var c = Make(ChunkDifficulty.Medium, 30, new ObstaclePlacement("teacart", -1, 10, 0), new ObstaclePlacement("crates", 0, 10, 0),
                         new ObstaclePlacement("melonstall", 1, 10, 0));
            Assert.IsTrue(ChunkRules.Validate(c, lib).Exists(e => e.Contains("güvenli şerit")));
        }

        [Test]
        public void Rejects_GroupsTooClose_AndEdges()
        {
            var c = Make(ChunkDifficulty.Easy, 30, new ObstaclePlacement("crates", 0, 3, 0), new ObstaclePlacement("jug", -1, 10, 1));
            var e = ChunkRules.Validate(c, lib);
            Assert.IsTrue(e.Exists(x => x.Contains("giriş")), "giriş boş olmalı");
            Assert.IsTrue(e.Exists(x => x.Contains("gruplar arası")), "gruplar arası 12 m");
        }

        [Test]
        public void Rejects_TooManyActionsInSixMeters()
        {
            var c = Make(ChunkDifficulty.Hard, 30, new ObstaclePlacement("bar", -1, 10, 0), new ObstaclePlacement("crates", 1, 11, 0),
                         new ObstaclePlacement("teacart", 0, 14, 0));
            Assert.IsTrue(ChunkRules.Validate(c, lib).Exists(e => e.Contains("farklı hareket")));
        }

        [Test]
        public void Rejects_TrapThatOnlyLooksOpen()
        {
            // her an bir şerit açık görünür ama sağdan sola geçiş penceresi yok: sağ kapanırken sol da kapalı, orta hiç açılmıyor
            var c = Make(ChunkDifficulty.Hard, 45, new ObstaclePlacement("minibus", 0, 6, 0), new ObstaclePlacement("minibus", 0, 11.9f, 0),
                         new ObstaclePlacement("teacart", -1, 6, 0), new ObstaclePlacement("minibus", 1, 15f, 0));
            Assert.IsTrue(ChunkRules.Validate(c, lib).Exists(e => e.Contains("geçilemiyor")));
        }

        [Test]
        public void Rejects_UnknownObstacle_AndHardOnlyInEasy()
        {
            var c = Make(ChunkDifficulty.Easy, 45, new ObstaclePlacement("yok_boyle", 0, 10, 0), new ObstaclePlacement("truck", -1, 20, 1));
            var e = ChunkRules.Validate(c, lib);
            Assert.IsTrue(e.Exists(x => x.Contains("bilinmeyen")));
            Assert.IsTrue(e.Exists(x => x.Contains("en az")));
        }

        [Test]
        public void Director_FollowsDistancePhases_RepeatAndBreathing()
        {
            var go = new GameObject("dir");
            try
            {
                var dir = go.AddComponent<DifficultyDirector>();
                dir.Configure(lib, AllChunks().ToArray());
                dir.Init();
                Assert.Greater(dir.ValidCount, 20);
                Random.InitState(7);
                LevelChunk prev = null, prev2 = null;
                for (float d = 0f; d < 3000f; d += 30f)
                {
                    var c = dir.Next(d, ChunkWorld.Street);
                    Assert.IsNotNull(c);
                    Assert.IsFalse(c.special);
                    if (d < dir.EasyUntil) Assert.AreEqual(ChunkDifficulty.Easy, c.difficulty, $"{d} m: yalnız kolay");
                    else if (d < dir.MediumUntil) Assert.AreNotEqual(ChunkDifficulty.Hard, c.difficulty, $"{d} m: zor yok");
                    else Assert.AreNotEqual(ChunkDifficulty.Easy, c.difficulty, $"{d} m: orta + zor");
                    if (prev != null && prev.difficulty == ChunkDifficulty.Hard) Assert.AreNotEqual(ChunkDifficulty.Hard, c.difficulty, "zordan sonra nefes");
                    Assert.IsFalse(c == prev && c == prev2, "aynı chunk üst üste en fazla 2 kez");
                    prev2 = prev;
                    prev = c;
                }
                dir.SetLevel(8);
                Assert.Less(dir.EasyUntil, 300f, "ileri bölümde kolay evre kısalır");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
