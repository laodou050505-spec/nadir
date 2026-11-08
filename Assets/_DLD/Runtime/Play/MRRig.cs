using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.XR;

namespace DLD.Play
{
    /// <summary>头显启动：追踪原点设为地面，地图对到系统安全区中心。透视模式下相机清屏全透明</summary>
    public class MRRig : MonoBehaviour
    {
        public Camera cam; public bool passthrough;
        public Vector3 basePos;                // 对齐后的原点，下沉和坠落都叠加在它上面
        bool aligned, floorSet; float retry;

        void Start() => Apply();
        void OnApplicationPause(bool paused) { if (!paused) Apply(); }

        public void Apply()
        {
            if (Application.isEditor || !XRSettings.isDeviceActive) return;
            PXR_Manager.EnableVideoSeeThrough = passthrough;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = passthrough ? new Color(0, 0, 0, 0) : Color.black;
        }

        /// <summary>系统安全区里放得下的最大正方形边长（米），0 表示读不到</summary>
        public float SafeSide { get; private set; }
        bool hasRect, rectDone; Vector3 rectCenter; float rectYaw;

        /// <summary>读系统边界。PlayArea 是用户自定义边界里的最大矩形；原地模式没有矩形，用外轮廓估一个内接正方形</summary>
        void ReadBoundary()
        {
            hasRect = false; SafeSide = 0; outline = null;
            var ol = PXR_Boundary.GetGeometry(BoundaryType.OuterBoundary); if (ol != null && ol.Length >= 3) outline = ol;
            var dim = PXR_Boundary.GetDimensions(BoundaryType.PlayArea);
            var pts = dim.x > .1f && dim.z > .1f ? PXR_Boundary.GetGeometry(BoundaryType.PlayArea) : null;
            if (pts != null && pts.Length >= 3)
            {
                var c = Vector3.zero; foreach (var p in pts) c += p; c /= pts.Length;
                var e = pts[1] - pts[0];
                hasRect = true; rectCenter = c; rectYaw = Mathf.Atan2(e.x, e.z) * Mathf.Rad2Deg; SafeSide = Mathf.Min(dim.x, dim.z);
            }
            else
            {
                var o = PXR_Boundary.GetGeometry(BoundaryType.OuterBoundary);
                if (o != null && o.Length >= 3)
                {
                    var c = Vector3.zero; foreach (var p in o) c += p; c /= o.Length;
                    float r = float.MaxValue; foreach (var p in o) r = Mathf.Min(r, new Vector2(p.x - c.x, p.z - c.z).magnitude);
                    SafeSide = r * 1.414f;
                }
            }
            Debug.Log($"[NADIR] 系统边界 dim={dim} rect={hasRect} safe={SafeSide:F2} center={rectCenter} yaw={rectYaw:F1} head={cam.transform.localPosition}");
        }

        /// <summary>
        /// 规则层假定地图中心在原点。有矩形安全区时把地图中心对到安全区中心，朝向沿矩形的边（取最接近玩家面朝的一条），
        /// 这样地图永远在系统边界里，和站在哪里开局无关；没有矩形时才退回“玩家站的地方就是中心”
        /// </summary>
        public void Align()
        {
            if (!XRSettings.isDeviceActive) return;
            ReadBoundary();
            if (hasRect && rectDone) return;
            var lp = transform.InverseTransformPoint(cam.transform.position);
            var lf = Quaternion.Inverse(transform.rotation) * cam.transform.forward; lf.y = 0;
            if (lf.sqrMagnitude < 1e-4f) return;
            float lyaw = Mathf.Atan2(lf.x, lf.z) * Mathf.Rad2Deg;
            if (hasRect) { lyaw = rectYaw + Mathf.Round((lyaw - rectYaw) / 90f) * 90f; lp = rectCenter; rectDone = true; }
            var rot = Quaternion.Euler(0, -lyaw, 0);
            transform.rotation = rot;
            var q = -(rot * lp);
            basePos = new Vector3(q.x, 0, q.z); transform.position = basePos;
            aligned = true;
        }

        /// <summary>
        /// SDK 不能改系统边界（只能读、隐藏、关闭），能做的是把玩家送到系统的边界设置。
        /// 成功返回 true；回到游戏时 OnApplicationPause 会重新读取
        /// </summary>
        public bool OpenSystemBoundary()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var act = up.GetStatic<AndroidJavaObject>("currentActivity");
                using var it = new AndroidJavaObject("android.content.Intent");
                it.Call<AndroidJavaObject>("setClassName", "com.pico.guardian", "com.pico.guardian.MainActivity");
                act.Call("startActivity", it);
                return true;
            }
            catch (System.Exception e) { Debug.LogWarning("[NADIR] 打不开系统边界设置：" + e.Message); }
#endif
            return false;
        }

        Vector3[] outline;
        /// <summary>这个世界坐标是否在系统安全边界里，离边界至少留 margin 米。读不到边界时一律算在里面</summary>
        public bool InSafe(double x, double z)
        {
            const float margin = .3f;
            if (outline == null) return true;
            var p = transform.InverseTransformPoint(new Vector3((float)x, 0, (float)z)); bool inside = false; float best = 1e9f;
            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
            {
                var a = outline[i]; var b = outline[j];
                if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x) inside = !inside;
                var ab = new Vector2(b.x - a.x, b.z - a.z); var ap = new Vector2(p.x - a.x, p.z - a.z);
                float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f)); best = Mathf.Min(best, (ap - ab * t).magnitude);
            }
            return inside && best >= margin;
        }

        /// <summary>回到菜单时重新对一次（玩家可能在系统里重设过边界）</summary>
        public void Realign() { rectDone = false; Align(); }

        void Update()
        {
            if (!aligned && XRSettings.isDeviceActive && cam.transform.localPosition.sqrMagnitude > .01f) Align();
            if (floorSet || !XRSettings.isDeviceActive) return;
            retry -= Time.unscaledDeltaTime;
            if (retry > 0) return;
            retry = 1;
            var subs = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subs);
            foreach (var s in subs)
                if ((s.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) != 0)
                    floorSet |= s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        }
    }
}
