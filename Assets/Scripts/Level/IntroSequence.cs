using System.Collections.Generic;
using Jumpline.Core;
using Jumpline.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.Level
{
    /// <summary>
    /// Açılış sahnesi (hikâye D — Efsaneler Kitabı): her bölümün ilk kampanya koşusundan önce bir kez portreli konuşma
    /// (bölüm 1: kitap açılır, Tepegöz kaçar; bölüm 2: Mardin'de Şahmeran), oyun donuk.
    /// "Atla" ile geçilebilir. Bitince ya da atlanınca kayda işlenir; sonra koşu başlar (canavar kükrer).
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        [SerializeField] private DialogueBox dialogue;
        [SerializeField] private CanvasGroup backdrop;
        [SerializeField] private Button skipButton;

        public static bool Seen => PlayerPrefs.GetInt("jl.introSeen", 0) == 1;
        private static string SeenKey(int level) => level <= 1 ? "jl.introSeen" : "jl.introSeen." + level;
        public static bool SeenLevel(int level) => PlayerPrefs.GetInt(SeenKey(level), 0) == 1;

        public static readonly DialogueBox.Line[] Level2Lines =
        {
            new DialogueBox.Line("narrator", "Mardin. The Book of Legends trembles... a new page is glowing in golden light."),
            new DialogueBox.Line("sahmeran", "Who dares to wake the Queen of Serpents? These honey-stone streets are mine now!"),
            new DialogueBox.Line("cayci", "Nephew, that is Shahmaran! She is faster than the giant. Keep throwing, never let her slip away!"),
            new DialogueBox.Line("hero", "Back into the book you go, Your Majesty!"),
        };

        public static readonly DialogueBox.Line[] Level3Lines =
        {
            new DialogueBox.Line("narrator", "Hasankeyf, on the banks of the Tigris. Ancient caves stare from the cliffs..."),
            new DialogueBox.Line("humbaba", "I guarded the cedar forests before your cities had names! Leave my river!"),
            new DialogueBox.Line("cayci", "Humbaba, the forest guardian! He is huge but slow to turn. Aim for his back!"),
            new DialogueBox.Line("hero", "The book has room for one more page. Let's go!"),
        };

        public static readonly DialogueBox.Line[] Level4Lines =
        {
            new DialogueBox.Line("narrator", "Sanliurfa. Beyond the city, ancient T-shaped stones stand on a hill older than history..."),
            new DialogueBox.Line("stone_guardian", "TWELVE THOUSAND YEARS I STOOD. NOW... I WALK."),
            new DialogueBox.Line("cayci", "A stone giant! Melons won't crack it, but each hit slows it down. Keep at it, nephew!"),
            new DialogueBox.Line("hero", "Then I'll throw twice as many!"),
        };

        public static readonly DialogueBox.Line[] Level5Lines =
        {
            new DialogueBox.Line("narrator", "Van. The lake is calm... too calm. Something huge moves under the soda-blue water."),
            new DialogueBox.Line("van_monster", "SSSSS... Fishermen whisper my name. Now the whole city will scream it!"),
            new DialogueBox.Line("cayci", "The Van Lake Monster! It is slippery on land. Hit it before it reaches the shore again!"),
            new DialogueBox.Line("hero", "No more hiding in the lake for you!"),
        };

        /// <summary>Bölümün açılış satırları (yoksa null).</summary>
        public static DialogueBox.Line[] LinesFor(int level) => level switch { <= 1 => Lines, 2 => Level2Lines, 3 => Level3Lines, 4 => Level4Lines, 5 => Level5Lines, _ => null };
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
        private int level = 1;

        /// <summary>Açılış oynuyor (LevelFlowController zamanı başlatmasın).</summary>
        public static bool Playing { get; private set; }

        private void Awake()
        {
            level = GameSession.Level;
            if (SeenLevel(level) || LinesFor(level) == null || GameSession.Mode != RunMode.Campaign || dialogue == null) { Hide(); enabled = false; return; }
            Time.timeScale = 0f;   // koşu açılış bitene kadar başlamaz
            Playing = true;
            if (backdrop != null) { backdrop.alpha = 1f; backdrop.blocksRaycasts = true; }
            if (skipButton != null) { skipButton.gameObject.SetActive(true); skipButton.onClick.AddListener(Finish); }
        }

        private void Start()
        {
            if (!enabled) return;
            dialogue.Play(new List<DialogueBox.Line>(LinesFor(level)), Finish);
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            Playing = false;
            PlayerPrefs.SetInt(SeenKey(level), 1);
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
