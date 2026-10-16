using System.Collections.Generic;
using UnityEngine;

namespace Jumpline.Level.Chunks
{
    /// <summary>
    /// Chunk tasarım kuralları (kullanıcının parkur kuralları, 16 Eki 2026) + geçilebilirlik simülasyonu.
    /// Editörde chunk'lar üretilirken, EditMode testlerinde ve oyunda (geçersiz chunk havuza girmez) çalışır.
    /// Simülasyon: chunk 0.25 m adımlarla yürünür; durum = (şerit, yerde / araç tavanında). Her şeritten yerde
    /// girilince sona yerde varılabiliyorsa chunk geçilebilirdir → her chunk her chunk'ın ardından gelebilir.
    /// </summary>
    public static class ChunkRules
    {
        /// <summary>Chunk girişi ve çıkışında engelsiz zemin (m): iki chunk arasında en az 2 × 6 = 12 m.</summary>
        public const float EdgeClear = 6f;
        /// <summary>İki engel grubu arası en az boşluk (m).</summary>
        public const float GroupGap = 12f;
        /// <summary>Bu pencerede en fazla iki farklı hareket istenir (m).</summary>
        public const float ActionWindow = 6f;
        public const int MaxGroups = 3;
        public const int MaxPerGroup = 3;
        /// <summary>Aynı şeritte bu kadar yakın araçlar tek "tren" sayılır (m).</summary>
        public const float TrainLink = 3.2f;

        // simülasyon (PlayerController ile uyumlu, en hızlı durum için ihtiyatlı)
        private const float Step = 0.25f;
        private const float LaneChange = 3f;        // şerit değişimi boyunca gidilen yol (16 m/sn × ~0.19 sn)
        private const float RoofJumpGap = 3.2f;     // araçtan araca atlanabilen boşluk
        private const float RoofStepUp = 0.6f;      // tavandan tavana çıkılabilen fark
        private const float SlideHead = 1.0f;

        public const float JumpMin = 0.8f, JumpMax = 1.2f;
        public const float SlideMin = 1.2f, SlideMax = 1.5f;
        public const float MaxHalfWidth = 1.0f;     // şerit 2.2 m: engel yan şeride taşmaz

        private struct Span
        {
            public ObstacleDefinition def;
            public int lane;
            public float z0, z1;
            public int group;
        }

        /// <summary>Engel tanımının boyut kuralları.</summary>
        public static List<string> ValidateDefinition(ObstacleDefinition d)
        {
            var e = new List<string>();
            if (d == null) { e.Add("tanım yok"); return e; }
            if (d.prefab == null) e.Add($"{d.id}: prefab yok");
            if (d.laneSpan == 1 && d.collider.x > MaxHalfWidth + 1e-3f) e.Add($"{d.id}: yarı genişlik {d.collider.x:0.00} > {MaxHalfWidth} (yan şeride taşar)");
            if (d.kind == ObstacleKind.Jump && (d.collider.y < JumpMin - 1e-3f || d.collider.y > JumpMax + 1e-3f))
                e.Add($"{d.id}: zıplama yüksekliği {d.collider.y:0.00} ∉ [{JumpMin}, {JumpMax}]");
            if (d.kind == ObstacleKind.Slide && (d.collider.y < SlideMin - 1e-3f || d.collider.y > SlideMax + 1e-3f))
                e.Add($"{d.id}: kayma boşluğu {d.collider.y:0.00} ∉ [{SlideMin}, {SlideMax}]");
            if (d.Length <= 0f) e.Add($"{d.id}: boy 0");
            return e;
        }

