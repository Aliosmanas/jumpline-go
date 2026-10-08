using System.Collections.Generic;
using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Engel / yüzey: koşu yönünde (+Z) yükseklik profili olan dikdörtgen taban. Fizik kullanılmaz; PlayerController
    /// her karede <see cref="ObstacleField.SurfaceAt"/> ile altındaki yüzeyi sorar.
    /// Katı: yüksekliği aşamayan koşucu çarpar (ölür), üstüne çıkan koşar (kasalar, kamyon tavanı, rampa).
    /// Boşluk: zemini aşağı indirir (çatı arası) — düşen koşucu ölür.
    /// Tavan: alt kenarı (yükseklik) koşucunun kafasına çarpar (sur kapısı — kamyon tavanındaki koşucu ölür).
    /// Döndürme desteklenmez: engeller hep +Z'ye bakar.
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        public enum Kind { Solid, Hole, Ceiling }

        [System.Serializable]
        public struct Segment
        {
            [Tooltip("Yerel z başlangıç / bitiş (m).")] public float z0, z1;
            [Tooltip("Yerel yükseklik z0'da / z1'de (rampa için farklı).")] public float h0, h1;
        }

        [SerializeField] private Kind kind = Kind.Solid;
        [Tooltip("Yarı genişlik (x, m).")]
        [SerializeField, Min(0.05f)] private float halfWidth = 0.7f;
        [SerializeField] private Segment[] segments = { new Segment { z0 = -0.4f, z1 = 0.4f, h0 = 1f, h1 = 1f } };
        [Tooltip("Tavan kalınlığı (m): 0 = gökyüzüne kadar (sur kapısı); >0 = üstünden de geçilebilen bariyer (kalas).")]
        [SerializeField, Min(0f)] private float thickness;

        public float Thickness => thickness;

        public Kind Type => kind;
        public float MinZ => transform.position.z + (segments.Length > 0 ? segments[0].z0 : 0f);
        public float MaxZ => transform.position.z + (segments.Length > 0 ? segments[segments.Length - 1].z1 : 0f);

        private void OnEnable() => ObstacleField.Register(this);
        private void OnDisable() => ObstacleField.Unregister(this);

        /// <summary>(x,z) tabanın içindeyse dünya yüksekliğini verir.</summary>
        public bool TryHeight(float x, float z, out float height)
        {
            Vector3 o = transform.position;
            height = 0f;
            if (Mathf.Abs(x - o.x) > halfWidth) return false;
            float lz = z - o.z;
            for (int i = 0; i < segments.Length; i++)
            {
                Segment s = segments[i];
                if (lz < s.z0 || lz > s.z1) continue;
                float t = s.z1 > s.z0 ? (lz - s.z0) / (s.z1 - s.z0) : 0f;
                height = o.y + Mathf.Lerp(s.h0, s.h1, t);
                return true;
            }
            return false;
        }

        public void Configure(Kind type, float width, Segment[] segs, float ceilingThickness = 0f)
        {
            thickness = ceilingThickness;
            kind = type;
            halfWidth = width;
            segments = segs;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = kind == Kind.Solid ? new Color(1f, 0.3f, 0.3f, 0.6f) : kind == Kind.Hole ? new Color(0.3f, 0.6f, 1f, 0.6f) : new Color(1f, 0.8f, 0.2f, 0.6f);
            Vector3 o = transform.position;
            foreach (Segment s in segments)
            {
                float h = Mathf.Max(s.h0, s.h1);
                Gizmos.DrawWireCube(o + new Vector3(0f, h * 0.5f, (s.z0 + s.z1) * 0.5f), new Vector3(halfWidth * 2f, Mathf.Abs(h), s.z1 - s.z0));
            }
        }
#endif
    }

    /// <summary>Etkin engellerin listesi ve koşucunun altındaki yüzey sorgusu.</summary>
    public static class ObstacleField
    {
        private static readonly List<Obstacle> Active = new List<Obstacle>(64);

        public static void Register(Obstacle o)
        {
            if (!Active.Contains(o)) Active.Add(o);
        }

        public static void Unregister(Obstacle o) => Active.Remove(o);

        /// <summary>
        /// (x,z) noktasındaki yürünebilir yüzey: önce zemin (boşluklar aşağı indirir), sonra katılar (en yüksek).
        /// </summary>
        public static float SurfaceAt(float x, float z, float ground)
        {
            float surface = ground;
            for (int i = 0; i < Active.Count; i++)
                if (Active[i].Type == Obstacle.Kind.Hole && Active[i].TryHeight(x, z, out float h)) surface = Mathf.Min(surface, h);
            for (int i = 0; i < Active.Count; i++)
                if (Active[i].Type == Obstacle.Kind.Solid && Active[i].TryHeight(x, z, out float h)) surface = Mathf.Max(surface, h);
            return surface;
        }

        /// <summary>Gövde (ayak..kafa) bir tavanın/bariyerin kalınlığıyla çakışıyor mu (kafa çarpar).</summary>
        public static bool HitsCeiling(float x, float z, float feet, float head)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                Obstacle o = Active[i];
                if (o.Type != Obstacle.Kind.Ceiling || !o.TryHeight(x, z, out float bottom)) continue;
                float top = o.Thickness > 0f ? bottom + o.Thickness : float.PositiveInfinity;
                if (head > bottom && feet < top) return true;
            }
            return false;
        }

        /// <summary>(x,z) noktasındaki en alçak tavan (yoksa +∞).</summary>
        public static float CeilingAt(float x, float z)
        {
            float ceiling = float.PositiveInfinity;
            for (int i = 0; i < Active.Count; i++)
                if (Active[i].Type == Obstacle.Kind.Ceiling && Active[i].TryHeight(x, z, out float h)) ceiling = Mathf.Min(ceiling, h);
            return ceiling;
        }

        /// <summary>(x,z) noktasındaki en yüksek katı engel (çarpılan engel; yoksa null).</summary>
        public static Obstacle SolidAt(float x, float z)
        {
            Obstacle best = null;
            float bestH = float.NegativeInfinity;
            for (int i = 0; i < Active.Count; i++)
            {
                if (Active[i].Type != Obstacle.Kind.Solid || !Active[i].TryHeight(x, z, out float h) || h <= bestH) continue;
                best = Active[i];
                bestH = h;
            }
            return best;
        }

        /// <summary>z aralığında (x şeridinde) katı engel var mı.</summary>
        public static bool AnySolid(float x, float halfWidth, float z0, float z1)
        {
            for (int i = 0; i < Active.Count; i++)
            {
                Obstacle o = Active[i];
                if (o.Type != Obstacle.Kind.Solid) continue;
                if (Mathf.Abs(o.transform.position.x - x) > halfWidth) continue;
                if (o.MaxZ < z0 || o.MinZ > z1) continue;
                return true;
            }
            return false;
        }
    }
}
