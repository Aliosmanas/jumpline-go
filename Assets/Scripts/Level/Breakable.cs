using UnityEngine;

namespace Jumpline.Level
{
    /// <summary>
    /// Çarpılınca parçaları savrulan engel (sandık yığını, testiler, çuval…). Fizik motoru kullanmaz: her parça kendi
    /// hızı, dönüşü ve yerçekimiyle uçar, zemine çarpıp seker, durur. Havuza dönünce (OnDisable) parçalar yerine kurulur.
    /// Parçalar: doğrudan alt objeler (sandık yığını 3 sandık; tek parçalı dekorda dekorun kendisi devrilir).
    /// </summary>
    public class Breakable : MonoBehaviour
    {
        [SerializeField] private Vector2 forwardSpeed = new Vector2(2.5f, 5.5f);
        [SerializeField] private Vector2 upSpeed = new Vector2(2.0f, 4.5f);
        [SerializeField, Min(0f)] private float sideSpeed = 2.2f;
        [SerializeField, Min(0f)] private float spin = 420f;
        [SerializeField, Min(0f)] private float gravity = 22f;
        [SerializeField, Range(0f, 1f)] private float bounce = 0.35f;
        [SerializeField, Min(0.1f)] private float lifetime = 3.5f;

        private Transform[] pieces;
        private Vector3[] startPos;
        private Quaternion[] startRot;
        private Vector3[] velocity;
        private Vector3[] angular;
        private float brokenAt = -1f;

        public bool IsBroken => brokenAt >= 0f;

        private void Awake()
        {
            int n = transform.childCount;
            pieces = new Transform[n];
            startPos = new Vector3[n];
            startRot = new Quaternion[n];
            velocity = new Vector3[n];
            angular = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                pieces[i] = transform.GetChild(i);
                startPos[i] = pieces[i].localPosition;
                startRot[i] = pieces[i].localRotation;
            }
        }

        /// <summary>Koşucu çarptı: parçaları koşu yönünde savur.</summary>
        public void Break(float runnerSpeed)
        {
            if (IsBroken || pieces == null) return;
            brokenAt = Time.time;
            float boost = Mathf.Clamp(runnerSpeed / 9f, 0.8f, 1.8f);
            for (int i = 0; i < pieces.Length; i++)
            {
                float side = (pieces[i].localPosition.x >= 0f ? 1f : -1f) * Random.Range(0.3f, 1f) * sideSpeed;
                velocity[i] = new Vector3(side, Random.Range(upSpeed.x, upSpeed.y), Random.Range(forwardSpeed.x, forwardSpeed.y) * boost);
                angular[i] = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * spin;
            }
        }

        private void Update()
        {
            if (!IsBroken) return;
            float dt = Time.deltaTime;
            if (Time.time - brokenAt > lifetime) return;
            float ground = transform.position.y;
            for (int i = 0; i < pieces.Length; i++)
            {
                Transform t = pieces[i];
                velocity[i].y -= gravity * dt;
                Vector3 p = t.position + velocity[i] * dt;
                if (p.y < ground)
                {
                    p.y = ground;
                    velocity[i].y = -velocity[i].y * bounce;
                    velocity[i].x *= 0.6f;
                    velocity[i].z *= 0.6f;
                    angular[i] *= 0.55f;
                }
                t.position = p;
                t.Rotate(angular[i] * dt, Space.World);
            }
        }

        private void OnDisable()
        {
            if (pieces == null) return;
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i].localPosition = startPos[i];
                pieces[i].localRotation = startRot[i];
            }
            brokenAt = -1f;
        }
    }
}
