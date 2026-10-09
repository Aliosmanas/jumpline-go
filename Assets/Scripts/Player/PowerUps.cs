using System;
using Jumpline.Core;
using UnityEngine;

namespace Jumpline.Player
{
    public enum PowerUpType { Magnet, Shield, DoubleGold, SpeedBoost }

    /// <summary>
    /// Koşudaki etkin güçler. Süreler yükseltme ekranından (CharacterStats: magnet / shield / double_gold / speed_boost,
    /// değer = saniye) gelir. Etkiler:
    ///  • Mıknatıs — CoinField yakındaki paraları çeker.   • 2x Altın — her para 2 sayılır.
    ///  • Kalkan — bir ölümcül çarpmayı emer (engel parçalanır), sonra biter.
    ///  • Hız — koşu hızı artar, çarpılan engel parçalanır (ölüm yok); canavarla aradaki mesafe kapanır.
    /// Boşluğa düşüş (çatı arası) hiçbir güçle engellenmez.
    /// </summary>
    public class PowerUps : MonoBehaviour
    {
        [SerializeField] private CharacterStats stats;
        [Tooltip("Stats yoksa ya da id bulunamazsa kullanılan süre (sn).")]
        [SerializeField, Min(1f)] private float fallbackDuration = 8f;
        [Tooltip("Hız gücünde koşu hızı çarpanı.")]
        [SerializeField, Min(1f)] private float speedMultiplier = 1.35f;
        [Tooltip("Kalkan/hız bir çarpmayı emdikten sonra kısa dokunulmazlık (sn) — aynı engele ikinci kez çarpmasın.")]
        [SerializeField, Min(0f)] private float graceTime = 0.6f;
        [SerializeField] private ParticleSystem shatterFx;

        private static readonly string[] StatIds = { "magnet", "shield", "double_gold", "speed_boost" };
        private readonly float[] endTime = new float[4];
        private readonly float[] duration = new float[4];
        private float graceUntil = -1f;

        /// <summary>Güç başladı / bitti.</summary>
        public event Action<PowerUpType> Activated;
        public event Action<PowerUpType> Ended;
        /// <summary>Bir çarpma emildi (kamera sarsıntısı, ses).</summary>
        public event Action Absorbed;

        public bool IsActive(PowerUpType t) => Time.time < endTime[(int)t];
        public float Remaining01(PowerUpType t) => IsActive(t) && duration[(int)t] > 0f ? (endTime[(int)t] - Time.time) / duration[(int)t] : 0f;
        public float SpeedMultiplier => IsActive(PowerUpType.SpeedBoost) ? speedMultiplier : 1f;
        public int CoinMultiplier => IsActive(PowerUpType.DoubleGold) ? 2 : 1;
        public bool InGrace => Time.time < graceUntil;

        public static string StatId(PowerUpType t) => StatIds[(int)t];

        /// <summary>Yükseltme seviyesine göre süre (sn).</summary>
        public float DurationOf(PowerUpType t)
        {
            StatDefinition s = stats != null ? stats.Find(StatId(t)) : null;
            return s != null ? CharacterStats.Value(s) : fallbackDuration;
        }

        public void Activate(PowerUpType t)
        {
            int i = (int)t;
            duration[i] = DurationOf(t);
            endTime[i] = Time.time + duration[i];
            Activated?.Invoke(t);
        }

        private readonly bool[] wasActive = new bool[4];

        private void Update()
        {
            for (int i = 0; i < 4; i++)
            {
                bool now = Time.time < endTime[i];
                if (wasActive[i] && !now) Ended?.Invoke((PowerUpType)i);
                wasActive[i] = now;
            }
        }

        /// <summary>Ölümcül çarpma: hız ya da kalkan varsa emer (engeli parçalar) ve true döner.</summary>
        public bool TryAbsorb(Jumpline.Level.Obstacle hit, float runnerSpeed)
        {
            if (InGrace) return true;
            bool speed = IsActive(PowerUpType.SpeedBoost);
            bool shield = IsActive(PowerUpType.Shield);
            if (!speed && !shield) return false;
            if (!speed)
            {
                endTime[(int)PowerUpType.Shield] = Time.time;   // kalkan bir kez emer
            }
            graceUntil = Time.time + graceTime;
            Shatter(hit, runnerSpeed);
            Haptics.Pulse(Haptics.Medium, 200);
            Absorbed?.Invoke();
            return true;
        }

        /// <summary>Koşu bitince / devam edince güçler sıfırlanır.</summary>
        public void ClearAll()
        {
            for (int i = 0; i < 4; i++) endTime[i] = 0f;
        }

        private void Shatter(Jumpline.Level.Obstacle hit, float runnerSpeed)
        {
            if (hit == null) return;
            Vector3 at = hit.transform.position + Vector3.up * 1f;
            if (hit.TryGetComponent(out Jumpline.Level.Breakable b))
            {
                b.Break(runnerSpeed * 1.5f);
                hit.enabled = false;              // parçalar savrulur, engel artık katı değil
            }
            else hit.gameObject.SetActive(false); // araç/duvar: toz bulutu içinde yok olur
            if (shatterFx != null)
            {
                shatterFx.transform.position = at;
                shatterFx.Emit(45);
            }
        }
    }
}
