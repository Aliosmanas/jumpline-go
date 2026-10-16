using System.Collections;
using System.IO;
using Jumpline.Core;
using Jumpline.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jumpline.Tests
{
    /// <summary>
    /// Parkur önizlemesi (elle: -testFilter ChunkRunCapture): koşucu kontrolü kapalı, kod onu adım adım ileri taşır
    /// (chunk'lar ve harita parçaları gerçekte olduğu gibi üretilir); belirli mesafelerde oyun kamerası karesi →
    /// Logs/ChunkRun/*.png. Kolay → karışık → zor evre ve portal meydanı görülür.
    /// </summary>
    [Explicit, Category("Concept")]
    public class ChunkRunCapture
    {
        [UnityTest]
        public IEnumerator Capture()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            GameSession.Mode = RunMode.QuickRace;
            GameSession.Level = 1;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerController>();
            player.enabled = false;
            Random.InitState(11);
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "ChunkRun");
            Directory.CreateDirectory(dir);
            float[] shots = { 40f, 150f, 260f, 380f, 520f, 700f, 900f, 1100f, 1300f, 1420f };
            float z0 = player.transform.position.z;
            int next = 0;
            for (float d = 0f; next < shots.Length; d += 1.5f)
            {
                player.transform.position = new Vector3(player.LaneOriginX, player.GroundHeight, z0 + d);
                yield return null;
                if (d < shots[next]) continue;
                yield return null;
                Shoot(Camera.main, Path.Combine(dir, $"run_{(int)shots[next]:0000}m.png"));
                next++;
            }
            Assert.Pass();
        }

        [UnityTest]
        public IEnumerator CaptureRooftop()
        {
            PlayerPrefs.SetInt("jl.introSeen", 1);
            PlayerPrefs.SetInt("jl.tutorialDone", 1);
            GameSession.Mode = RunMode.QuickRace;
            GameSession.Level = 1;
            SceneManager.LoadScene("Gameplay");
            yield return null;
            yield return null;
            var player = Object.FindAnyObjectByType<PlayerController>();
            var switcher = Object.FindAnyObjectByType<Jumpline.Level.WorldSwitcher>();
            switcher.EnterRooftop();
            yield return new WaitForSeconds(1.2f);
            player.enabled = false;
            string dir = Path.Combine(Application.dataPath, "..", "Logs", "ChunkRun");
            Directory.CreateDirectory(dir);
            Vector3 start = player.transform.position;
            float[] shots = { 30f, 90f, 160f, 240f, 330f };
            int next = 0;
            for (float d = 0f; next < shots.Length; d += 1.5f)
            {
                player.transform.position = new Vector3(start.x, start.y, start.z + d);
                yield return null;
                if (d < shots[next]) continue;
                yield return null;
                Shoot(Camera.main, Path.Combine(dir, $"roof_{(int)shots[next]:0000}m.png"));
                next++;
            }
            Assert.Pass();
        }

        private static void Shoot(Camera cam, string path)
        {
            var rt = new RenderTexture(720, 1280, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(720, 1280, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 720, 1280), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
