using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Jumpline.UI
{
    /// <summary>
    /// Menü ekranları arasında geçiş ve geri geçmişi. Alt çubuk düğmeleri kök ekran açar
    /// (geri → ana ekran); diğer bağlantılar üst üste açar (geri → bir önceki).
    /// </summary>
    public class MenuNavigator : MonoBehaviour
    {
        [SerializeField] private string homeId = "home";
        [SerializeField] private GameObject bottomNav;
        [SerializeField] private Toast toast;
        [SerializeField, Min(0.01f)] private float fadeTime = 0.16f;
        [SerializeField, Range(0.5f, 1f)] private float hiddenScale = 0.96f;

        private readonly Dictionary<string, MenuScreen> screens = new Dictionary<string, MenuScreen>();
        private readonly Stack<MenuScreen> history = new Stack<MenuScreen>();
        private MenuScreen current;
        private Coroutine transition;

        public Toast Toast => toast;

        private void Awake()
        {
            foreach (MenuScreen s in GetComponentsInChildren<MenuScreen>(true)) screens[s.Id] = s;
        }

        private void Start()
        {
            foreach (MenuScreen s in screens.Values) SetVisible(s, false);
            current = screens[homeId];
            SetVisible(current, true);
            bottomNav.SetActive(current.ShowBottomNav);
        }

        private void Update()
        {
            // Android geri tuşu Input System'de Escape olarak gelir.
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Back();
        }

        /// <summary>Ekranı mevcutun üstüne açar.</summary>
        public void Open(string id)
        {
            if (!screens.TryGetValue(id, out MenuScreen target) || target == current) return;
            history.Push(current);
            SwitchTo(target);
        }

        /// <summary>Ekranı kök olarak açar: geri tuşu ana ekrana döner.</summary>
        public void OpenRoot(string id)
        {
            if (!screens.TryGetValue(id, out MenuScreen target)) return;
            history.Clear();
            if (id != homeId) history.Push(screens[homeId]);
            if (target != current) SwitchTo(target);
        }

        public void Back()
        {
            if (history.Count > 0) SwitchTo(history.Pop());
            else if (current.Id != homeId) SwitchTo(screens[homeId]);
        }

        private void SwitchTo(MenuScreen target)
        {
            if (transition != null) StopCoroutine(transition);
            transition = StartCoroutine(Transition(current, target));
            current = target;
        }

        private IEnumerator Transition(MenuScreen from, MenuScreen to)
        {
            bottomNav.SetActive(to.ShowBottomNav);
            foreach (MenuScreen s in screens.Values)
                if (s != from && s != to) SetVisible(s, false);

            if (from != null)
            {
                yield return Fade(from.Group, 0f, 1f, hiddenScale);
                SetVisible(from, false);
            }
            to.gameObject.SetActive(true);
            yield return Fade(to.Group, 1f, hiddenScale, 1f);
            SetVisible(to, true);
            transition = null;
        }

        private IEnumerator Fade(CanvasGroup group, float targetAlpha, float fromScale, float toScale)
        {
            group.interactable = false;
            group.blocksRaycasts = false;
            float startAlpha = group.alpha;
            Transform t = group.transform;
            for (float time = 0f; time < fadeTime; time += Time.unscaledDeltaTime)
            {
                float u = 1f - Mathf.Pow(1f - time / fadeTime, 3f);
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, u);
                t.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, u);
                yield return null;
            }
        }

        private static void SetVisible(MenuScreen s, bool visible)
        {
            CanvasGroup g = s.Group;
            s.gameObject.SetActive(visible);
            g.alpha = visible ? 1f : 0f;
            g.interactable = visible;
            g.blocksRaycasts = visible;
            g.transform.localScale = Vector3.one;
        }
    }
}
