using System;
using System.Collections;
using System.Collections.Generic;
using Jumpline.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Jumpline.UI
{
    /// <summary>
    /// Portreli konuşma kutusu (açılış sahnesi, öğretici, günlük görev — hepsi bunu kullanır).
    /// Satır: konuşan kimliği (portre + isim), metin. Metin harf harf akar; dokununca önce tamamlanır, sonra sıradakine geçer.
    /// Zaman ölçeğinden bağımsız çalışır (öğreticide oyun yavaşken de okunur). Konuşmacılar: <see cref="Speaker"/>.
    /// </summary>
    public class DialogueBox : MonoBehaviour
    {
        [Serializable]
        public struct Speaker
        {
            public string id;
            [Tooltip("Ekranda görünen ad (çeviri anahtarı).")] public string displayName;
            public Sprite portrait;
            public Color plateColor;
            [Tooltip("true: portre sağda (karşı taraf).")] public bool right;
        }

        public struct Line
        {
            public string speaker;
            public string text;
            public Line(string speaker, string text) { this.speaker = speaker; this.text = text; }
        }

        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image portraitLeft;
        [SerializeField] private Image portraitRight;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private Image namePlate;
        [SerializeField] private TMP_Text body;
        [SerializeField] private GameObject continueHint;
        [SerializeField] private Button tapArea;
        [SerializeField] private Speaker[] speakers;
        [SerializeField, Min(5f)] private float charsPerSecond = 42f;
        [SerializeField, Min(0.05f)] private float fadeTime = 0.18f;

        private readonly Queue<Line> queue = new Queue<Line>();
        private Action onDone;
        private bool typing;
        private bool advance;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (tapArea != null) tapArea.onClick.AddListener(() => advance = true);
            SetVisible(0f);
        }

        /// <summary>Satırları sırayla göster; bitince onDone.</summary>
        public void Play(IEnumerable<Line> lines, Action done = null)
        {
            queue.Clear();
            foreach (Line l in lines) queue.Enqueue(l);
            onDone = done;
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        /// <summary>Tek satırlık kısa ipucu: dokunma beklemez, süre dolunca kapanır (öğreticide oyun akarken).</summary>
        public void Say(string speaker, string text, float seconds)
        {
            queue.Clear();
            StopAllCoroutines();
            StartCoroutine(Bark(new Line(speaker, text), seconds));
        }

        public void Close()
        {
            StopAllCoroutines();
            StartCoroutine(Fade(0f, false));
            IsOpen = false;
        }

        private IEnumerator Run()
        {
            IsOpen = true;
            if (group.alpha < 1f) yield return Fade(1f);
            while (queue.Count > 0)
            {
                Line line = queue.Dequeue();
                yield return Type(line);
                if (continueHint != null) continueHint.SetActive(true);
                advance = false;
                while (!advance) yield return null;
                if (continueHint != null) continueHint.SetActive(false);
                Jumpline.Audio.AudioManager.Play("click", 0.6f);
            }
            yield return Fade(0f);
            IsOpen = false;
            onDone?.Invoke();
        }

        private IEnumerator Bark(Line line, float seconds)
        {
            IsOpen = true;
            yield return Fade(1f, false);   // ipucu: dokunmayı engellemez (oyuncu kaydırabilsin)
            if (continueHint != null) continueHint.SetActive(false);
            yield return Type(line, false);
            yield return new WaitForSecondsRealtime(seconds);
            yield return Fade(0f, false);
            IsOpen = false;
        }

        private IEnumerator Type(Line line, bool skippable = true)
        {
            Speaker sp = Find(line.speaker);
            bool named = !string.IsNullOrEmpty(sp.displayName);
            if (nameLabel != null) nameLabel.text = named ? Localization.Get(sp.displayName) : "";
            if (namePlate != null)
            {
                namePlate.gameObject.SetActive(named);
                if (sp.plateColor.a > 0f) namePlate.color = sp.plateColor;
            }
            SetPortrait(portraitLeft, !sp.right ? sp.portrait : null);
            SetPortrait(portraitRight, sp.right ? sp.portrait : null);
            string text = Localization.Get(line.text);
            body.text = text;
            body.maxVisibleCharacters = 0;
            typing = true;
            advance = false;
            float shown = 0f;
            while (shown < text.Length)
            {
                if (advance && skippable) { advance = false; break; }   // dokunuş: metni tamamla
                shown += charsPerSecond * Time.unscaledDeltaTime;
                body.maxVisibleCharacters = Mathf.FloorToInt(shown);
                yield return null;
            }
            body.maxVisibleCharacters = text.Length;
            typing = false;
        }

        private static void SetPortrait(Image img, Sprite s)
        {
            if (img == null) return;
            img.gameObject.SetActive(s != null);
            if (s != null) img.sprite = s;
        }

        private Speaker Find(string id)
        {
            if (speakers != null) foreach (Speaker s in speakers) if (s.id == id) return s;
            return new Speaker { id = id, displayName = id };
        }

        private IEnumerator Fade(float to, bool block = true)
        {
            float from = group.alpha;
            group.blocksRaycasts = block && to > 0.5f;
            group.interactable = block && to > 0.5f;
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(from, to, t / fadeTime);
                yield return null;
            }
            group.alpha = to;
        }

        private void SetVisible(float a)
        {
            if (group == null) return;
            group.alpha = a;
            group.blocksRaycasts = group.interactable = a > 0.5f;
        }
    }
}
