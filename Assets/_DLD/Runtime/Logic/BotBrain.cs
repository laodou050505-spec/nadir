using System;
using System.Collections.Generic;
using System.Linq;

namespace DLD.Logic
{
    /// <summary>
    /// 机器人玩家：看见裂纹（有反应时间）后换砖、寻路（能跨一格洞）、去拿碎片和进门，
    /// 盯住碎片后捏合隔空抓取、把贴近边缘的黑影推回去、手扑过来时有概率拍开、眼睛睁开时看远处。
    /// 平衡模拟和 Play 模式自动测试共用
    /// </summary>
    public class BotBrain
    {
        public static readonly double[] Novice = { .75, .75, 1.05, .6, .7, .10, .4, .25 }, Normal = { .5, .95, 1.25, .8, .45, .05, .6, .5 }, Skilled = { .3, 1.15, 1.45, .95, .3, .02, .8, .75 };
        class Swat { public AbyssHand hand; public double at; public bool ok; public int k; }

        readonly Game g; readonly Rng R; readonly double react, bspeed, bstep, bswat, swatReact, misLook, bcover, bpoint;
        public double mx, mz, t, misLookT, coverT, pullSep, pushK, pushHold;
        readonly Dictionary<Tile, double> seen = new(); List<Tile> path = new(); Swat plan;
        double DT;

        public BotBrain(Game game, double[] b, long seed)
        {
            g = game; R = new Rng(seed * 13 + 5);
            react = b[0]; bspeed = b[1]; bstep = b[2]; bswat = b[3]; swatReact = b[4]; misLook = b[5]; bcover = b[6]; bpoint = b[7];
        }
        Floor F() => g.floor;
        bool Known(Tile x) => x.state == TileState.Crack && seen.ContainsKey(x) && t - seen[x] >= react;
        bool Safe(Tile x) => x != null && (x.state == TileState.Solid || (x.state == TileState.Rise && x.t > Cfg.RiseTime - .3)) && !Known(x) && !(x == g.stay?.tile && g.stay.t > Cfg.StayLimit - 1);

        List<Tile> Route(Tile from, Func<Tile, bool> goal)
            {
                var fl = F(); var q = new List<Tile> { from }; var prev = new Dictionary<Tile, Tile> { [from] = null }; int head = 0;
                var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };
                while (head < q.Count)
                {
                    var c = q[head++];
                    if (goal(c) && c != from)
                    {
                        var o = new List<Tile>(); var x = c;
                        while (x != null && x != from) { o.Insert(0, x); x = prev[x]; }
                        return o;
                    }
                    var nb = new List<Tile>();
                    foreach (var (di, dj) in dirs)
                    {
                        var n = fl.At(c.i + di, c.j + dj);
                        if (n != null && Safe(n)) nb.Add(n);
                        else if (n != null && !Floor.Supportive(n) && Math.Abs(di) + Math.Abs(dj) == 1)
                        {
                            var far = fl.At(c.i + 2 * di, c.j + 2 * dj);
                            if (far != null && Safe(far)) nb.Add(far);
                        }
                    }
                    foreach (var n in nb) if (!prev.ContainsKey(n)) { prev[n] = c; q.Add(n); }
                }
                return null;
            }


