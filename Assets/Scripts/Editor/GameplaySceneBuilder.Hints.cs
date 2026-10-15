using System.IO;
using Jumpline.Level;
using UnityEditor;
using UnityEngine;
using static Jumpline.EditorTools.UIKit;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Engel okunabilirliği: yerde parlayan işaret dokuları (▲ zıpla, ▼ kay, ✕ şerit değiştir) ve şeffaf malzemeleri;
    /// sokak ve çatı üreticilerine bağlanır (ObstacleHint).
    /// </summary>
    public static partial class GameplaySceneBuilder
    {
        private const string HintDir = "Assets/Art/UI/Hints";

        private static void BuildObstacleHints(params ObstacleSpawner[] spawners)
        {
            EnsureFolder(HintDir);
            var jump = HintMaterial("Hint_Jump", Hex("#FFD23F"), 0);
            var slide = HintMaterial("Hint_Slide", Hex("#3FD2FF"), 1);
            var block = HintMaterial("Hint_Block", Hex("#FF3B3B"), 2);
            var climb = HintMaterial("Hint_Climb", Hex("#3BFF6A"), 0);
            foreach (var s in spawners)
            {
                if (s == null) continue;
                Set(s, "hintJump", jump);
                Set(s, "hintSlide", slide);
                Set(s, "hintBlock", block);
                Set(s, "hintClimb", climb);
                SetFloat(s, "hintJumpClear", 1.75f);
            }
        }

        /// <summary>shape: 0 = iki yukarı ok (▲▲), 1 = iki aşağı ok (▼▼), 2 = çarpı (✕). Kenarlarda yumuşak parlama.</summary>
        private static Material HintMaterial(string name, Color color, int shape)
        {
            const int w = 128, h = 192;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f;     // -1..1 (yanlar)
                float v = (y + 0.5f) / h;               // 0 (oyuncuya yakın) .. 1 (engele yakın)
                float a = 0f;
                if (shape == 2)
                {
                    float vv = v * 2f - 1f;
                    float d = Mathf.Min(Mathf.Abs(u - vv * 0.75f), Mathf.Abs(u + vv * 0.75f));
                    a = Mathf.Clamp01(1f - (d - 0.12f) / 0.1f) * Mathf.Clamp01((0.95f - Mathf.Abs(vv)) / 0.1f);
                }
                else
                {
                    for (int k = 0; k < 2; k++)   // iki ok, öne doğru
                    {
                        float c = 0.3f + k * 0.38f;
                        float dir = shape == 0 ? 1f : -1f;
                        float arm = (v - c) * dir + Mathf.Abs(u) * 0.55f;   // V biçimi
                        float d = Mathf.Abs(arm);
                        a = Mathf.Max(a, Mathf.Clamp01(1f - (d - 0.05f) / 0.05f) * Mathf.Clamp01((0.92f - Mathf.Abs(u)) / 0.1f));
                    }
                }
                float edge = Mathf.Clamp01(1f - Mathf.Abs(u)) * 0.18f * Mathf.Sin(v * Mathf.PI);   // zemin parıltısı
                px[y * w + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a + edge));
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            string path = $"{HintDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            if (AssetImporter.GetAtPath(path) is TextureImporter imp)
            {
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }
            string matPath = $"{HintDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.shader = Shader.Find("Sprites/Default");
            mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            mat.color = new Color(color.r * 1.4f, color.g * 1.4f, color.b * 1.4f, 0.95f);   // parlak (sis ve gece sahnesinde seçilsin)
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
