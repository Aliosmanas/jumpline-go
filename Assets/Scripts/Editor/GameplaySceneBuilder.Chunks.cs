using System;
using System.Collections.Generic;
using System.Linq;
using Jumpline.Level;
using Jumpline.Level.Chunks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static Jumpline.EditorTools.UIKit;
using Object = UnityEngine.Object;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Chunk parkur sistemi (16 Eki 2026, kullanıcı kararı: eski rastgele/JSON parkur söküldü).
    ///  1) Engel prefab'ları + tanımları: her Meshy engeli bir kez, tanımdaki gerçek boyuta kurulur (Assets/Prefabs/Obstacles,
    ///     Assets/Data/Obstacles). Kök = Obstacle (çarpışma profili, ölçek 1); çocuk "Visual" = mesh (+ ObstacleVisual malzeme
    ///     değişimi). Meshe ve FBX'e dokunulmaz.
    ///  2) Chunk'lar: aşağıdaki tablodan LevelChunk varlıkları (Assets/Data/Chunks); her biri ChunkRules ile doğrulanır,
    ///     geçersiz olan derlemede hata verir ve havuza girmez.
    ///  3) Görsel vurgular (kemer, fener, minare, köprü ayağı…) ve hareketli süsler (kuş, flama, sallanan fener) prefab'ları.
    /// </summary>
    public static partial class GameplaySceneBuilder
    {
        private const string ObstaclePrefabDir = "Assets/Prefabs/Obstacles";
        private const string ObstacleDefDir = "Assets/Data/Obstacles";
        private const string ChunkDir = "Assets/Data/Chunks";
        private const string AccentDir = "Assets/Prefabs/Accents";
        private const string AmbientArtDir = "Assets/Art/Environment/Ambient";
        public const string ObstacleLibraryPath = ObstacleDefDir + "/ObstacleLibrary.asset";

        private struct ObsSpec
        {
            public string id;
            public ObstacleKind kind;
            public Func<Transform, Obstacle> make;
            public float height;               // > 0: görsel bu yüksekliğe (m) ölçeklenir (zıplanan engeller); 0 = doğal
            public float slideBottom;          // > 0: tavan engelinin alt kenarı (m), görsel dikeyde buna uyar
            public ChunkDifficulty min;
            public bool walk, ramp;
            public int span;
            public float moveSpeed;
            public (string slot, string hex, string suffix)[] recolor;
            public bool turn;                  // görsel şeritten genişse 90° döndür (uzun kenar yol boyunca: durak, mangal)
        }

        /// <summary>Tek şeritlik engelin görsel genişlik sınırı (m): şerit 2.2, yanlara en fazla 5 cm taşar.</summary>
        private const float MaxVisualWidth = 2.3f;

        /// <summary>Engel tanımları + prefab'lar + kütüphane (ölçüler buradaki tablodan; sahnede şablon kalmaz).</summary>
        private static ObstacleLibrary BuildObstacleLibrary()
        {
            EnsureFolder(ObstaclePrefabDir);
            EnsureFolder(ObstacleDefDir);
            var work = new GameObject("ObstacleWork");
            work.transform.position = new Vector3(0f, -50f, 0f);   // ölçülürken görünmesin
            Obstacle V(string file, Transform p, Obstacle.Kind kind, float hw, bool ramp = false) =>
                VehicleObstacle($"{ObstacleDir}/{file}.fbx", "Obs_" + file, p, kind, hw, null, 0f, ramp);
            ObsSpec S(string id, ObstacleKind k, Func<Transform, Obstacle> make, float h = 0f, ChunkDifficulty min = ChunkDifficulty.Easy,
                      bool walk = false, bool ramp = false, int span = 1, float slide = 0f, float move = 0f, bool turn = false,
                      params (string, string, string)[] recolor) =>
                new ObsSpec { id = id, kind = k, make = make, height = h, min = min, walk = walk, ramp = ramp, span = span, slideBottom = slide,
                              moveSpeed = move, recolor = recolor, turn = turn };
            var specs = new[]
            {
                // zıpla (0.8–1.2 m): yükseklik tanımdan
                S("crates", ObstacleKind.Jump, CrateStackObstacle, 1.1f),
                S("jug", ObstacleKind.Jump, p => MeshyObstacle("ClayJug", p, 1.0f, 0f), 1.0f),
                S("stalldecor", ObstacleKind.Jump, p => MeshyObstacle("M06_StallDecor", p, 1.05f, 0f), 1.05f),
                S("watermelon", ObstacleKind.Jump, p => MeshyObstacle("Watermelon", p, 0.85f, 0f), 0.85f),
                S("teatable", ObstacleKind.Jump, p => PiecesObstacle($"{ObstacleDir}/TeaTable.fbx", "Obs_TeaTable", p, 1f), 0.85f),
                S("patpat", ObstacleKind.Jump, p => V("PatPat", p, Obstacle.Kind.Solid, 0.7f), 0f, ChunkDifficulty.Medium),
                // kay (alt boşluk 1.2–1.5 m)
                S("bar", ObstacleKind.Slide, p => VehicleObstacle(SlideBarPath, "Obs_SlideBar", p, Obstacle.Kind.Ceiling, 1.0f,
                    new[] { new Obstacle.Segment { z0 = -0.3f, z1 = 0.3f, h0 = 1.15f, h1 = 1.15f } }, 0.35f), 0f, slide: 1.25f),
                // kaç (tek şerit, yerden aşılmaz)
                S("teacart", ObstacleKind.Dodge, p => MeshyObstacle("TeaCart", p, 0f, 4f)),
                S("simitcart", ObstacleKind.Dodge, p => MeshyObstacle("SimitCart", p, 0f, 4f)),
                S("melonstall", ObstacleKind.Dodge, p => MeshyObstacle("MelonStall", p, 0f, 4f)),
                S("grill", ObstacleKind.Dodge, p => MeshyObstacle("KebabGrill", p, 0f, 4f), turn: true),
                S("taxi", ObstacleKind.Dodge, p => VehicleObstacle(TaxiPath, "Obs_Taxi", p, Obstacle.Kind.Solid, 0.86f, null), walk: true,
                    recolor: new[] { ("T_Yellow", "#EDEAE3", "White"), ("T_Yellow", "#B3262E", "Red") }),
                // şeridi kapatır (tavanı yürünür)
                S("busstop", ObstacleKind.FullLane, p => MeshyObstacle("BusStop", p, 0f, 4f), turn: true),
                S("minibus", ObstacleKind.FullLane, p => VehicleObstacle(MinibusPath, "Obs_Minibus", p, Obstacle.Kind.Solid, 1.0f, null), walk: true),
                S("citybus", ObstacleKind.FullLane, p => V("CityBus", p, Obstacle.Kind.Solid, 1.0f), 0f, ChunkDifficulty.Medium, walk: true),
                // çık (rampalı araç)
                S("truck", ObstacleKind.Climb, TruckObstacle, 0f, ChunkDifficulty.Medium, walk: true, ramp: true,
                    recolor: new[] { ("T_Paint", "#9E2B25", "Red"), ("T_Paint", "#D9A21B", "Yellow") }),
                S("melon", ObstacleKind.Climb, p => V("MelonTruck", p, Obstacle.Kind.Solid, 1.0f, true), 0f, ChunkDifficulty.Medium, walk: true, ramp: true),
                S("tractor", ObstacleKind.Climb, p => V("Tractor", p, Obstacle.Kind.Solid, 1.0f, true), 0f, ChunkDifficulty.Medium, walk: true, ramp: true),
                // hareketli: şeridinde yavaşça gelen taksi
                S("taxi_moving", ObstacleKind.MovingObstacle, p => VehicleObstacle(TaxiPath, "Obs_TaxiMoving", p, Obstacle.Kind.Solid, 0.86f, null),
                    0f, ChunkDifficulty.Medium, walk: true, move: 3f),
                // yolun üstü: çarşı kemeri (yerde engel değil; araç tavanındakine tavan)
                S("gate", ObstacleKind.Overhead, MeshyGate, 0f, ChunkDifficulty.Hard, span: 3),
            };

            var defs = new List<ObstacleDefinition>();
            foreach (var sp in specs)
            {
                Obstacle o = sp.make(work.transform);
                if (o == null) { Debug.LogError($"[Jumpline] Engel kurulamadı: {sp.id}"); continue; }
                Transform visual = MakeVisualChild(o);
                if (sp.height > 0f) FitHeight(o, visual, sp.height);
                if (sp.slideBottom > 0f) FitCeiling(o, visual, sp.slideBottom);
                if (sp.span == 1) FitWidth(o, visual, sp.turn, sp.kind == ObstacleKind.Slide);
                if (sp.span == 1 && o.HalfWidth > ChunkRules.MaxHalfWidth) o.Configure(o.Type, ChunkRules.MaxHalfWidth, o.Segments, o.Thickness);
                Color code = ObstacleDefinition.CodeColor(sp.kind);
                visual.gameObject.AddComponent<ObstacleVisual>().Configure(null, code);
                if (sp.kind == ObstacleKind.MovingObstacle) o.gameObject.AddComponent<MovingObstacle>().Configure(sp.moveSpeed, 45f);
                foreach (var r in o.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.On;

                var def = LoadOrCreate<ObstacleDefinition>($"{ObstacleDefDir}/Obs_{sp.id}.asset");
                def.id = sp.id;
                def.kind = sp.kind;
                def.minDifficulty = sp.min;
                def.walkableTop = sp.walk;
                def.ramp = sp.ramp;
                def.laneSpan = sp.span;
                def.moveSpeed = sp.moveSpeed;
                def.activateDistance = 45f;
                def.accentColor = code;
                Measure(o, def);
                def.prefab = SaveObstaclePrefab(o, sp.id);
                var variants = new List<Obstacle>();
                if (sp.recolor != null)
                    foreach (var (slot, hex, suffix) in sp.recolor)
                    {
                        var mat = RecolorMaterial(slot, hex);
                        if (mat == null) continue;
                        var copy = Object.Instantiate(o.gameObject, work.transform);
                        copy.transform.position = o.transform.position;
                        copy.GetComponentInChildren<ObstacleVisual>().Configure(new[] { new ObstacleVisual.Override { slot = slot, material = mat } }, code);
                        variants.Add(SaveObstaclePrefab(copy.GetComponent<Obstacle>(), sp.id + "_" + suffix));
                    }
                def.variants = variants.ToArray();
                foreach (var err in ChunkRules.ValidateDefinition(def)) Debug.LogError("[Jumpline] Engel tanımı: " + err);
                EditorUtility.SetDirty(def);
                defs.Add(def);
            }
            Object.DestroyImmediate(work);
            var lib = LoadOrCreate<ObstacleLibrary>(ObstacleLibraryPath);
            lib.SetDefinitions(defs);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            return lib;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        /// <summary>Kökteki görsel parçaları "Visual" çocuğuna toplar; kökün ölçeği Visual'a geçer (kök ölçek 1: çarpışma metre).</summary>
        private static Transform MakeVisualChild(Obstacle o)
        {
            Transform root = o.transform;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            var kids = new List<Transform>();
            foreach (Transform c in root) if (c != visual) kids.Add(c);
            foreach (var c in kids) c.SetParent(visual, false);   // Visual birim: yerel değerler aynen kalır
            Vector3 s = root.localScale;
            root.localScale = Vector3.one;
            visual.localScale = s;                               // dünya ölçeği değişmez (kök × Visual = eski kök)
            return visual;
        }

        private static Bounds VisualBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Zıplanan engel: görsel orantılı ölçeklenip yüksekliği tanıma eşitlenir; çarpışma kutusu görselden.</summary>
        private static void FitHeight(Obstacle o, Transform visual, float height)
        {
            Vector3 pivot = o.transform.position;
            Bounds b = VisualBounds(visual);
            float k = height / Mathf.Max(0.01f, b.max.y - pivot.y);
            visual.localScale *= k;
            b = VisualBounds(visual);
            visual.position += new Vector3(pivot.x - b.center.x, pivot.y - b.min.y, pivot.z - b.center.z);   // tabanı yerde, ortası pivotta
            b = VisualBounds(visual);
            float halfW = Mathf.Clamp(b.extents.x, 0.45f, ChunkRules.MaxHalfWidth);
            float halfD = Mathf.Max(b.extents.z, 0.3f);
            o.Configure(Obstacle.Kind.Solid, halfW, new[] { new Obstacle.Segment { z0 = -halfD, z1 = halfD, h0 = height, h1 = height } });
        }

        /// <summary>
        /// Görsel şeritten taşmasın (≤ <see cref="MaxVisualWidth"/>): önce (izinliyse) 90° döndürülür, hâlâ genişse orantılı
        /// küçültülür (bariyer yalnız enine). Ölçek pivot etrafında: taban yerde kalır, çarpışma profili aynı oranla ölçeklenir.
        /// </summary>
        private static void FitWidth(Obstacle o, Transform visual, bool turn, bool xOnly)
        {
            Vector3 p = o.transform.position;
            Bounds b = VisualBounds(visual);
            if (b.size.x <= MaxVisualWidth + 1e-3f) return;
            if (turn && b.size.z < b.size.x)
            {
                visual.RotateAround(p, Vector3.up, 90f);
                b = VisualBounds(visual);
                visual.position += new Vector3(p.x - b.center.x, 0f, p.z - b.center.z);
                b = VisualBounds(visual);
                float top = o.Segments.Length > 0 ? o.Segments[0].h0 : b.max.y - p.y;   // kutu engel: yeni ayak izi
                o.Configure(o.Type, Mathf.Clamp(b.extents.x, 0.45f, ChunkRules.MaxHalfWidth),
                    new[] { new Obstacle.Segment { z0 = -b.extents.z, z1 = b.extents.z, h0 = top, h1 = top } }, o.Thickness);
            }
            if (b.size.x <= MaxVisualWidth + 1e-3f) return;
            float k = MaxVisualWidth / b.size.x;
            if (xOnly) { visual.localScale = Vector3.Scale(visual.localScale, new Vector3(k, 1f, 1f)); return; }
            visual.localScale *= k;
            visual.localPosition *= k;
            var ns = o.Segments.Select(s => new Obstacle.Segment
            {
                z0 = s.z0 * k, z1 = s.z1 * k,
                h0 = s.h0 >= 3.99f ? s.h0 : s.h0 * k, h1 = s.h1 >= 3.99f ? s.h1 : s.h1 * k,   // 4 m = aşılamaz duvar (olduğu gibi)
            }).ToArray();
            o.Configure(o.Type, Mathf.Min(o.HalfWidth, ChunkRules.MaxHalfWidth), ns, o.Thickness * k);
        }

        /// <summary>Kayılan bariyer: görsel dikeyde uzatılır (direkler yerde kalır), alt kenar tanımdaki boşluğa oturur.</summary>
        private static void FitCeiling(Obstacle o, Transform visual, float bottom)
        {
            var segs = o.Segments;
            float old = segs.Length > 0 ? segs[0].h0 : bottom;
            float k = bottom / Mathf.Max(0.01f, old);
            visual.localScale = Vector3.Scale(visual.localScale, new Vector3(1f, k, 1f));
            var ns = segs.Select(s => new Obstacle.Segment { z0 = s.z0, z1 = s.z1, h0 = bottom, h1 = bottom }).ToArray();
            o.Configure(Obstacle.Kind.Ceiling, o.HalfWidth, ns, o.Thickness * k);
        }

        /// <summary>Tanıma gerçek ölçüleri yazar: görsel boyut, ön kenar → pivot, çarpışma kutusu.</summary>
        private static void Measure(Obstacle o, ObstacleDefinition def)
        {
            Vector3 p = o.transform.position;
            Bounds b = VisualBounds(o.transform);
            var segs = o.Segments;
            float front = b.min.z - p.z, back = b.max.z - p.z, top = 0f;
            if (segs.Length > 0) { front = Mathf.Min(front, segs[0].z0); back = Mathf.Max(back, segs[^1].z1); }
            foreach (var s in segs) top = Mathf.Max(top, Mathf.Max(s.h0, s.h1));
            if (o.Type == Obstacle.Kind.Ceiling && segs.Length > 0) top = segs[0].h0;   // tavan: alt kenar
            def.size = new Vector3(b.size.x, b.max.y - p.y, back - front);
            def.frontToPivot = -front;
            def.collider = new Vector3(o.HalfWidth, top, back - front);
            def.ceilingThickness = o.Type == Obstacle.Kind.Ceiling ? o.Thickness : 0f;
        }

        private static Obstacle SaveObstaclePrefab(Obstacle o, string id)
        {
            Vector3 keep = o.transform.position;
            o.transform.position = Vector3.zero;
            o.gameObject.name = "Obs_" + id;
            var saved = PrefabUtility.SaveAsPrefabAsset(o.gameObject, $"{ObstaclePrefabDir}/Obs_{id}.prefab");
            o.transform.position = keep;
            return saved.GetComponent<Obstacle>();
        }

        private static Material RecolorMaterial(string slot, string hex)
        {
            string path = $"{TruckMatDir}/{slot}_{hex.TrimStart('#')}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var baseMat = AssetDatabase.LoadAssetAtPath<Material>($"{TruckMatDir}/{slot}.mat");
                if (baseMat == null) return null;
                mat = new Material(baseMat);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", Hex(hex));
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---------------------------------------------------------------------
        // Chunk tablosu — kurallar: ChunkRules (giriş/çıkış 6 m boş, gruplar arası ≥ 12 m, ≤ 3 grup, üç şerit asla kapalı değil,
        // zıplanan/kayılanın yanında boş şerit, 6 m'de ≤ 2 hareket, iki şerit kapatan yalnız orta/zor, her şeritten geçilebilir).
        // z = engelin ön kenarı (chunk başından m). Şerit: -1 sol, 0 orta, 1 sağ.
        // ---------------------------------------------------------------------
        private const int LnL = -1, LnM = 0, LnR = 1;

        private static ObstaclePlacement Ob(string id, int lane, float z, int group = 0) => new ObstaclePlacement(id, lane, z, group);
        private static CoinRoute CoinLine(int lane, float z, float len) => new CoinRoute(CoinShape.Line, lane, lane, z, len);
        private static CoinRoute CoinCurve(int from, int to, float z, float len) => new CoinRoute(CoinShape.SCurve, from, to, z, len);

        private struct ChunkSpec
        {
            public string id, notes;
            public ChunkDifficulty diff;
            public ChunkWorld world;
            public float length;
            public VisualAccent accent;
            public int moving;
            public bool special;
            public ObstaclePlacement[] obs;
            public CoinRoute[] coins;
        }

        private static ChunkSpec Chunk(string id, ChunkDifficulty d, float len, VisualAccent accent, string notes, ObstaclePlacement[] obs, params CoinRoute[] coins) =>
            new ChunkSpec { id = id, diff = d, world = ChunkWorld.Street, length = len, accent = accent, moving = 3, notes = notes, obs = obs, coins = coins };

        private static ChunkSpec RoofChunk(ChunkSpec c) { c.world = ChunkWorld.Rooftop; return c; }

        private static readonly ChunkDifficulty DEasy = ChunkDifficulty.Easy, DMed = ChunkDifficulty.Medium, DHard = ChunkDifficulty.Hard;

        private static ChunkSpec[] ChunkTable() => new[]
        {
            // ---------------- KOLAY (ilk 300 m) ----------------
            Chunk("e_crates_center", DEasy, 30, VisualAccent.Lanterns, "Ortada sandık: zıpla ya da yana geç. Para yayı üstünden.",
              new[] { Ob("crates", LnM, 12) }, CoinLine(LnM, 3, 20)),
            Chunk("e_jug_watermelon", DEasy, 30, VisualAccent.ShopFront, "Solda testi, sağda karpuz; orta şerit hep açık.",
              new[] { Ob("jug", LnL, 8, 0), Ob("watermelon", LnR, 21.5f, 1) }, CoinLine(LnM, 3, 22)),
            Chunk("e_bar_intro", DEasy, 30, VisualAccent.None, "İlk kayma: ortada bariyer, paralar altından alçak hat.",
              new[] { Ob("bar", LnM, 12) }, CoinLine(LnM, 6, 12)),
            Chunk("e_cart_dodge", DEasy, 30, VisualAccent.ShopFront, "Solda çay arabası, sağda simit arabası: ortadan koş.",
              new[] { Ob("teacart", LnL, 8, 0), Ob("simitcart", LnR, 22, 1) }, CoinLine(LnM, 3, 22)),
            WithMoving(Chunk("e_safe_bazaar", DEasy, 30, VisualAccent.Arch, "Güvenli alan: çarşı kemeri, S para rotası, bol süs.",
              new ObstaclePlacement[0], CoinCurve(LnL, LnR, 4, 22)), 5),
            Chunk("e_stall_crates", DEasy, 30, VisualAccent.None, "Solda tezgâh süsü, sağda sandık: ortada düz para.",
              new[] { Ob("stalldecor", LnL, 7, 0), Ob("crates", LnR, 21.5f, 1) }, CoinLine(LnM, 3, 22)),
            Chunk("e_teatable_bar", DEasy, 30, VisualAccent.Lanterns, "Ortada çay masası (zıpla), sonra solda bariyer; paralar sağa kıvrılır.",
              new[] { Ob("teatable", LnM, 8, 0), Ob("bar", LnL, 22, 1) }, CoinLine(LnM, 2, 14), CoinCurve(LnM, LnR, 17, 9)),
            Chunk("e_busstop_side", DEasy, 30, VisualAccent.Column, "Sağda durak: tek şerit kapalı, sol şeritte paralar.",
              new[] { Ob("busstop", LnR, 9) }, CoinLine(LnL, 3, 22)),
            WithMoving(Chunk("e_safe_lanterns", DEasy, 30, VisualAccent.Lanterns, "Güvenli alan: fener dizisi, ortada para.",
              new ObstaclePlacement[0], CoinLine(LnM, 4, 22)), 4),

            // ---------------- ORTA (300–800 m kolayla karışık) ----------------
            Chunk("m_weave", DMed, 45, VisualAccent.ShopFront, "Zikzak: sol sandık → orta bariyer → sağ testi; S paralar yolu gösterir.",
              new[] { Ob("crates", LnL, 6, 0), Ob("bar", LnM, 19, 1), Ob("jug", LnR, 32, 2) },
              CoinLine(LnL, 1, 9), CoinCurve(LnL, LnM, 11, 6), CoinLine(LnM, 17, 5), CoinCurve(LnM, LnR, 24, 6), CoinLine(LnR, 30, 6)),
            Chunk("m_two_carts", DMed, 30, VisualAccent.None, "İki yanda araba (iki şerit kapalı) → ortadan geç, sonra ortadaki sandığı zıpla.",
              new[] { Ob("teacart", LnL, 8, 0), Ob("melonstall", LnR, 8, 0), Ob("crates", LnM, 22.5f, 1) }, CoinLine(LnM, 2, 24)),
            Chunk("m_bus_wall", DMed, 30, VisualAccent.Lanterns, "Solda minibüs, sağda durak: orta şerit tek yol.",
              new[] { Ob("minibus", LnL, 6, 0), Ob("busstop", LnR, 7, 0) }, CoinLine(LnM, 2, 22)),
            Chunk("m_taxi_rows", DMed, 45, VisualAccent.Column, "Solda sonra sağda taksi; ortada uzun para dizisi.",
              new[] { Ob("taxi", LnL, 6, 0), Ob("taxi", LnR, 22.5f, 1) }, CoinLine(LnM, 2, 36)),
            Chunk("m_bar_cart", DMed, 30, VisualAccent.None, "Solda bariyer (kay) + sağda araba; orta açık, sonra ortada sandık.",
              new[] { Ob("bar", LnL, 9, 0), Ob("teacart", LnR, 8, 0), Ob("crates", LnM, 22, 1) }, CoinLine(LnM, 2, 25)),
            Chunk("m_patpat", DMed, 30, VisualAccent.ShopFront, "Ortada patpat (üstünden zıpla), sonra solda simit arabası; paralar sağa döner.",
              new[] { Ob("patpat", LnM, 7, 0), Ob("simitcart", LnL, 22, 1) }, CoinLine(LnM, 1, 12), CoinCurve(LnM, LnR, 14, 8)),
            Chunk("m_citybus", DMed, 45, VisualAccent.BridgePier, "Solda otobüs, sağda minibüs (iki şerit) → ortadan; sonra ortada bariyer.",
              new[] { Ob("citybus", LnL, 6, 0), Ob("minibus", LnR, 7, 0), Ob("bar", LnM, 28, 1) }, CoinLine(LnM, 2, 34)),
            Chunk("m_moving_taxi", DMed, 45, VisualAccent.Lanterns, "Ortada karşıdan gelen taksi (sarı-siyah işaret): sola geç, sonra soldaki sandığı zıpla.",
              new[] { Ob("taxi_moving", LnM, 20, 0), Ob("crates", LnL, 37, 1) }, CoinLine(LnL, 2, 30)),
            Chunk("m_climb_melon", DMed, 45, VisualAccent.None, "Ortada karpuz kamyonu: rampadan çık, tavanda koş; sonra iki yanda testi/karpuz.",
              new[] { Ob("melon", LnM, 6, 0), Ob("jug", LnL, 25, 1), Ob("watermelon", LnR, 25, 1) }, CoinLine(LnM, 2, 20), CoinLine(LnM, 22, 8)),
            Chunk("m_tractor", DMed, 45, VisualAccent.Column, "Sağda traktör (çıkılır), sonra solda bariyer; paralar traktörden ortaya iner.",
              new[] { Ob("tractor", LnR, 6, 0), Ob("bar", LnL, 26, 1) }, CoinLine(LnR, 2, 16), CoinCurve(LnR, LnM, 19, 6), CoinLine(LnM, 25, 6)),

            // ---------------- ZOR (800 m sonrası orta ile karışık; zordan sonra hep nefes) ----------------
            Chunk("h_train_left", DHard, 45, VisualAccent.None, "Tren: solda kamyon + iki minibüs (tavandan tavana), ortada minibüsler; sağ şerit yerden kaçış.",
              new[] { Ob("truck", LnL, 6), Ob("minibus", LnL, 14.5f), Ob("minibus", LnL, 22.4f), Ob("minibus", LnM, 8), Ob("minibus", LnM, 15.9f) },
              CoinLine(LnL, 1, 30)),
            Chunk("h_train_gate", DHard, 45, VisualAccent.None, "Tren sağda, üstünden çarşı kemeri geçer (tavanda koşarken kemerin altından geçilir). Sol şerit yerden kaçış.",
              new[] { Ob("truck", LnR, 6), Ob("minibus", LnR, 14.5f), Ob("minibus", LnR, 22.4f), Ob("minibus", LnM, 8), Ob("minibus", LnM, 15.9f), Ob("gate", LnM, 9) },
              CoinLine(LnR, 1, 30)),
            Chunk("h_double_ramp", DHard, 45, VisualAccent.None, "İki yanda rampalı araç + minibüs; orta yerden açık.",
              new[] { Ob("truck", LnL, 6), Ob("minibus", LnL, 14.5f), Ob("melon", LnR, 6), Ob("minibus", LnR, 14.5f) }, CoinLine(LnR, 1, 22)),
            Chunk("h_wall_slide", DHard, 45, VisualAccent.Lanterns, "Solda minibüs + ortada bariyer (sağ açık) → sağda sandık → ortada araba.",
              new[] { Ob("minibus", LnL, 6, 0), Ob("bar", LnM, 8, 0), Ob("crates", LnR, 24, 1), Ob("teacart", LnM, 37, 2) }, CoinLine(LnR, 2, 34)),
            Chunk("h_cart_maze", DHard, 45, VisualAccent.ShopFront, "Araba labirenti: sol+orta kapalı → sağ; orta+sağ kapalı → sol; sonda solda bariyer.",
              new[] { Ob("teacart", LnL, 6, 0), Ob("melonstall", LnM, 6, 0), Ob("simitcart", LnR, 20.6f, 1), Ob("grill", LnM, 20.6f, 1), Ob("bar", LnL, 35.4f, 2) },
              CoinLine(LnR, 2, 9), CoinCurve(LnR, LnL, 12, 7), CoinLine(LnL, 20, 12)),
            Chunk("h_moving_flank", DHard, 45, VisualAccent.None, "Sağda minibüs, sonra solda karşıdan gelen taksi; orta hep açık.",
              new[] { Ob("minibus", LnR, 6, 0), Ob("taxi_moving", LnL, 24.5f, 1) }, CoinLine(LnM, 2, 34)),
            Chunk("h_roof_hop", DHard, 45, VisualAccent.None, "Solda kamyon + minibüs, ortada otobüs: tavandan otobüse atlanır; sağ yerden açık.",
              new[] { Ob("truck", LnL, 6), Ob("minibus", LnL, 14.5f), Ob("citybus", LnM, 9) }, CoinLine(LnL, 1, 26)),
            Chunk("h_slide_jump", DHard, 45, VisualAccent.ShopFront, "Solda bariyer + sağda sandık → ortada araba → solda bariyer + sağda testi.",
              new[] { Ob("bar", LnL, 6, 0), Ob("crates", LnR, 6, 0), Ob("teacart", LnM, 19, 1), Ob("bar", LnL, 33, 2), Ob("jug", LnR, 33, 2) },
              CoinLine(LnM, 1, 14), CoinCurve(LnM, LnL, 15, 6), CoinLine(LnL, 21, 8)),

            // ---------------- ÇATI (portal evreni; araç ve kemer yok) ----------------
            RoofChunk(Chunk("r_crates", DEasy, 30, VisualAccent.None, "Çatı: ortada sandık.", new[] { Ob("crates", LnM, 12) }, CoinLine(LnM, 3, 20))),
            RoofChunk(Chunk("r_bar_jug", DEasy, 30, VisualAccent.None, "Çatı: solda bariyer, sağda testi.", new[] { Ob("bar", LnL, 9, 0), Ob("jug", LnR, 22, 1) }, CoinLine(LnM, 3, 22))),
            RoofChunk(WithMoving(Chunk("r_safe", DEasy, 30, VisualAccent.None, "Çatı: güvenli alan, S paralar.", new ObstaclePlacement[0], CoinCurve(LnR, LnL, 4, 22)), 4)),
            RoofChunk(Chunk("r_walls", DMed, 30, VisualAccent.None, "Çatı: iki yanda tezgâh, ortadan geç.",
              new[] { Ob("melonstall", LnL, 8, 0), Ob("simitcart", LnR, 8, 0) }, CoinLine(LnM, 2, 22))),
            RoofChunk(Chunk("r_weave", DMed, 45, VisualAccent.None, "Çatı zikzak: sol sandık → orta bariyer → sağ testi.",
              new[] { Ob("crates", LnL, 6, 0), Ob("bar", LnM, 19, 1), Ob("jug", LnR, 32, 2) },
              CoinLine(LnL, 1, 9), CoinCurve(LnL, LnM, 11, 6), CoinLine(LnM, 17, 5), CoinCurve(LnM, LnR, 24, 6), CoinLine(LnR, 30, 6))),
            RoofChunk(Chunk("r_mix", DHard, 45, VisualAccent.None, "Çatı: sol+orta kapalı → sağda bariyer → solda sandık.",
              new[] { Ob("teacart", LnL, 6, 0), Ob("busstop", LnM, 6, 0), Ob("bar", LnR, 22, 1), Ob("crates", LnL, 35, 2) }, CoinLine(LnR, 2, 16))),

            // ---------------- ÖZEL: portal meydanı (bölüm içinde portalDistance m'de) ----------------
            SpecialChunk(Chunk("portal_plaza", DMed, 90, VisualAccent.Arch, "Portal meydanı: iki yanda testi/karpuz, sonra durak + tezgâh orta şeride toplar, portal ortada.",
              new[] { Ob("jug", LnL, 26, 0), Ob("watermelon", LnR, 26, 0), Ob("busstop", LnL, 48, 1), Ob("melonstall", LnR, 48, 1), Ob("portal", LnM, 66, 2) },
              CoinLine(LnM, 20, 44))),
        };

        private static ChunkSpec WithMoving(ChunkSpec c, int n) { c.moving = n; return c; }
        private static ChunkSpec SpecialChunk(ChunkSpec c) { c.special = true; return c; }

        /// <summary>Chunk varlıklarını tablodan yazar ve doğrular. Döndürür: geçerli chunk'lar.</summary>
        private static LevelChunk[] BuildChunkAssets(ObstacleLibrary lib)
        {
            EnsureFolder(ChunkDir);
            var list = new List<LevelChunk>();
            int bad = 0;
            foreach (var s in ChunkTable())
            {
                var c = LoadOrCreate<LevelChunk>($"{ChunkDir}/Chunk_{s.id}.asset");
                c.id = s.id;
                c.notes = s.notes;
                c.difficulty = s.diff;
                c.world = s.world;
                c.length = s.length;
                c.accent = s.accent;
                c.movingDecor = s.moving;
                c.special = s.special;
                c.obstacles = s.obs ?? new ObstaclePlacement[0];
                c.coins = s.coins ?? new CoinRoute[0];
                EditorUtility.SetDirty(c);
                var errors = ChunkRules.Validate(c, lib);
                if (errors.Count > 0) { bad++; foreach (var e in errors) Debug.LogError("[Jumpline] Chunk kuralı: " + e); continue; }
                list.Add(c);
            }
            AssetDatabase.SaveAssets();
            WriteChunkDoc(list, lib);
            Debug.Log($"[Jumpline] Chunk'lar: {list.Count} geçerli, {bad} geçersiz " +
                      $"(sokak {list.Count(c => c.world == ChunkWorld.Street && !c.special)}, çatı {list.Count(c => c.world == ChunkWorld.Rooftop)})");
            return list.ToArray();
        }

        /// <summary>docs/Parkur_Chunklari.md: chunk ve engel listesi tablodan üretilir (belge koddan kaymaz).</summary>
        private static void WriteChunkDoc(List<LevelChunk> chunks, ObstacleLibrary lib)
        {
            string Lane(int l) => l < 0 ? "sol" : l > 0 ? "sağ" : "orta";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Parkur chunk'ları (otomatik — GameplaySceneBuilder.Chunks.cs tablosundan üretilir; elle düzenlemeyin)");
            sb.AppendLine();
            sb.AppendLine("Kurallar `ChunkRules` ile her kurulumda ve EditMode testinde doğrulanır. z = engelin ön kenarı (chunk başından m).");
            sb.AppendLine();
            sb.AppendLine("## Engeller (gerçek ölçü, m)");
            sb.AppendLine();
            sb.AppendLine("| Kimlik | Tür | Genişlik × Yükseklik × Boy | Çarpışma (yarı gen. / yük. veya tavan) | En kolay chunk | Not |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var d in lib.Definitions)
            {
                string note = (d.walkableTop ? "tavanı yürünür " : "") + (d.ramp ? "rampalı " : "") + (d.variants.Length > 0 ? $"{d.variants.Length} renk çeşidi " : "")
                              + (d.kind == ObstacleKind.MovingObstacle ? $"{d.moveSpeed} m/sn gelir" : "");
                sb.AppendLine($"| {d.id} | {d.kind} | {d.size.x:0.00} × {d.size.y:0.00} × {d.size.z:0.00} | {d.collider.x:0.00} / {d.collider.y:0.00} | {d.minDifficulty} | {note.Trim()} |");
            }
            foreach (var world in new[] { ChunkWorld.Street, ChunkWorld.Rooftop })
            {
                sb.AppendLine();
                sb.AppendLine(world == ChunkWorld.Street ? "## Sokak chunk'ları" : "## Çatı chunk'ları (portal evreni)");
                sb.AppendLine();
                sb.AppendLine("| # | Kimlik | Zorluk | Boy | Engel yerleşimi | Para rotası | Vurgu / hareketli süs | Tasarım notu |");
                sb.AppendLine("|---|---|---|---|---|---|---|---|");
                int n = 0;
                foreach (var c in chunks.Where(c => c.world == world).OrderBy(c => c.special).ThenBy(c => c.difficulty))
                {
                    string obs = c.obstacles.Length == 0 ? "— (güvenli alan)" :
                        string.Join("<br>", c.obstacles.Select(p => $"g{p.group}: {p.id} {Lane(p.lane)} @{p.z:0.#}"));
                    string coins = string.Join("<br>", c.coins.Select(r => r.shape == CoinShape.SCurve
                        ? $"S {Lane(r.lane)}→{Lane(r.toLane)} @{r.z:0.#} ({r.length:0} m)" : $"düz {Lane(r.lane)} @{r.z:0.#} ({r.length:0} m)"));
                    sb.AppendLine($"| {++n} | {c.id}{(c.special ? " (özel)" : "")} | {c.difficulty} | {c.length:0} | {obs} | {coins} | {c.accent} / {c.movingDecor} | {c.notes} |");
                }
            }
            System.IO.File.WriteAllText("docs/Parkur_Chunklari.md", sb.ToString(), new System.Text.UTF8Encoding(false));
        }

        // ---------------------------------------------------------------------
        // Görsel vurgular + hareketli süsler (prefab; yolun dışında / üstünde)
        // ---------------------------------------------------------------------

        private static GameObject SaveDecorPrefab(GameObject go, string path)
        {
            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return saved;
        }

        /// <summary>Meshy modelinden vurgu prefab'ı: yükseklik h, kökte yaw; kökün konumu = önerilen yerel konum (ambient için).</summary>
        private static GameObject MeshyDecorPrefab(string name, string model, float h, float yaw, Vector3 rootPos, AmbientMotion.Mode? motion = null,
                                                   float amount = 0f, float speed = 0f, Vector3 axis = default)
        {
            var root = new GameObject(name);
            var vis = PlaceMeshy(model, root.transform, Vector3.zero, yaw, h);
            if (vis == null) { Object.DestroyImmediate(root); return null; }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;   // süs gölge atmaz (mobil)
            if (motion.HasValue) root.AddComponent<AmbientMotion>().Configure(motion.Value, amount, speed, axis);
            root.transform.position = rootPos;
            return SaveDecorPrefab(root, $"{AccentDir}/{name}.prefab");
        }

        private static ChunkSpawner.AccentEntry[] BuildAccents()
        {
            EnsureFolder(AccentDir);
            ChunkSpawner.AccentEntry A(VisualAccent a, GameObject p, Vector3 off, bool mirror) =>
                new ChunkSpawner.AccentEntry { accent = a, prefab = p, offset = off, mirrored = mirror };
            var arch = MeshyDecorPrefab("Accent_Arch", "M04_BazaarArch", 1f, 180f, Vector3.zero);
            if (arch != null)
            {   // kemer yolu aşar: 9.5 m genişlik (iç tavan 3.7 m; yalnız araçsız chunk'larda)
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(arch);
                Bounds b = VisualBounds(inst.transform);
                inst.transform.GetChild(0).localScale *= 9.5f / Mathf.Max(0.1f, b.size.x);
                b = VisualBounds(inst.transform);
                inst.transform.GetChild(0).position += new Vector3(-b.center.x, -b.min.y, -b.center.z);
                SinkFloorSlab(inst.transform.GetChild(0).gameObject, "M04_BazaarArch");   // döşeme yolun altına
                PrefabUtility.ApplyPrefabInstance(inst, InteractionMode.AutomatedAction);
                Object.DestroyImmediate(inst);
            }
            var lantern = MeshyDecorPrefab("Accent_Lantern", "M13_BridgeLantern", 2.6f, 90f, Vector3.zero, AmbientMotion.Mode.Sway, 1.2f, 0.35f, Vector3.forward);
            var stall = MeshyDecorPrefab("Accent_Stall", "M06_StallDecor", 1.1f, 90f, Vector3.zero);
            var minaret = MeshyDecorPrefab("Accent_Minaret", "M10_Minaret", 16f, 0f, Vector3.zero);
            var pier = MeshyDecorPrefab("Accent_BridgePier", "M12_BridgeArch", 10f, 90f, Vector3.zero);
            var lights = MeshyDecorPrefab("Accent_StringLights", "M05_StringLights", 1.2f, 0f, Vector3.zero, AmbientMotion.Mode.Sway, 2f, 0.25f, Vector3.right);
            return new[]
            {
                A(VisualAccent.Arch, arch, Vector3.zero, false),
                A(VisualAccent.Lanterns, lantern, new Vector3(4.7f, 0f, -6f), true),
                A(VisualAccent.Lanterns, lantern, new Vector3(4.7f, 0f, 6f), true),
                A(VisualAccent.ShopFront, stall, new Vector3(4.9f, 0f, 0f), true),
                A(VisualAccent.Column, minaret, new Vector3(13f, 0f, 4f), false),
                A(VisualAccent.BridgePier, pier, new Vector3(15f, -1f, 0f), true),
                A(VisualAccent.Sign, lights, new Vector3(0f, 5.4f, 0f), false),
            }.Where(a => a.prefab != null).ToArray();
        }

        /// <summary>Hareketli süsler: süzülen kuş sürüsü, sallanan flamalar, sallanan fener (fizik yok).</summary>
        private static GameObject[] BuildAmbientDecor()
        {
            EnsureFolder(AccentDir);
            EnsureFolder(AmbientArtDir);
            var list = new List<GameObject>();
            // kuş sürüsü: 3 koyu "V" kanat, yolun yanında 9–12 m yüksekte daire çizer
            var birdMesh = SaveMesh(BirdMesh(), $"{AmbientArtDir}/BirdMesh.asset");
            var birdMat = AmbientMat("Ambient_Bird", Hex("#2B2A33"), false);
            var flock = new GameObject("Ambient_Birds");
            for (int i = 0; i < 3; i++)
            {
                var b = new GameObject("Bird" + i, typeof(MeshFilter), typeof(MeshRenderer));
                b.transform.SetParent(flock.transform, false);
                b.transform.localPosition = new Vector3(i * 0.9f - 0.9f, i % 2 * 0.5f, -i * 0.8f);
                b.transform.localScale = Vector3.one * (0.55f + 0.1f * i);
                b.GetComponent<MeshFilter>().sharedMesh = birdMesh;
                var mr = b.GetComponent<MeshRenderer>();
                mr.sharedMaterial = birdMat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                b.AddComponent<AmbientMotion>().Configure(AmbientMotion.Mode.Sway, 25f, 2.2f + 0.3f * i, Vector3.forward);   // kanat çırpma
            }
            flock.AddComponent<AmbientMotion>().Configure(AmbientMotion.Mode.Glide, 3.5f, 0.07f, Vector3.up);
            flock.transform.position = new Vector3(9f, 10f, 0f);
            list.Add(SaveDecorPrefab(flock, $"{AccentDir}/Ambient_Birds.prefab"));
            // flamalar: direk + üçgen bayrak (kırmızı / turkuaz / safran), kaldırım kenarında rüzgârda sallanır
            var flagMesh = SaveMesh(FlagMesh(), $"{AmbientArtDir}/FlagMesh.asset");
            var poleMat = AmbientMat("Ambient_Pole", Hex("#5B4636"), false);
            foreach (var (name, hex) in new[] { ("Red", "#D7263D"), ("Turquoise", "#1BB5B0"), ("Saffron", "#F2A51A") })
            {
                var root = new GameObject("Ambient_Flag" + name);
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(pole.GetComponent<Collider>());
                pole.transform.SetParent(root.transform, false);
                pole.transform.localScale = new Vector3(0.07f, 1.6f, 0.07f);
                pole.transform.localPosition = new Vector3(0f, 1.6f, 0f);
                pole.GetComponent<MeshRenderer>().sharedMaterial = poleMat;
                var flag = new GameObject("Flag", typeof(MeshFilter), typeof(MeshRenderer));
                flag.transform.SetParent(root.transform, false);
                flag.transform.localPosition = new Vector3(0f, 3.1f, 0f);
                flag.GetComponent<MeshFilter>().sharedMesh = flagMesh;
                flag.GetComponent<MeshRenderer>().sharedMaterial = AmbientMat("Ambient_Flag" + name, Hex(hex), true);
                flag.AddComponent<AmbientMotion>().Configure(AmbientMotion.Mode.Sway, 14f, 0.9f, Vector3.up);
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                root.transform.position = new Vector3(4.75f, 0f, 0f);
                list.Add(SaveDecorPrefab(root, $"{AccentDir}/Ambient_Flag{name}.prefab"));
            }
            // asılı fener: kaldırım üstünde yavaş sallanır
            var hang = MeshyDecorPrefab("Ambient_Lantern", "M13_BridgeLantern", 1.9f, 90f, new Vector3(4.8f, 0f, 0f), AmbientMotion.Mode.Sway, 2.5f, 0.45f, Vector3.forward);
            if (hang != null) list.Add(hang);
            return list.ToArray();
        }

        private static Mesh SaveMesh(Mesh m, string path)
        {
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old != null) { old.Clear(); EditorUtility.CopySerialized(m, old); return old; }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        private static Material AmbientMat(string name, Color c, bool doubleSided)
        {
            string path = $"{AmbientArtDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", 0.2f);
            mat.SetFloat("_Cull", doubleSided ? 0f : 2f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Mesh BirdMesh()
        {
            var m = new Mesh { name = "BirdMesh" };
            m.vertices = new[] { new Vector3(0f, 0f, 0.25f), new Vector3(-0.7f, 0.18f, -0.15f), new Vector3(0f, 0.02f, -0.2f), new Vector3(0.7f, 0.18f, -0.15f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Mesh FlagMesh()
        {
            var m = new Mesh { name = "FlagMesh" };
            m.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, -0.55f, 0f), new Vector3(0f, -0.28f, -0.95f) };
            m.triangles = new[] { 0, 2, 1, 0, 1, 2 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ---------------------------------------------------------------------
        // Sahne: sokak + çatı chunk üreticileri
        // ---------------------------------------------------------------------

        /// <summary>Engel kütüphanesi, chunk'lar, vurgular ve iki chunk üreticisi (sokak / çatı).</summary>
        private static (ChunkSpawner street, ChunkSpawner roof) BuildObstacles(Transform runner, WorldSwitcher switcher, Portal[] portals)
        {
            var lib = BuildObstacleLibrary();
            var chunks = BuildChunkAssets(lib);
            var accents = BuildAccents();
            var ambient = BuildAmbientDecor();
            var player = runner.GetComponent<Jumpline.Player.PlayerController>();
            ChunkSpawner Make(string name, bool rooftop)
            {
                var go = new GameObject(name);
                var dir = go.AddComponent<DifficultyDirector>();
                dir.Configure(lib, chunks);
                EditorUtility.SetDirty(dir);
                var sp = go.AddComponent<ChunkSpawner>();
                Set(sp, "player", player);
                Set(sp, "switcher", switcher);
                Set(sp, "director", dir);
                var so = new SerializedObject(sp);
                so.FindProperty("rooftop").boolValue = rooftop;
                var acc = so.FindProperty("accents");
                var useAccents = rooftop ? new ChunkSpawner.AccentEntry[0] : accents;
                acc.arraySize = useAccents.Length;
                for (int i = 0; i < useAccents.Length; i++)
                {
                    var e = acc.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("accent").enumValueIndex = (int)useAccents[i].accent;
                    e.FindPropertyRelative("prefab").objectReferenceValue = useAccents[i].prefab;
                    e.FindPropertyRelative("offset").vector3Value = useAccents[i].offset;
                    e.FindPropertyRelative("mirrored").boolValue = useAccents[i].mirrored;
                }
                var amb = so.FindProperty("ambientDecor");
                var useAmbient = rooftop ? ambient.Where(a => a.name == "Ambient_Birds" || a.name.StartsWith("Ambient_Flag")).ToArray() : ambient;
                amb.arraySize = useAmbient.Length;
                for (int i = 0; i < useAmbient.Length; i++) amb.GetArrayElementAtIndex(i).objectReferenceValue = useAmbient[i];
                if (!rooftop) so.FindProperty("coursePortal").objectReferenceValue = portals != null && portals.Length > 0 ? portals[0] : null;
                so.ApplyModifiedPropertiesWithoutUndo();
                return sp;
            }
            var street = Make("StreetChunks", false);
            var roof = Make("RooftopChunks", true);
            if (portals != null && portals.Length > 0 && portals[0] != null)
            {
                var pso = new SerializedObject(portals[0]);
                pso.FindProperty("courseControlled").boolValue = true;   // sokak portalı portal meydanı chunk'ında
                pso.ApplyModifiedPropertiesWithoutUndo();
            }
            return (street, roof);
        }
    }
}
