using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DLD.Logic;

namespace DLD.EditorTools
{
    /// <summary>平衡模拟：三档机器人跑每一层，和 Three.js 原型同种子对比，确认移植没有改变规则</summary>
    public static class LogicSim
    {
        class Bot { public string name; public double react, speed, step, swat, swatReact, misLook, cover, point; }
        static readonly Bot[] Bots =
        {
            new Bot { name = "新手", react = .75, speed = .75, step = 1.05, swat = .6, swatReact = .7, misLook = .10, cover = .4, point = .25 },
            new Bot { name = "普通", react = .5, speed = .95, step = 1.25, swat = .8, swatReact = .45, misLook = .05, cover = .6, point = .5 },
            new Bot { name = "熟练", react = .3, speed = 1.15, step = 1.45, swat = .95, swatReact = .3, misLook = .02, cover = .8, point = .75 },
        };
        const double DT = 1.0 / 30;

        public struct Res { public bool ok; public double time; public int falls, stars, spotted; }

        public static Res PlayOnce(int idx, double[] b, long seed, string area = "medium")
        {
            var g = new Game(seed, area); g.StartLayer(idx, seed);
            var bot = new BotBrain(g, b, seed);
            while (bot.t < 400 && (g.state == "play" || g.state == "falling"))
            {
                var input = bot.Step(DT);
                g.Update(DT, input);
                g.Drain();
                bot.After();
            }
            return new Res { ok = g.state == "clear", time = bot.t, falls = g.falls, stars = g.result?.stars ?? 0, spotted = g.eye.spotted };
        }

        /// <summary>教学能不能走完，走多久</summary>
        public static Res PlayTutorial(double[] b, long seed)
        {
            var g = new Game(seed, "medium"); g.StartTutorial();
            var bot = new BotBrain(g, b, seed);
            while (bot.t < 400 && g.state == "play") { var input = bot.Step(DT); g.Update(DT, input); g.Drain(); bot.After(); }
            return new Res { ok = g.state == "clear", time = bot.t, falls = g.falls };
        }

        public static string Csv(int runs)
        {
            var sb = new StringBuilder();
            for (int idx = 0; idx < Layers.All.Length; idx++)
                foreach (var b in Bots)
                    for (int r = 0; r < runs; r++)
                    {
                        var res = PlayOnce(idx, new[] { b.react, b.speed, b.step, b.swat, b.swatReact, b.misLook, b.cover, b.point }, 1000 + r * 7 + idx * 101);
                        sb.Append(idx).Append(',').Append(b.name).Append(',').Append(r).Append(',').Append(res.ok ? 1 : 0).Append(',').Append(res.time.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(',').Append(res.falls).Append(',').Append(res.stars).Append(',').Append(res.spotted).Append('\n');
                    }
            return sb.ToString();
        }
    }
}