        /// <summary>Chunk'ın bütün kurallarını denetler; boş liste = geçerli.</summary>
        public static List<string> Validate(LevelChunk c, ObstacleLibrary lib)
        {
            var e = new List<string>();
            if (c == null) { e.Add("chunk yok"); return e; }
            string P(string m) => $"{c.id}: {m}";
            var spans = new List<Span>();
            var overheads = new List<Span>();
            foreach (var p in c.obstacles)
            {
                if (p.id == "portal") continue;
                var d = lib != null ? lib.Get(p.id) : null;
                if (d == null) { e.Add(P($"bilinmeyen engel '{p.id}'")); continue; }
                if (d.minDifficulty > c.difficulty) e.Add(P($"'{d.id}' en az {d.minDifficulty} chunk'ta kullanılır"));
                var span = new Span { def = d, lane = p.lane, z0 = p.z, z1 = p.z + d.Length, group = p.group };
                if (d.kind == ObstacleKind.Overhead) { overheads.Add(span); continue; }   // süs: grup/şerit kurallarına girmez
                spans.Add(span);
            }
            if (spans.Count == 0) return e;   // güvenli alan

            // giriş / çıkış boş
            float first = float.MaxValue, last = float.MinValue;
            foreach (var s in spans) { first = Mathf.Min(first, s.z0); last = Mathf.Max(last, s.z1); }
            if (first < EdgeClear - 1e-3f) e.Add(P($"ilk engel {first:0.0} m'de (giriş en az {EdgeClear} m boş)"));
            if (last > c.length - EdgeClear + 1e-3f) e.Add(P($"son engel {last:0.0} m'de bitiyor (çıkış en az {EdgeClear} m boş, boy {c.length})"));
            foreach (var s in spans)
                if (ApproachStart(s) < 0f) e.Add(P($"hareketli '{s.def.id}' yaklaşırken chunk başını geçiyor (z en az {s.z0 - ApproachStart(s):0.0} m olmalı)"));

            // gruplar: en fazla 3, her biri 1–3 engel (aynı şeritte bitişik araçlar tek tren), aralar ≥ 12 m
            var groups = new SortedDictionary<int, List<Span>>();
            foreach (var s in spans) { if (!groups.TryGetValue(s.group, out var g)) groups[s.group] = g = new List<Span>(); g.Add(s); }
            if (groups.Count > MaxGroups) e.Add(P($"{groups.Count} engel grubu (en fazla {MaxGroups})"));
            var ranges = new List<Vector2>();
            foreach (var kv in groups)
            {
                float a = float.MaxValue, b = float.MinValue;
                foreach (var s in kv.Value) { a = Mathf.Min(a, s.z0); b = Mathf.Max(b, s.z1); }
                ranges.Add(new Vector2(a, b));
                int units = CountUnits(kv.Value);
                if (units > MaxPerGroup) e.Add(P($"grup {kv.Key}: {units} engel (en fazla {MaxPerGroup}; aynı şeritte bitişik araçlar tek sayılır)"));
            }
            ranges.Sort((x, y) => x.x.CompareTo(y.x));
            for (int i = 1; i < ranges.Count; i++)
                if (ranges[i].x - ranges[i - 1].y < GroupGap - 1e-3f)
                    e.Add(P($"gruplar arası {ranges[i].x - ranges[i - 1].y:0.0} m (en az {GroupGap})"));

            // şerit kapanması: üçü birden asla; ikisi yalnız orta/zor
            for (float z = 0f; z <= c.length; z += Step)
            {
                int blocked = 0;
                for (int l = -1; l <= 1; l++) if (GroundBlockedNoClimb(spans, l, z)) blocked++;
                if (blocked >= 3) { e.Add(P($"z={z:0.0}: üç şerit birden kapalı")); break; }
                if (blocked == 2 && c.difficulty == ChunkDifficulty.Easy) { e.Add(P($"z={z:0.0}: iki şerit kapalı (yalnız orta/zor chunk)")); break; }
            }

            // zıplanan / kayılan engelin yanında güvenli (boş) şerit
            foreach (var s in spans)
            {
                if (s.def.kind != ObstacleKind.Jump && s.def.kind != ObstacleKind.Slide) continue;
                bool safe = false;
                for (int l = s.lane - 1; l <= s.lane + 1; l += 2)
                    if (l >= -1 && l <= 1 && !AnyIn(spans, l, s.z0 - 1f, s.z1 + 1f)) safe = true;
                if (!safe) e.Add(P($"'{s.def.id}' (şerit {s.lane}, z {s.z0:0.0}) yanında güvenli şerit yok"));
            }

            // 6 m içinde en fazla iki farklı hareket
            foreach (var s in spans)
            {
                var acts = new HashSet<int>();
                foreach (var o in spans) if (o.z0 >= s.z0 - 1e-3f && o.z0 <= s.z0 + ActionWindow) acts.Add(ActionOf(o.def.kind));
                if (acts.Count > 2) { e.Add(P($"z={s.z0:0.0}: {ActionWindow} m içinde {acts.Count} farklı hareket")); break; }
            }

            // geçilebilirlik: her şeritten girilince sona varılır
            for (int l = -1; l <= 1; l++)
                if (!Solvable(spans, overheads, c, l)) e.Add(P($"şerit {l}'den girilince geçilemiyor"));
            return e;
        }

