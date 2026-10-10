using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Jumpline.EditorTools
{
    /// <summary>
    /// Android APK derlemesi: varsayılan sürüm yapısı (test oyuncuları ve performans ölçümü için); ayrıca geliştirici yapısı menüsü.
    /// Menü: Jumpline → Build Android APK. Toplu mod:
    ///   Unity.exe -batchmode -quit -projectPath D:\Repolar\junpline -executeMethod Jumpline.EditorTools.AndroidBuild.BuildApk
    /// Çıktı: Builds/Android/JumplineGo.apk (hata ayıklama imzası; Play Store için keystore'u kullanıcı ayarlar).
    /// </summary>
    public static class AndroidBuild
    {
        public const string OutputPath = "Builds/Android/JumplineGo.apk";

        /// <summary>Test APK'sı: sürüm yapısı + JL_TEST_BUILD (sınırsız altın/elmas).</summary>
        [MenuItem("Jumpline/Build Android APK")]
        public static void BuildApk() => Build(false, true);

        /// <summary>Mağaza adayı: gerçek ekonomi, test kısayolları yok.</summary>
        [MenuItem("Jumpline/Build Android APK (Store)")]
        public static void BuildStoreApk() => Build(false, false);

        /// <summary>Geliştirici yapısı: profilleyici/hata ayıklama açık, daha yavaş, köşede "Development Build".</summary>
        [MenuItem("Jumpline/Build Android APK (Development)")]
        public static void BuildDevApk() => Build(true, true);

        private const string TestDefine = "JL_TEST_BUILD";

        private static void Build(bool development, bool testBuild)
        {
            // Kullanıcı adındaki boşluk Java 17'nin geçici soketini bozar ("Unable to establish loopback connection"):
            // Gradle'a boşluksuz geçici klasör ver (alt süreçler bu ortamı devralır).
            const string tmp = @"D:\tmp";
            Directory.CreateDirectory(tmp);
            System.Environment.SetEnvironmentVariable("TEMP", tmp);
            System.Environment.SetEnvironmentVariable("TMP", tmp);
            System.Environment.SetEnvironmentVariable("JAVA_TOOL_OPTIONS", $"-Djdk.net.unixdomain.tmpdir={tmp} -Djava.io.tmpdir={tmp}");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            EditorUserBuildSettings.buildAppBundle = false;   // .apk (kabloyla yükleme)
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            var target = UnityEditor.Build.NamedBuildTarget.Android;
            var defines = new System.Collections.Generic.List<string>(PlayerSettings.GetScriptingDefineSymbols(target).Split(';', System.StringSplitOptions.RemoveEmptyEntries));
            defines.Remove(TestDefine);
            if (testBuild) defines.Add(TestDefine);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            PlayerSettings.SetIl2CppCompilerConfiguration(UnityEditor.Build.NamedBuildTarget.Android,
                development ? Il2CppCompilerConfiguration.Debug : Il2CppCompilerConfiguration.Release);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary sum = report.summary;
            Debug.Log($"[Jumpline] Android derleme: {sum.result}, {sum.totalSize / (1024 * 1024)} MB, {sum.totalTime}, hata {sum.totalErrors}");
            if (Application.isBatchMode) EditorApplication.Exit(sum.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
