// 打包守卫：Gradle 工程生成后检查清单里有没有 VR 标记。缺了头显会把游戏当 2D 窗口跑，所以直接让打包失败
#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEditor.Build;

namespace DLD.EditorTools
{
    internal class DLDManifestGuard : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10000;     // 排在 PICO 的 PXR_Manifest 之后
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var m = File.ReadAllText(Path.Combine(path, "src/main/AndroidManifest.xml"));
            foreach (var need in new[] { "pvr.app.type", "com.picovr.permission.HAND_TRACKING", "com.picovr.permission.EYE_TRACKING" })
                if (!m.Contains(need)) throw new BuildFailedException($"AndroidManifest 缺少 {need}：装到头显会变成 2D 窗口。确认 PICO 加载器已启用、平台已是 Android 并编译完成");
            // 应用名双语：默认 NADIR（PlayerSettings.productName），中文系统显示“天底”
            var dir = Path.Combine(path, "../launcher/src/main/res/values-zh"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "strings.xml"), "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<resources>\n  <string name=\"app_name\">天底</string>\n</resources>\n");
        }
    }
}
#endif
