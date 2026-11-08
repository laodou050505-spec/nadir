// 结局自检：在编辑器 Play 里调用 EndingCheck.Run("/tmp/dld_end")。
// 和真的走进传送门走同一条路（GameRoot.DebugEnterPortal），按时间点把主相机渲染成带透明度的图（透视模式下 alpha=0 的地方就是真实画面），
// 同时记下每一帧的状态（压暗、日晕、眼皮、字卡、环境音音量），给 Python 合成到房间照片上逐帧检查
using System.IO;
using System.Text;
using DLD.Play;
using UnityEditor;
using UnityEngine;

namespace DLD.EditorTools
{
    public static class EndingCheck
    {
        static readonly float[] AtEnd = { .3f, .8f, 1.3f, 1.8f, 2.4f, 3.0f, 3.7f, 4.3f, 5.2f, 6.6f, 8.0f, 9.5f, 11f, 12.5f, 14f, 16f, 19f, 22f };
        static readonly float[] AtNext = { .3f, .8f, 1.3f, 1.8f, 2.4f, 3.0f, 3.7f, 4.3f, 4.9f, 5.6f, 6.4f, 7.5f };
        static float[] At = AtEnd; static bool toEnd;
        static string dir; static float t0, tLook; static int next, lookStep; static StringBuilder log;

        public static void Run(string outDir, bool end = true)
        {
            var r = Object.FindAnyObjectByType<GameRoot>();
            if (!r || r.game == null) { Debug.LogError("[EndingCheck] 不在对局里"); return; }
            dir = outDir; Directory.CreateDirectory(dir); foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            log = new StringBuilder(); next = 0; lookStep = 0; t0 = Time.time; toEnd = end; At = end ? AtEnd : AtNext;
            r.DebugEnterPortal(end);
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            float t = Time.time - t0;
            if (next < At.Length && t >= At[next]) { Shot($"f{next:00}_{At[next]:0.0}s", t, 0); next++; }
            if (next >= At.Length && toEnd && lookStep != 4)
            {
                // 模拟转头：先正对日晕看 3 秒，再转开 3 秒，看晃眼会不会跟着变
                var ei = Object.FindAnyObjectByType<EditorInput>();
                if (lookStep == 0) { ei.DebugLook(ei.Yaw - 4, -15); tLook = t; lookStep = 1; }
                else if (lookStep == 1 && t - tLook > 3) { Shot("face_halo", t, 0); tLook = t; lookStep = 5; }
                else if (lookStep == 5 && t - tLook > 3) { Shot("face_halo_b", t, 0); ei.DebugLook(ei.Yaw + 75, 0); tLook = t; lookStep = 2; }
                else if (lookStep == 2 && t - tLook > 4.5f) { Shot("turned_away", t, 0); ei.DebugLook(ei.Yaw - 45, 0); lookStep = 4; }
                return;
            }
            if (next >= At.Length)
            {
                File.WriteAllText(Path.Combine(dir, "log.txt"), log.ToString());
                File.WriteAllText(Path.Combine(dir, "done.txt"), "ok");
                EditorApplication.update -= Tick;
            }
        }

        static float GlareOf(Renderer h) { var m = new MaterialPropertyBlock(); h.GetPropertyBlock(m); return m.GetFloat("_Glare"); }
        static float SunAngle(Renderer h, Camera c) { var m = new MaterialPropertyBlock(); h.GetPropertyBlock(m); return Vector3.Angle(c.transform.forward, m.GetVector("_SunDir")); }

        static void Shot(string name, float t, float pitch, float yaw = 0)
        {
            var r = Object.FindAnyObjectByType<GameRoot>(); var cam = r.cam; var tr = cam.transform;
            var keep = tr.localRotation;
            if (pitch != 0 || yaw != 0) tr.rotation = Quaternion.Euler(pitch, tr.eulerAngles.y + yaw, 0);
            var bg = cam.backgroundColor; var clear = cam.clearFlags; var tgt = cam.targetTexture;
            const int W = 1280, H = 800;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 2 };
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = tgt; cam.backgroundColor = bg; cam.clearFlags = clear; tr.localRotation = keep;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
            // 状态
            var e = r.ending; float dimA = -1;
            if (r.dim && r.dim.activeInHierarchy)
            {
                var dr = r.dim.GetComponent<Renderer>(); var mpb = new MaterialPropertyBlock(); dr.GetPropertyBlock(mpb);
                dimA = mpb.isEmpty ? dr.sharedMaterial.GetFloat("_Alpha") : mpb.GetFloat("_Alpha");
            }
            float halo = -1;
            if (e.halo && e.halo.enabled) { var mpb = new MaterialPropertyBlock(); e.halo.GetPropertyBlock(mpb); halo = mpb.GetFloat("_Fade"); }
            log.AppendLine($"{name}\tt={t:0.00}\tsunAng={(e.halo && e.halo.enabled ? SunAngle(e.halo, cam) : -1):0}\tglare={(e.halo && e.halo.enabled ? GlareOf(e.halo) : -1):0.00}\tui={Object.FindAnyObjectByType<UIFlow>().DebugState.Replace(" ", "_")}\tgame={(r.game != null ? r.game.layerIdx + "_" + r.game.state : "null")}\trift={(r.rift && r.rift.view.enabled)}\tending={e.Active}\tdim={dimA:0.00}\thalo={halo:0.00}\tlids={r.lids.view.enabled}\tcard={e.card.Open}\tamb={(e.amb ? e.amb.volume : -1):0.00}\thospital={(r.hospital.Length > 0 && r.hospital[0] ? r.hospital[0].activeInHierarchy : false)}\ttiles={r.tiles.gameObject.activeInHierarchy}");
        }
    }
}
