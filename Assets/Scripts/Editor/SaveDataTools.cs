using UnityEditor;
using UnityEngine;

namespace Jumpline.EditorTools
{
    public static class SaveDataTools
    {
        /// <summary>Bu projenin tüm PlayerPrefs kaydını siler (ilerleme, cüzdan, ayarlar).</summary>
        [MenuItem("Jumpline/Reset Save Data")]
        public static void ResetSaveData()
        {
            if (!EditorUtility.DisplayDialog("Kaydı sıfırla",
                    "Bu projenin tüm kayıtları (bölümler, altın/elmas, eşyalar, ayarlar) silinecek. Emin misin?",
                    "Sil", "Vazgeç")) return;
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[Jumpline] Kayıt sıfırlandı.");
        }
    }
}