        /// <summary>算这一帧的输入。调用方跑完 Game.Update 后要调 After()</summary>
        public PlayerInput Step(double dt)
        {
            DT = dt;
            t += DT;
            var fl = F();
            foreach (var x in fl.tiles) { if (x.state == TileState.Crack) { if (!seen.ContainsKey(x)) seen[x] = t; } else seen.Remove(x); }
            var cur = fl.TileAt(mx, mz);

            // 目标：门 > 最近的碎片 > 原地
            Func<Tile, bool> goal = null; Shard reach = null;
            if (g.door != null && g.door.ready) { var doorTile = g.door.tile; goal = x => x == doorTile; }
                else if (g.door != null)
                {
                    var dt2 = g.door.tile;
                    if (V3.Hypot(dt2.x - mx, dt2.z - mz) > .8) goal = x => x != dt2 && V3.Hypot(x.x - dt2.x, x.z - dt2.z) < .8 && Safe(x);
                }
            else if (g.shards.Count > 0)
            {
                Shard best = null; double bd = double.PositiveInfinity;
                foreach (var s in g.shards) { double d = V3.Hypot(s.pos.x - mx, s.pos.z - mz); if (d < bd) { bd = d; best = s; } }
                reach = best;
                if (bd > .5) goal = x => V3.Hypot(x.x - best.pos.x, x.z - best.pos.z) < .45 && Safe(x);
            }
            bool danger = cur != null && !Safe(cur);
            if (g.state == "play" && (danger || (path.Count == 0 && goal != null)))
            {
                var p = goal != null ? Route(cur ?? fl.TileAt(0, 0), goal) : null;
                if (p != null && p.Count > 0) path = p;
                else if (danger) path = Route(cur, Safe) ?? new List<Tile>();
            }
            if (path.Count > 0 && !Safe(path[0]) && path[0] != cur) path = new List<Tile>();
            // 走路（跨洞时步子快）
            if (path.Count > 0 && g.state == "play")
            {
                var w = path[0];
                double dx = w.x - mx, dz = w.z - mz, l = V3.Hypot(dx, dz);
                var under = fl.TileAt(mx, mz);
                double sp = under != null && Floor.Supportive(under) ? bspeed : bstep;
                if (l < .04) path.RemoveAt(0);
                else { double m = Math.Min(l, sp * DT) / l; mx += dx * m; mz += dz * m; }
            }

            // 视线
            var gaze = new V3(0, -.6, -1);
            if (reach != null) gaze = new V3(reach.pos.x - mx, reach.pos.y - 1.6, reach.pos.z - mz);
            bool eyeOpen = g.eye.phase == "open"; bool cover = false;
            if (eyeOpen)
            {
                if (misLookT <= 0 && R.Next() < misLook * DT * 10) misLookT = .9;
                if (misLookT > 0)
                {
                    misLookT -= DT;
                    var hole = fl.tiles.FirstOrDefault(x => !Floor.Supportive(x) && x.state != TileState.Rise);
                    if (hole != null) gaze = new V3(hole.x - mx, -1.6, hole.z - mz);
                    if (g.eye.seen > .4 && R.Next() < bcover) coverT = 1.2;
                }
                else gaze = new V3(0, .05, -1);
            }
            if (coverT > 0) { coverT -= DT; cover = true; }

            // 手：拍开 / 收集
            V3? right = null, left = null; bool rightPinch = false;
            var threat = g.hands.FirstOrDefault(h => (h.phase == "reach" || h.phase == "grab") && V3.Hypot(h.pos.x - mx, h.pos.z - mz) < .75);
            if (threat != null && (plan == null || plan.hand != threat)) plan = new Swat { hand = threat, at = t + swatReact, ok = R.Next() < bswat, k = 0 };
            if (plan != null && t >= plan.at && plan.hand.phase != "recoil")
            {
                var h = plan.hand;
                if (plan.ok)
                {
                    right = plan.k == 0 ? new V3(h.pos.x - .12, h.pos.y, h.pos.z) : h.pos;
                    plan.k++; if (plan.k > 1) plan = null;
                }
                else { plan.at = t + swatReact; plan.ok = R.Next() < bswat; }
            }
            if (right == null && reach != null && V3.Hypot(reach.pos.x - mx, reach.pos.z - mz) < .6) right = reach.pos;
            // 隔空抓取：碎片被盯住锁定后，手放在身前捏合
            if (right == null && reach != null && reach.locked && !eyeOpen) { right = new V3(mx, 1.2, mz - .25); rightPinch = true; }
            if (cover) left = new V3(mx, 1.55, mz - .08);
            // 推黑影：有空闲的手时，把离边缘最近的那个推回去
            else if (pushHold > 0 || R.Next() < bpoint)
            {
                var figs = g.scares.effigies.Where(f => f.placed && (f.edgeT > 2 || f.r < g.L.figureMin * g.floor.s / .75 + .4)).OrderBy(f => f.r).ToList();
                if (figs.Count > 0)
                {
                    var f = figs[0]; double l = V3.Hypot(f.x - mx, f.z - mz); if (l == 0) l = 1;
                    // 一推就推完整一下：先在身前停一会儿让手“就位”，再推出去
                    if (pushHold <= 0) { pushHold = 1.2; pushK = 0; }
                    pushHold -= DT; pushK = pushHold > .75 ? 0 : Math.Min(1, pushK + DT * 3.2);
                    left = new V3(mx + (f.x - mx) / l * (.1 + .5 * pushK), 1.4, mz + (f.z - mz) / l * (.1 + .5 * pushK));      // 从身前推出去，不是瞬移
                }
            }

            // 走到裂缝跟前，双手捏住往两边拉
            bool leftPinch = false;
            if (g.door != null && !g.door.ready && g.door.t >= Cfg.Door.Rise && V3.Hypot(g.door.tile.x - mx, g.door.tile.z - mz) < 1.1 && right == null)
            {
                pullSep = Math.Min(pullSep + DT * .45, .62);
                var dc = g.door.tile;
                right = new V3(dc.x + pullSep * .5 + .05, 1.25, dc.z); left = new V3(dc.x - pullSep * .5 - .05, 1.25, dc.z); rightPinch = true; leftPinch = true;
            }
            else if (g.door == null) pullSep = 0;
            var head = new V3(mx, 1.6, mz);
            double gl = V3.Len(gaze); if (gl == 0) gl = 1;
            var dir = new V3(gaze.x / gl, gaze.y / gl, gaze.z / gl);
            // 机器人低头时头往前探，抵消脚位置估计的后收
            double back = Math.Min(1, Math.Max(0, -dir.y) * 1.2) * Cfg.Feet.LeanBack;
            double hl = V3.Hypot(dir.x, dir.z); if (hl == 0) hl = 1;
            head.x += dir.x / hl * back; head.z += dir.z / hl * back;
            var input = new PlayerInput
            {
                headPos = head, headFwd = dir, gazeOrigin = head, gazeDir = dir, hasGaze = true,
                right = right.HasValue ? new HandInput { tracked = true, palm = right.Value, pinch = rightPinch } : default,
                left = left.HasValue ? new HandInput { tracked = true, palm = left.Value, pinch = leftPinch } : default,
            };
            return input;
        }

        public void After() { if (g.state == "falling") path = new List<Tile>(); }
    }
}
