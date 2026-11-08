// 手背判定自检：用 OpenXR 的手部骨架约定摆出“看手表”和“掌心朝上”两种姿势，
// 检查 PicoInput 里那个叉积公式算出来的手背方向对不对。-executeMethod DLD.EditorTools.WristCheck.Run，结果在 Logs/wrist_check.txt
using System.IO;
using System.Text;
using UnityEngine;

namespace DLD.EditorTools
{
    public static class WristCheck
    {
        /// <summary>和 PicoInput 里完全相同的公式</summary>
        static Vector3 Back(int side, Vector3 wrist, Vector3 idx, Vector3 lit)
        {
            var n = Vector3.Cross(idx - wrist, lit - wrist).normalized * (side == 0 ? 1 : -1);
            return -n;
        }

        /// <summary>
        /// 摆一只手：手腕在 wrist，手指指向 fingers，掌心朝 palmDir。
        /// 食指根在拇指一侧：右手掌心朝下、手指朝前时拇指在左边；左手相反
        /// </summary>
        static (Vector3 w, Vector3 i, Vector3 l) Pose(int side, Vector3 wrist, Vector3 fingers, Vector3 palmDir)
        {
            fingers.Normalize(); palmDir.Normalize();
            // 右手：掌心朝下(−Y)、手指朝前(+Z) 时，拇指（食指那一侧）在 −X
            var thumbSide = Vector3.Cross(palmDir, fingers).normalized * (side == 1 ? 1 : -1);
            var idx = wrist + fingers * .09f + thumbSide * .035f;
            var lit = wrist + fingers * .08f - thumbSide * .035f;
            return (wrist, idx, lit);
        }

        public static void Run()
        {
            var sb = new StringBuilder(); var eye = new Vector3(0, 1.6f, 0);
            foreach (int side in new[] { 0, 1 })
            {
                string hand = side == 0 ? "左手" : "右手";
                var wrist = new Vector3(side == 0 ? -.1f : .1f, 1.25f, .3f);
                var toEye = (eye - wrist).normalized;
                // 1) 看手表：手背朝眼睛 → 掌心背离眼睛
                var p = Pose(side, wrist, new Vector3(side == 0 ? 1 : -1, 0, 0), -toEye);
                float d1 = Vector3.Dot(Back(side, p.w, p.i, p.l), toEye);
                // 2) 掌心朝上（PICO 系统菜单的手势）
                p = Pose(side, wrist, Vector3.forward, Vector3.up);
                float d2 = Vector3.Dot(Back(side, p.w, p.i, p.l), toEye);
                // 3) 掌心朝自己
                p = Pose(side, wrist, Vector3.up, toEye);
                float d3 = Vector3.Dot(Back(side, p.w, p.i, p.l), toEye);
                // 4) 自然垂手/往前伸（掌心朝下）
                p = Pose(side, wrist, Vector3.forward, Vector3.down);
                float d4 = Vector3.Dot(Back(side, p.w, p.i, p.l), toEye);
                sb.Append($"{hand}: 看手表 {d1:0.00}（要 >0.72） | 掌心朝上 {d2:0.00}（要低） | 掌心朝自己 {d3:0.00}（要低） | 手平伸掌心朝下 {d4:0.00}\n");
            }
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/wrist_check.txt", sb.ToString());
        }
    }
}
