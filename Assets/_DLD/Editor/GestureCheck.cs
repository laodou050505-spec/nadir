// 手势判定自检：无界面运行 -executeMethod DLD.EditorTools.GestureCheck.Run，结果写到 Logs/gesture_check.txt。
// 只用规则层，不碰场景，不需要打开 Editor
using System;
using System.IO;
using System.Text;
using DLD.Logic;

namespace DLD.EditorTools
{
    public static class GestureCheck
    {
        static PlayerInput In(V3 head, V3 fwd, V3? palm)
        {
            var i = new PlayerInput { headPos = head, headFwd = V3.Norm(fwd), hasGaze = true, gazeOrigin = head, gazeDir = V3.Norm(fwd) };
            if (palm.HasValue) i.right = new HandInput { tracked = true, palm = palm.Value, tip = new V3(palm.Value.x, palm.Value.y, palm.Value.z + .1), hasTip = true };
            return i;
        }

        /// <summary>一根触手伸出来停在玩家脚边，然后让手按轨迹动，数拍中几次</summary>
        static string Tentacle(string name, Func<double, V3> traj, double dur = 1.2)
        {
            var g = new Game(7, "medium") { handsOn = true }; g.StartLayer(5, 7); g.god = true;
            foreach (var f in g.scares.effigies) f.placed = false;
            var head = new V3(0, 1.6, 0); var down = new V3(0, -.75, .65);
            var h = new AbyssHand { id = 99, hp = 1, anchor = new V3(.35, 0, .55), pos = new V3(.2, .38, .3), phase = "reach", cool = 0 };
            g.hands.Clear(); g.hands.Add(h);
            for (int i = 0; i < 20; i++) { g.Update(1 / 30.0, In(head, down, null)); g.Drain(); h.phase = "reach"; h.pos = new V3(.2, .38, .3); h.t = 0; }
            int hits = 0;
            for (int i = 0; i < dur * 30; i++)
            {
                if (h.phase != "recoil") { h.pos = new V3(.2, .38, .3); h.t = 0; }
                g.Update(1 / 30.0, In(head, down, traj(i / 30.0)));
                foreach (var e in g.Drain()) if (e.type == "swat") hits++;
            }
            return $"{name}: 拍中 {hits}\n";
        }

        static string Eye(string name, Func<double, V3> traj, V3 look, double dur = 1.4)
        {
            var g = new Game(9, "medium"); g.StartLayer(5, 9); g.god = true;
            foreach (var f in g.scares.effigies) f.placed = false;
            var head = new V3(0, 1.6, 0); g.OpenEyeNow();
            for (int i = 0; i < 150; i++) { g.Update(1 / 30.0, In(head, look, null)); g.Drain(); }
            int sw = 0;
            for (int i = 0; i < dur * 30; i++) { g.Update(1 / 30.0, In(head, look, traj(i / 30.0))); foreach (var e in g.Drain()) if (e.type == "eyeSwat") sw++; }
            return $"{name}: 拍眼 {sw} (eye={g.eye.phase})\n";
        }

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.Append("== 触手（脚边，梢部在 (.2,.38,.3)，洞口在 (.35,0,.55)）\n");
            sb.Append(Tentacle("从上往下一拍到梢部", t => new V3(.2, Math.Max(.4, 1.1 - t * 2.4), .3)));
            sb.Append(Tentacle("在梢部上方 45 厘米横挥", t => new V3(-.25 + t * 1.4, .83, .32)));
            sb.Append(Tentacle("快速横扫过身体中段", t => new V3(-.4 + t * 2.2, .3, .45)));
            sb.Append(Tentacle("一帧跨过去的快挥", t => t < .5 ? new V3(-.5, .45, .3) : new V3(.9, .45, .3)));
            sb.Append(Tentacle("手放在梢部旁边不动", t => new V3(.25, .5, .3)));
            sb.Append(Tentacle("在胸前挥手（不该触发）", t => new V3(-.3 + t * 1.2, 1.3, .35)));
            sb.Append(Tentacle("在腰间挥手", t => new V3(-.3 + t * 1.2, .95, .35)));
            sb.Append(Tentacle("在 70 厘米外快挥", t => new V3(-.3 + t * 1.5, .5, 1.0)));
            var down = new V3(0, -.6, .8);
            sb.Append("== 巨眼（低头）\n");
            sb.Append(Eye("朝破洞横挥", t => new V3(t < .6 ? -.3 : -.3 + (t - .6) * 2.2, 1.0, .45), down));
            sb.Append(Eye("往下一拍", t => new V3(.1, t < .5 ? 1.3 : Math.Max(.6, 1.3 - (t - .5) * 2.4), .45), down));
            sb.Append(Eye("快速抬手", t => new V3(.15, .5 + Math.Min(t * 4, 1) * .95, .5), down));
            sb.Append(Eye("手伸着不动", t => new V3(.05, 1.0, .5), down));
            sb.Append(Eye("平视时横挥（不该触发）", t => new V3(t < .6 ? -.3 : -.3 + (t - .6) * 2.2, 1.0, .45), new V3(0, 0, 1)));
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/gesture_check.txt", sb.ToString());
        }

        /// <summary>无界面平衡模拟：-executeMethod DLD.EditorTools.GestureCheck.Balance，结果在 Logs/balance.txt</summary>
        public static void Balance()
        {
            var sb = new StringBuilder(); string[] bn = { "新手", "普通", "熟练" }; var bs = new[] { BotBrain.Novice, BotBrain.Normal, BotBrain.Skilled };
            foreach (var area in new[] { "medium", "large" })
            {
                sb.Append("== " + area + "\n");
                for (int l = 0; l < Layers.All.Length; l++)
                {
                    sb.Append("层" + (l + 1) + ": ");
                    for (int b = 0; b < 3; b++)
                    {
                        int ok = 0, f = 0; double t = 0;
                        for (int r = 0; r < 8; r++) { var res = LogicSim.PlayOnce(l, bs[b], 1000 + r * 7 + l * 101, area); if (res.ok) { ok++; t += res.time; } f += res.falls; }
                        sb.Append($"{bn[b]} {ok * 100 / 8}% {(ok > 0 ? t / ok : 0):0}s 掉{f / 8.0:0.0} | ");
                    }
                    sb.Append("\n");
                }
            }
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/balance.txt", sb.ToString());
        }
    }
}
