using ByteDance.PICO.XR;
using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 头显输入：PICO 手部追踪 + 眼动。手和眼都拿不到时退回头部朝向，游戏仍能进行。
    /// 新机型的接口差异都收在这一个文件里
    /// </summary>
    public class PicoInput : InputSource
    {
        public Camera cam; public Transform origin;
        [Tooltip("眼动平滑（秒）：眼跳很快，稍微平滑一点不抖，又不至于拖")] public float gazeSmoothing = .035f;
        const float PinchOn = .03f, PinchOff = .055f;   // 系统的捏合状态优先；距离只做补充，宽松一些，手指抖动不会断
        HandAimState aim;

        HandJointLocations locs = new() { jointLocations = new HandJointLocation[(int)HandJoint.JointMax] };
        EyeTrackingData eye = new() { eyeDatas = new PerEyeData[(int)PerEyeUsage.EyeCount] };
        bool eyeStarted, eyeTried, eyeOk, handsOk; readonly bool[] pinch = new bool[2]; Vector3 gazeDir;

        public override bool EyeTracked => eyeOk;
        public override bool HandsTracked => handsOk;

        void OnDisable()
        {
            if (eyeStarted) { var stop = new EyeTrackingStopInfo(); PXR_MotionTracking.StopEyeTracking(ref stop); eyeStarted = false; eyeTried = false; }
        }

        Vector3 World(Vector3 p) => origin ? origin.TransformPoint(p) : p;

        bool ReadHand(HandType type, int side, out HandInput h)
        {
            h = default;
            if (!PXR_HandTracking.GetJointLocations(type, ref locs) || locs.isActive == 0 || locs.jointLocations == null) return false;
            const ulong need = (ulong)HandLocationStatus.PositionValid;
            Vector3 P(HandJoint j, out bool ok)
            {
                var l = locs.jointLocations[(int)j]; ok = ((ulong)l.locationStatus & need) != 0;
                return World(l.pose.Position.ToVector3());
            }
            var palm = P(HandJoint.JointPalm, out bool a); var thumb = P(HandJoint.JointThumbTip, out bool b); var tip = P(HandJoint.JointIndexTip, out bool c);
            if (!(a && b && c)) return false;
            float d = Vector3.Distance(thumb, tip);
            bool sys = PXR_HandTracking.GetAimState(type, ref aim) && (aim.aimStatus & HandAimStatus.AimIndexPinching) != 0;
            pinch[side] = sys || (pinch[side] ? d < PinchOff : d < PinchOn);
            // 手背朝向：手腕到食指根、小指根两条边的叉积就是掌心法线，左右手方向相反。
            // 掌心朝外、手背朝着眼睛，就是看手表的姿势
            var wrist = P(HandJoint.JointWrist, out bool w1); var idx = P(HandJoint.JointIndexProximal, out bool w2); var lit = P(HandJoint.JointLittleProximal, out bool w3);
            var back = Vector3.zero;
            if (w1 && w2 && w3)
            {
                var n = Vector3.Cross(idx - wrist, lit - wrist).normalized * (side == 0 ? 1 : -1);     // 掌心方向
                back = -n;                                                                                 // 手背方向
            }
            h = new HandInput { tracked = true, palm = ToV(palm), tip = ToV(tip), hasTip = true, pinch = pinch[side], back = ToV(back), wrist = ToV(w1 ? wrist : palm) };
            return true;
        }

        bool ReadEye(out Ray ray)
        {
            ray = default;
            if (!eyeTried)
            {
                eyeTried = true;
                var start = new EyeTrackingStartInfo { needCalibration = 0, mode = EyeTrackingMode.PXR_ETM_BOTH };
                eyeStarted = PXR_MotionTracking.StartEyeTracking(ref start) == 0;
            }
            if (!eyeStarted) return false;
            var info = new EyeTrackingDataGetInfo { displayTime = 0, flags = EyeTrackingDataGetFlags.PXR_EYE_POSITION | EyeTrackingDataGetFlags.PXR_EYE_ORIENTATION };
            if (PXR_MotionTracking.GetEyeTrackingData(ref info, ref eye) != 0 || eye.eyeDatas == null) return false;
            var cmb = eye.eyeDatas[(int)PerEyeUsage.Combined];
            if (cmb.isPoseValid == 0) return false;
            if (cmb.isOpennessValid != 0 && cmb.openness < .2f) return false;   // 闭眼 / 眨眼：这一帧不用眼动
            var o = new Vector3(cmb.pose.position.x, cmb.pose.position.y, -cmb.pose.position.z);
            var q = new Quaternion(cmb.pose.orientation.x, cmb.pose.orientation.y, -cmb.pose.orientation.z, -cmb.pose.orientation.w);
            var dir = origin ? origin.TransformDirection(q * Vector3.forward) : q * Vector3.forward;
            ray = new Ray(World(o), dir.normalized);
            return true;
        }

        public override PlayerInput Poll(float dt)
        {
            var t = cam.transform;
            var inp = new PlayerInput { headPos = ToV(t.position), headFwd = ToV(t.forward), hasGaze = true };
            bool active = PXR_HandTracking.GetActiveInputDevice() == ActiveInputDevice.HandTrackingActive;
            bool l = false, r = false;
            if (active) { l = ReadHand(HandType.HandLeft, 0, out inp.left); r = ReadHand(HandType.HandRight, 1, out inp.right); }
            handsOk = l || r;
            eyeOk = ReadEye(out var e);
            var target = eyeOk ? e : new Ray(t.position, t.forward);
            float k = 1f - Mathf.Exp(-dt / Mathf.Max(.001f, gazeSmoothing));
            gazeDir = gazeDir == Vector3.zero ? target.direction : Vector3.Slerp(gazeDir, target.direction, k);
            inp.gazeOrigin = ToV(target.origin); inp.gazeDir = ToV(gazeDir.normalized);
            return inp;
        }
    }
}
