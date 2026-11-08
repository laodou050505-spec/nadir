// 打包：菜单 DLD/4 切到 Android（要重新压缩贴图，只切一次）、DLD/5 打包 APK。
// 结果写到 Logs/android_build.txt；打包在 delayCall 里跑，MCP 调用不会被卡住。
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DLD.EditorTools
{
    public static class DLDBuild
    {
        public const string PackageId = "com.toybox.nadir", ProductName = "NADIR";
        const string Report = "Logs/android_build.txt", Splash = "Assets/_DLD/Art/Cover/splash.png";

        [MenuItem("DLD/4 切到 Android")]
        public static string SwitchAndroid()
        {
            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            return "切换 " + ok + " 现在 " + EditorUserBuildSettings.activeBuildTarget;
        }

        static void Pico()
        {
            var c = AssetDatabase.LoadAssetAtPath<ByteDance.PICO.XR.PXR_ProjectSetting>("Assets/Resources/PXR_ProjectSetting.asset");
            c.portalInited = true; c.handTracking = true; c.eyeTracking = true; c.videoSeeThrough = true;
            EditorUtility.SetDirty(c);
            foreach (var g in AssetDatabase.FindAssets("t:PXR_Settings"))
            {
                var ps = (ByteDance.PICO.XR.PXR_Settings)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(g));
                ps.systemSplashScreen = AssetDatabase.LoadAssetAtPath<Texture2D>(Splash); EditorUtility.SetDirty(ps);
                // splashPath 是私有字段且不序列化，平时靠 Inspector 的 OnValidate 写；同一进程里打包要手动补上
                typeof(ByteDance.PICO.XR.PXR_Settings).GetField("splashPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(ps, Splash);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>应用图标用封面裁出来的方图</summary>
        static void Icons()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_DLD/Art/Cover/icon.png");
            var t = NamedBuildTarget.Android;
            foreach (var kind in new[] { UnityEditor.Android.AndroidPlatformIconKind.Legacy, UnityEditor.Android.AndroidPlatformIconKind.Round })
            {
                var icons = PlayerSettings.GetPlatformIcons(t, kind);
                foreach (var i in icons) i.SetTexture(tex);
                PlayerSettings.SetPlatformIcons(t, kind, icons);
            }
        }

        static void Apply()
        {
            UISizes.Bake();
            var t = NamedBuildTarget.Android;
            Pico(); Icons();
            PlayerSettings.SplashScreen.show = false;      // 封面启动画面之后不再插 Unity 的 Logo
            PlayerSettings.SetApplicationIdentifier(t, PackageId);
            PlayerSettings.productName = ProductName; PlayerSettings.companyName = "toybox";
            // 版本号按打包时间生成（2026-01-01 起的分钟数）：工程设置被同步覆盖也只增不减，头显不会拒装成“降级”
            int vc = Math.Max(PlayerSettings.Android.bundleVersionCode + 1, (int)((DateTime.Now - new DateTime(2026, 1, 1)).TotalMinutes));
            PlayerSettings.Android.bundleVersionCode = vc; PlayerSettings.bundleVersion = "0.1." + vc;
            PlayerSettings.SetScriptingBackend(t, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)29;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;      // PICO SDK 校验：不能用 GameActivity
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/_DLD/Scenes/Main.unity", true) };
            string ks = Path.GetFullPath("Keystore/nadir.keystore"), pw = "Keystore/keystore_pass.txt";
            if (!File.Exists(ks) || !File.Exists(pw)) throw new Exception("找不到 Keystore/nadir.keystore");
            string pass = File.ReadAllText(pw).Trim();
            PlayerSettings.Android.useCustomKeystore = true; PlayerSettings.Android.keystoreName = ks; PlayerSettings.Android.keystorePass = pass;
            PlayerSettings.Android.keyaliasName = "nadir"; PlayerSettings.Android.keyaliasPass = pass;
            // Gradle 导出目录里的旧资源分片会混进新包，曾造成启动卡死
            foreach (var d in new[] { "Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/src/main/assets", "Library/Bee/Android/Prj/Mono2x/Gradle/unityLibrary/src/main/assets" })
                if (Directory.Exists(d)) Directory.Delete(d, true);
        }

        [MenuItem("DLD/5 打包 APK")]
        public static string BuildApk()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) return "先执行 DLD/4 切到 Android，等导入和编译完成";
            EditorApplication.delayCall += Run;
            return "已开始";
        }

        /// <summary>命令行打包：Unity -batchmode -quit -buildTarget Android -executeMethod DLD.EditorTools.DLDBuild.BuildBatch</summary>
        public static void BuildBatch() { Run(); }

        static void Run()
        {
            string msg;
            try
            {
                Apply();
                Directory.CreateDirectory("Builds");
                var path = Path.GetFullPath($"Builds/NADIR_{DateTime.Now:MMdd_HHmm}.apk");
                var rep = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/_DLD/Scenes/Main.unity" }, locationPathName = path, target = BuildTarget.Android, targetGroup = BuildTargetGroup.Android, options = BuildOptions.None });
                var s = rep.summary;
                long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;      // 真实的 APK 文件大小（summary.totalSize 不是安装包大小，会多出好几倍）
                msg = $"{s.result} 用时 {s.totalTime.TotalMinutes:0.0} 分钟 安装包 {bytes / 1048576f:0.0} MB 错误 {s.totalErrors} → {path}";
            }
            catch (Exception e) { msg = "失败 " + e.Message; }
            File.AppendAllText(Report, DateTime.Now.ToString("HH:mm ") + msg + "\n");
            Debug.Log("[DLD] 打包：" + msg);
        }
    }
}
