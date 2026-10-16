using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Engelin hemen önünde yerde parlayan işaret — oyuncu ne yapacağını uzaktan okusun:
    /// renk kodu (16 Eki 2026, sabit): turuncu ▲ zıpla · turkuaz ▼ kay · kırmızı ✕ şerit değiştir · sarı-siyah hareketli ·
    /// yeşil ▲ çık (rampalı araç: üstünden koşulur). Chunk sisteminde hareket engel tanımından gelir (ApplyAction);
    /// tanımsız engelde geometriden sınıflanır (Apply). Engel havuzdan her yerleşişte yeniden hesaplanır.
    /// </summary>
    public static class ObstacleHint
    {
        public enum Action { None, Jump, Slide, Block, Climb, Moving }

        /// <summary>Rampa: koşucunun geldiği uçta alçaktan başlar, basamaksız (her adım ≤ 0.4 m) en az 1 m yükselir.</summary>
        public static bool IsRamp(Obstacle o)
        {
            var segs = o.Segments;
            if (segs == null || segs.Length == 0 || Mathf.Min(segs[0].h0, segs[0].h1) > 0.6f) return false;
            float prev = segs[0].h0, top = prev;
            foreach (var s in segs)
            {
                if (Mathf.Abs(s.h0 - prev) > 0.4f) break;
                top = Mathf.Max(top, Mathf.Max(s.h0, s.h1));
                prev = s.h1;
            }
            return top > 1.0f;
        }

        private static Mesh quad;

        /// <summary>Engelin türünden ve yüksekliğinden gereken hareket.</summary>
        public static Action Classify(Obstacle o, float jumpClear)
        {
            if (o == null) return Action.None;
            if (o.Type == Obstacle.Kind.Hole) return Action.Jump;
            if (o.Type == Obstacle.Kind.Ceiling) return Action.Slide;
            var segs = o.Segments;
            if (segs == null || segs.Length == 0) return Action.None;
            if (IsRamp(o)) return Action.Climb;   // rampa: üstüne çıkılır
            float top = 0f;
            foreach (var s in segs) top = Mathf.Max(top, Mathf.Max(s.h0, s.h1));
            return top <= jumpClear ? Action.Jump : Action.Block;
        }

        /// <summary>İşareti oluşturur/günceller (engelin çocuğu; havuzla birlikte taşınır).</summary>
        public static void Apply(Obstacle o, float jumpClear, Material jump, Material slide, Material block, float length, float gap, Material climb = null)
        {
            Action a = Classify(o, jumpClear);
            Material m = a == Action.Jump ? jump : a == Action.Slide ? slide : a == Action.Climb ? climb : a == Action.Block ? block : null;
            ApplyAction(o, a, m, length, gap);
        }

        /// <summary>Hareketi belli işareti koyar (m null ya da hareket yoksa işaret gizlenir).</summary>
        public static void ApplyAction(Obstacle o, Action a, Material m, float length, float gap)
        {
            Transform t = o.transform.Find("Hint");
            if (a == Action.None || m == null)
            {
                if (t != null) t.gameObject.SetActive(false);
                return;
            }
            MeshRenderer mr;
            if (t == null)
            {
                var go = new GameObject("Hint", typeof(MeshFilter), typeof(MeshRenderer));
                t = go.transform;
                t.SetParent(o.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = Quad();
                mr = go.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
            else mr = t.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            float z0 = o.Segments.Length > 0 ? o.Segments[0].z0 : 0f;
            float w = Mathf.Clamp(o.HalfWidth * 2f, 0.9f, 1.5f);
            t.localPosition = new Vector3(0f, 0.035f, z0 - gap - length * 0.5f);
            t.localRotation = Quaternion.Euler(90f, 0f, 0f);
            t.localScale = new Vector3(w, length, 1f);
            t.gameObject.SetActive(true);
        }

        private static Mesh Quad()
        {
            if (quad != null) return quad;
            quad = new Mesh { name = "HintQuad" };
            quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
            quad.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            quad.RecalculateNormals();
            quad.RecalculateBounds();
            return quad;
        }
    }
}
