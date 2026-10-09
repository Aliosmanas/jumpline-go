using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Parçalı Sur kedisinin koşusu (Cat.fbx: Body, Head, Tail, LegFL/FR/BL/BR): çapraz bacak salınımı, gövde inip kalkar,
    /// kuyruk ve baş hafif sallanır. Hız ölçeği DailyMission'dan bağımsız sabit tempo (kedi küçük adımlarla hızlı koşar).
    /// </summary>
    public class CatRunner : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float stepsPerSecond = 3.4f;
        [SerializeField, Range(0f, 80f)] private float legSwing = 45f;
        [SerializeField, Min(0f)] private float bob = 0.04f;

        private Transform body, head, tail;
        private Transform[] legs;
        private Quaternion[] legRest;
        private Vector3 bodyRest;
        private Quaternion headRest, tailRest;

        private void Awake()
        {
            body = Find("Body");
            head = Find("Head");
            tail = Find("Tail");
            legs = new[] { Find("LegFL"), Find("LegBR"), Find("LegFR"), Find("LegBL") };   // çapraz çiftler
            legRest = new Quaternion[legs.Length];
            for (int i = 0; i < legs.Length; i++) if (legs[i] != null) legRest[i] = legs[i].localRotation;
            if (body != null) bodyRest = body.localPosition;
            if (head != null) headRest = head.localRotation;
            if (tail != null) tailRest = tail.localRotation;
        }

        private Transform Find(string n)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        private void Update()
        {
            float ph = Time.time * stepsPerSecond * Mathf.PI * 2f;
            for (int i = 0; i < legs.Length; i++)
            {
                if (legs[i] == null) continue;
                float s = Mathf.Sin(ph + (i < 2 ? 0f : Mathf.PI));
                legs[i].localRotation = legRest[i] * Quaternion.Euler(s * legSwing, 0f, 0f);
            }
            if (body != null) body.localPosition = bodyRest + Vector3.up * (Mathf.Abs(Mathf.Sin(ph)) * bob);
            if (head != null) head.localRotation = headRest * Quaternion.Euler(Mathf.Sin(ph * 2f) * 4f, 0f, 0f);
            if (tail != null) tail.localRotation = tailRest * Quaternion.Euler(0f, 0f, Mathf.Sin(ph * 0.5f) * 18f);
        }
    }
}
