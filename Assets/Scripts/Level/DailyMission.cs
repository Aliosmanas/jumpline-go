using System;
using System.Collections.Generic;
using Jumpline.Core;
using Jumpline.Player;
using Jumpline.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.Level
{
    /// <summary>
    /// Günlük görev (çaycı amca): güne göre bir hırsız seçilir (kapkaççı ↔ Sur kedisi). Başta çaycı amca konuşur
    /// (dünya donuk); hırsız önde kaçar, arada bir çaldığını düşürür (ulaşılabilir yere). Hedef sayıya ulaşınca
    /// çaycı amca teşekkür eder ve bölüm biter (LevelFlowController günlük ödülü verir). Ölünce elmasla devam edilebilir.
    /// </summary>
    public class DailyMission : MonoBehaviour
    {
        [Serializable]
        public struct Mission
        {
            public string id;
            [Tooltip("Konuşmacı kimliği (DialogueBox).")] public string thiefSpeaker;
            public GameObject thief;
            public GameObject collectible;
            public Sprite icon;
            [Min(1)] public int target;
            [Tooltip("Görev adı (HUD, çeviri anahtarı).")] public string title;
            public string[] intro;      // çaycı / hırsız satırları sırayla: "speaker|metin"
        }

        [SerializeField] private PlayerController player;
        [SerializeField] private LevelFlowController flow;
        [SerializeField] private DialogueBox dialogue;
        [SerializeField] private Mission[] missions;

        [Header("HUD")]
        [SerializeField] private GameObject hudRoot;
        [SerializeField] private Image hudIcon;
        [SerializeField] private TMP_Text hudCount;
        [SerializeField] private TMP_Text hudTitle;

        [Header("Hırsız")]
        [SerializeField, Min(5f)] private float gap = 15f;
        [SerializeField] private Vector2 laneChangeInterval = new Vector2(1.6f, 3.4f);
        [SerializeField, Min(1f)] private float laneSharpness = 10f;
        [SerializeField] private Vector2 dropSpacing = new Vector2(55f, 85f);
        [SerializeField, Min(0.3f)] private float hoverHeight = 1.0f;
        [SerializeField, Min(0.2f)] private float pickupRadius = 1.05f;
        [SerializeField] private ParticleSystem pickupFx;

        private Mission m;
        private int collected;
        private int lane;
        private float nextLane;
        private float nextDrop;
        private float y, vy;
        private bool done;
        private readonly List<Transform> items = new List<Transform>();
        private readonly Stack<Transform> pool = new Stack<Transform>();

        public int Collected => collected;
        public int Target => m.target;
        public bool Active => isActiveAndEnabled && !done;

        /// <summary>Bugünün görevi (yılın gününe göre dönüşümlü).</summary>
        public static int TodayIndex(int count) => count <= 0 ? 0 : DateTime.Now.DayOfYear % count;

        private void Awake()
        {
            bool daily = GameSession.Mode == RunMode.Daily && missions != null && missions.Length > 0;
            int pick = daily ? TodayIndex(missions.Length) : -1;
            for (int i = 0; missions != null && i < missions.Length; i++)
                if (missions[i].thief != null) missions[i].thief.SetActive(i == pick);
            if (hudRoot != null) hudRoot.SetActive(daily);
            if (!daily) { enabled = false; return; }
            m = missions[pick];
        }

        private void Start()
        {
            if (!enabled) return;
            y = player.GroundHeight;
            nextLane = Time.time + 2f;
            nextDrop = player.Distance + 35f;
            RefreshHud();
            if (dialogue != null && m.intro != null && m.intro.Length > 0 && !IntroSequence.Playing)
            {
                Time.timeScale = 0f;
                var lines = new List<DialogueBox.Line>();
                foreach (string s in m.intro)
                {
                    int bar = s.IndexOf('|');
                    lines.Add(bar > 0 ? new DialogueBox.Line(s.Substring(0, bar), s.Substring(bar + 1)) : new DialogueBox.Line("cayci", s));
                }
                Jumpline.Audio.AudioManager.Play("tea");
                dialogue.Play(lines, () => Time.timeScale = 1f);
            }
        }

        private void Update()
        {
            if (done || player == null || m.thief == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            MoveThief(dt);
            if (!player.IsDead && player.Distance >= nextDrop && collected + items.Count < m.target + 2)
                nextDrop = player.Distance + (TryDrop() ? UnityEngine.Random.Range(dropSpacing.x, dropSpacing.y) : 4f);
            UpdateItems();
        }

        private void MoveThief(float dt)
        {
            Transform t = m.thief.transform;
            Vector3 pp = player.transform.position;
            if (Time.time >= nextLane)
            {
                lane = lane == 0 ? (UnityEngine.Random.value < 0.5f ? -1 : 1) : (UnityEngine.Random.value < 0.6f ? 0 : -lane);
                nextLane = Time.time + UnityEngine.Random.Range(laneChangeInterval.x, laneChangeInterval.y);
            }
            float targetX = player.LaneOriginX + lane * player.LaneWidth;
            float x = Mathf.Lerp(t.position.x, targetX, 1f - Mathf.Exp(-laneSharpness * dt));
            if (Mathf.Abs(t.position.x - targetX) > player.LaneWidth * 3f) x = targetX;
            float z = pp.z + gap;
            float g = player.GroundHeight;
            float surface = Mathf.Max(ObstacleField.SurfaceAt(x, z, g), ObstacleField.SurfaceAt(x, z + 1.2f, g), g);
            if (surface > y + 0.25f && vy <= 0f) vy = Mathf.Sqrt(2f * 30f * (surface - y + 0.4f));
            if (vy != 0f || y > surface)
            {
                vy -= 30f * dt;
                y += vy * dt;
                if (y <= surface) { y = surface; vy = 0f; }
            }
            else y = surface;
            t.position = new Vector3(x, y, z);
        }

        private bool TryDrop()
        {
            Transform t = m.thief.transform;
            float z = t.position.z - 1f;
            float x = player.LaneOriginX + lane * player.LaneWidth;
            float g = player.GroundHeight;
            if (ObstacleField.AnySolid(x, 0.9f, z - 2.5f, z + 2.5f)) return false;
            if (Mathf.Abs(ObstacleField.SurfaceAt(x, z, g) - g) > 0.05f || ObstacleField.HitsCeiling(x, z, g, g + 2.2f)) return false;
            Transform it = pool.Count > 0 ? pool.Pop() : Instantiate(m.collectible, transform).transform;
            it.gameObject.SetActive(true);
            it.position = new Vector3(x, g + hoverHeight, z);
            items.Add(it);
            return true;
        }

        private void UpdateItems()
        {
            Vector3 p = player.transform.position;
            float head = player.IsSliding ? 1.0f : 1.7f;
            float bob = Mathf.Sin(Time.time * 3f) * 0.1f;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                Transform it = items[i];
                Vector3 c = it.position;
                if (c.z < p.z - 8f || Mathf.Abs(c.x - player.LaneOriginX) > 6f) { Release(i); continue; }
                c.y = player.GroundHeight + hoverHeight + bob;
                it.position = c;
                it.Rotate(0f, 140f * Time.deltaTime, 0f, Space.World);
                if (player.IsDead) continue;
                if (Mathf.Abs(c.x - p.x) < pickupRadius && Mathf.Abs(c.z - p.z) < pickupRadius && c.y > p.y - 0.4f && c.y < p.y + head + 0.4f)
                {
                    if (pickupFx != null) { pickupFx.transform.position = c; pickupFx.Emit(24); }
                    Release(i);
                    collected++;
                    Jumpline.Audio.AudioManager.Play("pickup");
                    Haptics.Pulse(Haptics.Light, 140);
                    RefreshHud();
                    if (collected >= m.target) Complete();
                }
            }
        }

        private void Complete()
        {
            done = true;
            foreach (Transform it in items) it.gameObject.SetActive(false);
            if (dialogue != null) dialogue.Say("cayci", "Bless you, nephew! Here, your reward!", 1.6f);
            Jumpline.Audio.AudioManager.Play("tea");
            Invoke(nameof(Finish), 1.8f);
        }

        private void Finish()
        {
            if (flow != null) flow.CompleteLevel();
        }

        private void Release(int i)
        {
            Transform it = items[i];
            items.RemoveAt(i);
            it.gameObject.SetActive(false);
            pool.Push(it);
        }

        private void RefreshHud()
        {
            if (hudCount != null) hudCount.text = Mathf.Min(collected, m.target) + "/" + m.target;
            if (hudIcon != null && m.icon != null) hudIcon.sprite = m.icon;
            if (hudTitle != null) hudTitle.text = Localization.Get(m.title);
        }
    }
}
