using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Jumpline.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Jumpline.Tests
{
    /// <summary>Atma düğmesine dokunuş ulaşıyor mu: düğmenin merkezinde arayüz ışın taraması, en üstteki öğe düğme olmalı.</summary>
    public class InputProbe
    {
        [UnityTest]
        public IEnumerator ThrowButton_ReceivesTaps()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            GameSession.Mode = RunMode.Campaign;
            GameSession.Level = 1;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
                            Jumpline.Level.Course.ClearRange(-1e6f, 120f);   // koşucu çarpıp ölmesin (ölüyken atış yok)
            yield return new WaitForSeconds(5f);
            var throws = Object.FindAnyObjectByType<Jumpline.Boss.ThrowSystem>();
            var held = (Stack<int>)typeof(Jumpline.Boss.ThrowSystem)
                .GetField("held", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(throws);
            held.Push(0);
            (typeof(Jumpline.Boss.ThrowSystem).GetField("Changed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(throws) as System.Action)?.Invoke();
            yield return new WaitForSecondsRealtime(0.6f);
            var root = GameObject.Find("ThrowButton");
            Assert.IsNotNull(root, "ThrowButton yok");
            var button = root.GetComponentInChildren<Button>(true);
            Assert.IsNotNull(button, "ThrowButton altında Button yok");
            var rt = (RectTransform)button.transform;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            var data = new PointerEventData(EventSystem.current) { position = screen };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            string list = string.Join(" > ", hits.Take(6).Select(h => h.gameObject.name + "(" + h.gameObject.transform.parent?.name + ")"));
            Debug.Log("[InputProbe] " + list + " | active=" + root.activeInHierarchy + " interactable=" + button.interactable);
            Assert.IsTrue(hits.Count > 0, "dokunuş hiçbir şeye çarpmıyor");
            Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(root.transform), "en üstte başka öğe var: " + list);
            int before = throws.ThrowsMade;
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, data, ExecuteEvents.pointerClickHandler);
            yield return null;
            Debug.Log("[InputProbe] atış " + before + " -> " + throws.ThrowsMade);
            Assert.Greater(throws.ThrowsMade, before, "düğmeye dokunmak atış yapmalı");
        }
    }
}
