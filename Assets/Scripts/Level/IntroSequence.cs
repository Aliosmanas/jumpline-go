using System.Collections.Generic;
using Jumpline.Core;
using Jumpline.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.Level
{
    /// <summary>
    /// Açılış sahnesi (hikâye D — Efsaneler Kitabı), ilk kampanya koşusundan önce bir kez: portreli konuşma, oyun donuk.
    /// "Atla" ile geçilebilir. Bitince ya da atlanınca kayda işlenir; sonra koşu başlar (canavar kükrer).
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        [SerializeField] private DialogueBox dialogue;
        [SerializeField] private CanvasGroup backdrop;
        [SerializeField] private Button skipButton;

        public static bool Seen => PlayerPrefs.GetInt("jl.introSeen", 0) == 1;
        public static void Reset() { PlayerPrefs.DeleteKey("jl.introSeen"); PlayerPrefs.Save(); }

        public static readonly DialogueBox.Line[] Lines =
        {
            new DialogueBox.Line("narrator", "Sur, Hasan Pasha Inn. On a dusty bookshop shelf lies a book nobody dares to open..."),
            new DialogueBox.Line("hero", "The Book of Legends? Let's take a look..."),
            new DialogueBox.Line("tepegoz", "AT LAST! I have waited in these pages for centuries!"),
            new DialogueBox.Line("cayci", "Tea, nephew? ... Wait, later! That giant jumped out of the book, he's running into the Sur!"),
            new DialogueBox.Line("cayci", "Find a melon, find a jug, throw it at his head! Send him back into the book!"),
        };

        private bool finished;

        /// <summary>Açılış oynuyor (LevelFlowController zamanı başlatmasın).</summary>
        public static bool Playing { get; private set; }

        private void Awake()
        {
            if (Seen || GameSession.Mode != RunMode.Campaign || dialogue == null) { Hide(); enabled = false; return; }
            Time.timeScale = 0f;   // koşu açılış bitene kadar başlamaz
            Playing = true;
            if (backdrop != null) { backdrop.alpha = 1f; backdrop.blocksRaycasts = true; }
            if (skipButton != null) { skipButton.gameObject.SetActive(true); skipButton.onClick.AddListener(Finish); }
        }

        private void Start()
        {
            if (!enabled) return;
            dialogue.Play(new List<DialogueBox.Line>(Lines), Finish);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            Playing = false;
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.Save();
            dialogue.Close();
            Hide();
            Time.timeScale = 1f;
        }

        private void OnDestroy() => Playing = false;

        private void Hide()
        {
            if (backdrop != null) { backdrop.alpha = 0f; backdrop.blocksRaycasts = false; backdrop.gameObject.SetActive(false); }
            if (skipButton != null) skipButton.gameObject.SetActive(false);
        }
    }
}