        private static int ActionOf(ObstacleKind k) => k switch
        {
            ObstacleKind.Jump => 0,
            ObstacleKind.Slide => 1,
            ObstacleKind.Climb => 3,
            _ => 2,   // Dodge, FullLane, Moving: şerit değiştir
        };

        private static int CountUnits(List<Span> g)
        {
            var sorted = new List<Span>(g);
            sorted.Sort((a, b) => a.lane != b.lane ? a.lane.CompareTo(b.lane) : a.z0.CompareTo(b.z0));
            int n = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                bool linked = i > 0 && sorted[i].lane == sorted[i - 1].lane && sorted[i].def.walkableTop && sorted[i - 1].def.walkableTop
                              && sorted[i].z0 - sorted[i - 1].z1 <= TrainLink;
                if (!linked) n++;
            }
            return n;
        }

        private static bool Covers(Span s, int lane, float z) =>
            (s.def.laneSpan >= 3 || s.lane == lane) && z >= ApproachStart(s) && z <= s.z1;

        /// <summary>Hareketli engel koşucuya doğru gelir: şeridini yaklaşma yolu boyunca da kapatır (ihtiyatlı).</summary>
        private static float ApproachStart(Span s) =>
            s.def.kind == ObstacleKind.MovingObstacle ? s.z0 - s.def.moveSpeed * s.def.activateDistance / (8f + s.def.moveSpeed) : s.z0;

        private static bool AnyIn(List<Span> spans, int lane, float z0, float z1)
        {
            foreach (var s in spans)
                if ((s.def.laneSpan >= 3 || s.lane == lane) && s.z1 >= z0 && ApproachStart(s) <= z1) return true;
            return false;
        }

        private static bool GroundBlockedNoClimb(List<Span> spans, int lane, float z)
        {
            foreach (var s in spans)
                if (Covers(s, lane, z) && s.def.BlocksGround && s.def.kind != ObstacleKind.Climb) return true;
            return false;
        }

        /// <summary>Yerde yürüyen koşucu (zıplayarak / kayarak) bu noktadan geçer mi.</summary>
        private static bool GroundOk(List<Span> spans, int lane, float z)
        {
            foreach (var s in spans)
                if (Covers(s, lane, z) && s.def.BlocksGround) return false;
            return true;
        }

        /// <summary>Şeritte z'deki yürünebilir araç tavanı yüksekliği (yoksa -1).</summary>
        private static float RoofAt(List<Span> spans, int lane, float z)
        {
            float h = -1f;
            foreach (var s in spans)
                if (s.def.walkableTop && (s.def.laneSpan >= 3 || s.lane == lane) && z >= s.z0 && z <= s.z1) h = Mathf.Max(h, s.def.collider.y);
            return h;
        }

        /// <summary>z'de rampalı aracın başlangıcı var mı (yerden tavana çıkış).</summary>
        private static bool RampStart(List<Span> spans, int lane, float z)
        {
            foreach (var s in spans)
                if (s.def.ramp && s.lane == lane && z >= s.z0 - Step && z <= s.z0 + Step) return true;
            return false;
        }

