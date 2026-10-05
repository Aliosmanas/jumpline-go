using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Android APK derlemesi (test için geliştirici yapısı: sınırsız para, T kısayolu açık).
    /// Menü: Jumpline → Build Android APK. Toplu mod:
    ///   Unity.exe -batchmode -quit -projectPath D:\Repolar\junpline -executeMethod Jumpline.EditorTools.AndroidBuild.BuildApk
    /// Çıktı: Builds/Android/JumplineGo.apk (hata ayıklama imzası; Play Store için keystore'u kullanıcı ayarlar).
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputPath = "Builds/Android/JumplineGo.apk";

        [MenuItem("Jumpline/Build Android APK")]
        public static void BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            EditorUserBuildSettings.buildAppBundle = false;   // .apk (kabloyla yükleme)
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary sum = report.summary;
            Debug.Log($"[Jumpline] Android derleme: {sum.result}, {sum.totalSize / (1024 * 1024)} MB, {sum.totalTime}, hata {sum.totalErrors}");
            if (Application.isBatchMode) EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
