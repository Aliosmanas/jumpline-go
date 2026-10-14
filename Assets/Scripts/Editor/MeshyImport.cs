using System.IO;
using UnityEditor;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Meshy modelleri (Blender'dan gömülü dokulu FBX): dokular FBX'in yanına &lt;ad&gt;_Tex klasörüne çıkarılır,
    /// dokusuz çıkarılmış eski malzemeler silinip yeniden çıkarılır (malzeme dokusuyla gelir), sonra canlı/ortama uygun ayar.
    /// Her ConfigureXImport'un başında <see cref="Prepare"/>, malzeme çıkarıldıktan sonra <see cref="Tune"/> çağrılır.
    /// </summary>
    public static class MeshyImport
    {
        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        // Meshy dokuları zaten aydınlatma içeriyor: hafif parlak (ıslak/plastik görünmesin), metal değil, taban rengi beyaz
        private const float Smoothness = 0.32f;

        public static string TexDir(string fbxPath) =>
            (Path.GetDirectoryName(fbxPath) + "/" + Path.GetFileNameWithoutExtension(fbxPath) + "_Tex").Replace('\\', '/');

        public static void Prepare(string fbxPath)
        {
            if (AssetImporter.GetAtPath(fbxPath) is not ModelImporter imp) return;
            string dir = TexDir(fbxPath);
            if (!AssetDatabase.IsValidFolder(dir))
            {
                // gömülü dokular alt varlık olarak listelenmez → çıkarmayı doğrudan dene
                AssetDatabase.CreateFolder(Path.GetDirectoryName(fbxPath).Replace('\\', '/'), Path.GetFileName(dir));
                bool ok = imp.ExtractTextures(dir);
                AssetDatabase.Refresh();
                if (!ok || AssetDatabase.FindAssets("t:Texture", new[] { dir }).Length == 0)
                {   // gömülü doku yok (eski Blender modelleri): klasörü sil, malzemelere dokunma
                    AssetDatabase.DeleteAsset(dir);
                    return;
                }
                imp.SaveAndReimport();
            }
            // dokusuz kalmış (dokular çıkarılmadan önce oluşmuş) eşlemeleri temizle → yeniden çıkarılsın
            bool changed = false;
            foreach (var kv in imp.GetExternalObjectMap())
            {
                if (kv.Value is Material m && m.HasProperty(BaseMap) && m.GetTexture(BaseMap) != null) continue;
                imp.RemoveRemap(kv.Key);
                // yalnız bu modelin kendi malzemesi silinir; paylaşılan eski malzemeler (ör. T_Paint) yerinde kalır
                string model = Path.GetFileNameWithoutExtension(fbxPath);
                if (kv.Value != null && (kv.Value.name.StartsWith(model + "_") || kv.Value.name.StartsWith("M_Body_")))
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(kv.Value));
                changed = true;
            }
            if (changed) imp.SaveAndReimport();
        }

        /// <summary>Dokusu &lt;ad&gt;_Tex klasöründen gelen malzemeleri URP Lit'e çevirip canlı ayar verir.</summary>
        public static void Tune(string matDir)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { matDir }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                Texture tex = m.HasProperty(BaseMap) ? m.GetTexture(BaseMap) : m.mainTexture;
                if (tex == null || !AssetDatabase.GetAssetPath(tex).Contains("_Tex/")) continue;
                if (lit != null && m.shader != lit) { m.shader = lit; m.SetTexture(BaseMap, tex); }
                m.SetColor("_BaseColor", Color.white);
                // glTF metal/pürüz dokusu (B=metal, G=pürüz) URP düzenine (R=metal, A=parlaklık) uymaz → kaldır, sabit değer
                if (m.HasProperty("_MetallicGlossMap")) m.SetTexture("_MetallicGlossMap", null);
                m.DisableKeyword("_METALLICSPECGLOSSMAP");
                Texture bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
                if (bump != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(bump)) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                {
                    ti.textureType = TextureImporterType.NormalMap;
                    ti.SaveAndReimport();
                }
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", Smoothness);
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