        /// <summary>Tavanda (yükseklik roof) koşan, z'deki kemerin altından kayarak geçebilir mi.</summary>
        private static bool OverheadOk(List<Span> overheads, float roof, float z)
        {
            foreach (var o in overheads)
                if (z >= o.z0 && z <= o.z1 && roof + SlideHead >= o.def.collider.y - 0.05f) return false;
            return true;
        }

        private static bool Solvable(List<Span> spans, List<Span> overheads, LevelChunk c, int startLane)
        {
            int n = Mathf.CeilToInt(c.length / Step) + 1;
            int laneSteps = Mathf.CeilToInt(LaneChange / Step);
            // durum: [adım, şerit(0..2), 0 = yer / 1 = tavan]
            var reach = new bool[n, 3, 2];
            var roofH = new float[n, 3];
            reach[0, startLane + 1, 0] = true;
            for (int i = 0; i < n - 1; i++)
            {
                float z = i * Step, nz = (i + 1) * Step;
                for (int li = 0; li < 3; li++)
                {
                    int lane = li - 1;
                    // yerde
                    if (reach[i, li, 0])
                    {
                        if (GroundOk(spans, lane, nz)) reach[i + 1, li, 0] = true;
                        else if (RampStart(spans, lane, nz)) { reach[i + 1, li, 1] = true; roofH[i + 1, li] = RoofAt(spans, lane, nz); }
                        for (int d = -1; d <= 1; d += 2)   // şerit değiştir (iki şerit de pencere boyunca açık)
                        {
                            int tl = lane + d;
                            if (tl < -1 || tl > 1 || i + laneSteps >= n) continue;
                            bool ok = true;
                            for (int k = 1; k <= laneSteps && ok; k++)
                            {
                                float zz = (i + k) * Step;
                                ok = k <= laneSteps / 2 ? GroundOk(spans, lane, zz) : GroundOk(spans, tl, zz);
                            }
                            if (ok) reach[i + laneSteps, tl + 1, 0] = true;
                        }
                    }
                    // araç tavanında
                    if (reach[i, li, 1])
                    {
                        float h = roofH[i, li];
                        float nh = RoofAt(spans, lane, nz);
                        if (nh >= 0f && nh <= h + RoofStepUp && OverheadOk(overheads, nh, nz)) { reach[i + 1, li, 1] = true; roofH[i + 1, li] = nh; }
                        else if (nh < 0f)
                        {
                            // tavan bitti: yakında aynı şeritte araç varsa üstüne atla, yoksa yere in
                            bool hop = false;
                            for (float g = Step; g <= RoofJumpGap; g += Step)
                            {
                                float gh = RoofAt(spans, lane, nz + g);
                                if (gh >= 0f && gh <= h + RoofStepUp)
                                {
                                    int j = Mathf.Min(n - 1, i + 1 + Mathf.RoundToInt(g / Step));
                                    reach[j, li, 1] = true; roofH[j, li] = gh; hop = true; break;
                                }
                            }
                            if (!hop && GroundOk(spans, lane, nz)) reach[i + 1, li, 0] = true;
                        }
                        for (int d = -1; d <= 1; d += 2)   // tavandan yan şeride: yan araç tavanına ya da yere
                        {
                            int tl = lane + d;
                            if (tl < -1 || tl > 1 || i + laneSteps >= n) continue;
                            float th = RoofAt(spans, tl, (i + laneSteps) * Step);
                            if (th >= 0f && Mathf.Abs(th - h) <= RoofStepUp) { reach[i + laneSteps, tl + 1, 1] = true; roofH[i + laneSteps, tl + 1] = th; }
                            else if (th < 0f && GroundOk(spans, tl, (i + laneSteps) * Step)) reach[i + laneSteps, tl + 1, 0] = true;
                        }
                    }
                }
            }
            for (int li = 0; li < 3; li++) if (reach[n - 1, li, 0]) return true;
            return false;
        }
    }
}
