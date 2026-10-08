using Jumpline.Player;
using TMPro;
using UnityEngine;

namespace Jumpline.UI
{
    /// <summary>Puan: katedilen her metre için <c>pointsPerMeter</c> puan; para sayacının altında küçük yazı.</summary>
    public class ScoreCounter : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private TMP_Text label;
        [SerializeField, Min(1)] private int pointsPerMeter = 10;

        private int shown = -1;

        public int Score => player == null ? 0 : Mathf.FloorToInt(player.Distance) * pointsPerMeter;

        private void Update()
        {
            if (label == null) return;
            int s = Score;
            if (s == shown) return;   // her karede yeni metin üretme
            shown = s;
            label.text = s.ToString("N0");
        }
    }
}
