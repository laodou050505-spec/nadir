// 截图审查：指定机位临时建相机渲染成 PNG（不开后处理，和真机一致）。
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DLD.EditorTools
{
    public static class Capture
    {
        /// <summary>透视模式的 alpha 巡检：清屏全透明渲染一次，返回 alpha 大于阈值的像素比例和整张 alpha 图（a 通道写成灰度 PNG）</summary>
        public static float ShotAlpha(string path, Vector3 pos, Vector3 euler, float fov = 80, int w = 1000, int h = 700)
        {
            var look = Object.FindFirstObjectByType<SceneLook>();
            if (look) look.Apply();
            var go = new GameObject("__CaptureCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>(); go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            cam.fieldOfView = fov; cam.nearClipPlane = .03f; cam.farClipPlane = 200; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            var data = go.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.antialiasing = AntialiasingMode.None;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); RenderTexture.active = prev;
            var px = tex.GetPixels32(); int n = 0; var o = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++) { if (px[i].a > 12) n++; o[i] = new Color32(px[i].a, px[i].a, px[i].a, 255); }
            tex.SetPixels32(o); File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
            return (float)n / px.Length;
        }

        public static void Shot(string path, Vector3 pos, Vector3 euler, float fov = 80, int w = 1600, int h = 1000)
        {
            var look = Object.FindFirstObjectByType<SceneLook>();
            if (look) look.Apply();
            var go = new GameObject("__CaptureCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            cam.fieldOfView = fov; cam.nearClipPlane = 0.03f; cam.farClipPlane = 200;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        }
    }
}
