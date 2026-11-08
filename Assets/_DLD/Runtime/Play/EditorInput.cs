using DLD.Logic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DLD.Play
{
    /// <summary>
    /// 编辑器键鼠：WASD 走，按住右键转头，鼠标当视线。
    /// 空格捏合，左键拍（手快速挥向鼠标指的位置），C 捂眼，F 伸手推开黑影，G 双手撕开裂缝
    /// </summary>
    public class EditorInput : InputSource
    {
        public Camera cam;
        float yaw, pitch, pullT; float swatT = 9;
        /// <summary>自检用：直接把头转到某个朝向</summary>
        public void DebugLook(float y, float p) { yaw = y; pitch = p; }
        public float Yaw => yaw;

        public override DLD.Logic.PlayerInput Poll(float dt)
        {
            var kb = Keyboard.current; var ms = Mouse.current; var t = cam.transform;
            if (ms != null && ms.rightButton.isPressed) { var d = ms.delta.ReadValue(); yaw += d.x * .15f; pitch = Mathf.Clamp(pitch - d.y * .15f, -85, 85); }
            t.rotation = Quaternion.Euler(pitch, yaw, 0);
            var move = Vector3.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed) move.z += 1; if (kb.sKey.isPressed) move.z -= 1; if (kb.dKey.isPressed) move.x += 1; if (kb.aKey.isPressed) move.x -= 1;
            }
            var f = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized; var r = Vector3.Cross(Vector3.up, f);
            t.position += (f * move.z + r * move.x) * 1.4f * dt;
            var ray = ms != null ? cam.ScreenPointToRay(ms.position.ReadValue()) : new Ray(t.position, t.forward);
            var inp = new DLD.Logic.PlayerInput { headPos = ToV(t.position), headFwd = ToV(t.forward), gazeOrigin = ToV(ray.origin), gazeDir = ToV(ray.direction), hasGaze = true };

            bool pinch = kb != null && kb.spaceKey.isPressed;
            var rest = t.position + t.forward * .3f - t.up * .25f + t.right * .15f;
            if (ms != null && ms.leftButton.wasPressedThisFrame) swatT = 0;
            swatT += dt;
            var palm = swatT < .25f ? Vector3.Lerp(rest, ray.origin + ray.direction * 1.2f, swatT / .12f) : rest;
            inp.right = new HandInput { tracked = true, palm = ToV(palm), pinch = pinch };
            // G：双手捏住往两边拉（测试传送门）
            if (kb != null && kb.gKey.isPressed)
            {
                pullT += dt; float sep = Mathf.Min(.1f + pullT * .5f, .7f); var c = t.position + f * .55f + Vector3.down * .35f;
                inp.right = new HandInput { tracked = true, palm = ToV(c + r * sep * .5f), pinch = true };
                inp.left = new HandInput { tracked = true, palm = ToV(c - r * sep * .5f), pinch = true };
                return inp;
            }
            pullT = 0;
            if (kb != null && kb.cKey.isPressed) inp.left = new HandInput { tracked = true, palm = ToV(t.position + t.forward * .08f) };
            else if (kb != null && kb.fKey.isPressed) inp.left = new HandInput { tracked = true, palm = ToV(t.position + ray.direction * .5f) };
            return inp;
        }
    }
}
