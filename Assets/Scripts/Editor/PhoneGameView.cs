using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Editör her açıldığında Game penceresini telefon ekranına (1080x1920 dikey, "Jumpline Portrait")
    /// ayarlar ve pencereye sığdırır. Menü: Jumpline → Phone Game View.
    /// </summary>
    [InitializeOnLoad]
    public static class PhoneGameView
    {
        private const string SizeName = "Jumpline Portrait";
        private const int Width = 1080, Height = 1920;
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static PhoneGameView()
        {
            // Pencere düzeni yüklendikten sonra uygula.
            EditorApplication.delayCall += () => EditorApplication.delayCall += Apply;
        }

        [MenuItem("Jumpline/Phone Game View")]
        public static void Apply()
        {
            try
            {
                Assembly asm = typeof(EditorWindow).Assembly;
                Type sizesType = asm.GetType("UnityEditor.GameViewSizes");
                object sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null, null);
                // etkin platformun grubu (proje Android → Android grubu; Standalone'a eklenen boyut Game penceresinde görünmez)
                object current = sizesType.GetProperty("currentGroupType", Flags)?.GetValue(sizes, null) ?? GameViewSizeGroupType.Android;
                object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { (int)current });
                Type groupType = group.GetType();

                int index = FindSize(groupType, group);
                if (index < 0)
                {
                    Type sizeType = asm.GetType("UnityEditor.GameViewSize");
                    Type kind = asm.GetType("UnityEditor.GameViewSizeType");
                    object size = sizeType.GetConstructor(new[] { kind, typeof(int), typeof(int), typeof(string) })
                        .Invoke(new object[] { Enum.Parse(kind, "FixedResolution"), Width, Height, SizeName });
                    groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    sizesType.GetMethod("SaveToHDD", Flags)?.Invoke(sizes, null);
                    index = FindSize(groupType, group);
                }

                Type gameViewType = asm.GetType("UnityEditor.GameView");
                UnityEngine.Object[] views = Resources.FindObjectsOfTypeAll(gameViewType);
                if (views.Length == 0) return;
                var view = (EditorWindow)views[0];
                gameViewType.GetProperty("selectedSizeIndex", Flags).SetValue(view, index, null);

                // Pencereye sığdır (en küçük ölçek = tüm ekran görünür)
                float min = (float)gameViewType.GetProperty("minScale", Flags).GetValue(view, null);
                gameViewType.GetMethod("SnapZoom", Flags).Invoke(view, new object[] { min });
                view.Repaint();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Jumpline] Telefon görünümü ayarlanamadı: {e.GetBaseException().Message}");
            }
        }

        private static int FindSize(Type groupType, object group)
        {
            var texts = (string[])groupType.GetMethod("GetDisplayTexts").Invoke(group, null);
            return Array.FindIndex(texts, t => t.StartsWith(SizeName));
        }
    }
}
