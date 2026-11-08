using System;
using System.Collections.Generic;
using System.Linq;

namespace DLD.Logic
{
    /// <summary>
    /// 惊吓导演：不改变规则，只制造恐惧。
    /// 一定有铺垫（先预兆，隔几秒才发生）；逐层升级；没有贴脸和巨响，一切缓入缓出
    /// </summary>
    public class ScareDirector
    {
        static readonly Dictionary<string, double[]> Rate = new() { ["normal"] = new[] { 18.0, 28 }, ["horror"] = new[] { 11.0, 17 } };
        static readonly (string k, int from, double normal, double horror)[] Kinds =
        {
            ("whisper", 0, 3, 2), ("knock", 0, 3, 2), ("beep", 0, 2, 2), ("footprints", 1, 2, 3), ("blackout", 2, 2, 3),
        };
        public static double Omen(string k) => k switch { "whisper" => 2.5, "knock" => 2, "beep" => 3, "footprints" => 4, "blackout" => 4, _ => 0 };

        readonly Game g;
        public double next, black, edgeCd;
        public readonly List<Effigy> effigies = new();
        public StepsState steps;
        public PendingScare pending;

        public ScareDirector(Game game)
        {
            g = game;
            next = g.rng.Range(12, 16);    // 开局先安静一会儿
            if (g.tut == null) SpawnEffigies(); else effigies.Add(new Effigy { id = 0, rustleCd = 9, turnT = 99, placed = false });
        }

        public void Update(double dt, in PlayerInput input)
        {
            if (black > 0)
            {
                black -= dt;
                if (black <= 0) g.Emit(new GameEvent("scare") { kind = "lightsOn" });
            }
            UpdateEffigies(dt, input);
            if (g.tut != null) return;
            UpdateSteps(dt);
            if (pending != null)
            {
                pending.t -= dt;
                if (pending.t <= 0) { var p = pending; pending = null; Fire(p.kind, input, p); }
                return;
            }
            next -= dt;
            if (next > 0) return;
            next = g.rng.Range(Rate[g.mood]);
            // 眼睛睁开、被抓住时不叠加，让玩家专心应付
            if (g.eye.phase == "open" || g.eye.phase == "warn" || g.grabbed != null) { next = 4; return; }
            Prepare(Pick(), input);
        }

        string Pick()
        {
            var w = new List<(string, double)>();
            foreach (var (k, from, normal, horror) in Kinds) if (g.layerIdx >= from) w.Add((k, (g.mood == "horror" ? horror : normal) * (k == "blackout" ? g.L.blackoutK : 1)));
            return g.rng.Weighted(w);
        }

        /// <summary>给预兆：声音（和灯光）先到，事情过几秒才发生</summary>
        void Prepare(string kind, in PlayerInput input)
        {
            if ((kind == "footprints" && steps != null) || (kind == "blackout" && black > 0)) return;
            var f = V3.Norm(new V3(input.headFwd.x, 0, input.headFwd.z));
            var pos = new V3(input.headPos.x - f.x * 3.5, 1.2, input.headPos.z - f.z * 3.5);
            if (kind == "knock")
            {
                var solid = g.floor.tiles.Where(x => x.state == TileState.Solid).ToList();
                if (solid.Count == 0) return;
                var t = g.rng.Pick(solid);
                pos = new V3(t.x, -.2, t.z);
            }
            pending = new PendingScare { kind = kind, t = Omen(kind), pos = pos, dur = Omen(kind) };
            g.Emit(new GameEvent("scare") { kind = "omen", what = kind, pos = pos, dur = Omen(kind) });
        }

        void Fire(string kind, in PlayerInput input, PendingScare p)
        {
            if (kind == "footprints" && steps == null) SpawnSteps();
            else if (kind == "blackout" && black <= 0)
            {
                black = g.rng.Range(2.2, 3.4);
                g.nextCollapse += black;       // 黑暗里不新增塌陷，公平
                g.Emit(new GameEvent("scare") { kind = "blackout", dur = black });
            }
            else if (kind == "whisper")
            {
                var f = V3.Norm(new V3(input.headFwd.x, 0, input.headFwd.z));
                g.Emit(new GameEvent("scare") { kind = "whisper", pos = new V3(input.headPos.x - f.x * .9, input.headPos.y, input.headPos.z - f.z * .9) });
            }
            else if (kind == "knock" && p != null) g.Emit(new GameEvent("scare") { kind = "knock", pos = p.pos });
            else if (kind == "beep" && p != null) g.Emit(new GameEvent("scare") { kind = "beep", pos = p.pos });
        }

        /// <summary>无脸人围在四周：数量随层数增加；恐怖版更多、走得更快</summary>
        void SpawnEffigies()
        {
            int n = Math.Min(12, 1 + g.layerIdx + (g.mood == "horror" ? 2 : 0));
            double b = g.rng.Range(0, Math.PI * 2);
            for (int i = 0; i < n; i++)
            {
                double a = b + (double)i / n * Math.PI * 2 + g.rng.Range(-.2, .2);
                // 从四条走廊的深处走出来
                int side = i % 4; double far = 9 + i * 2.1 + g.rng.Range(0, 7), lane = g.rng.Range(-.8, .8);
                var f = new Effigy { id = i, a = a, rustleCd = g.rng.Range(3, 9), variant = i % 2, turnT = g.rng.Range(Cfg.Figure.TurnMin, Cfg.Figure.TurnMax) };
                (f.x, f.z) = side switch { 0 => (lane, far), 1 => (far, lane), 2 => (lane, -far), _ => (-far, lane) };
                f.r = V3.Hypot(f.x, f.z); f.placed = true;
                // 第一段：直走到走廊口里面
                (f.tx, f.tz) = side switch { 0 => (lane * .7, 5.0), 1 => (5.0, lane * .7), 2 => (lane * .7, -5.0), _ => (-5.0, lane * .7) };
                effigies.Add(f);
            }
        }

        /// <summary>教学：一个黑影站到玩家正前方</summary>
        public Effigy PlaceAhead(in PlayerInput input, double dist)
        {
            var f = effigies[0]; double l = V3.Hypot(input.headFwd.x, input.headFwd.z), fx = l < .01 ? 0 : input.headFwd.x / l, fz = l < .01 ? 1 : input.headFwd.z / l;
            f.x = g.feet.x + fx * dist; f.z = g.feet.z + fz * dist; f.r = V3.Hypot(f.x, f.z); f.a = System.Math.Atan2(f.x, f.z); f.inHall = true; f.tx = f.x; f.tz = f.z; f.placed = true;
            return f;
        }

        double MinR => g.L.figureMin * g.floor.s / .75;     // 地图小时，挤的距离也跟着缩
        public V3? castFrom;                                 // 这一帧正在施法的手

        /// <summary>进了大厅后的目标：朝自己的方位直走，每次拐弯只偏一点</summary>
        void Retarget(Effigy f)
        {
            f.a += g.rng.Range(-Cfg.Figure.TurnAngle, Cfg.Figure.TurnAngle);
            double r = MinR;
            f.tx = Math.Sin(f.a) * r; f.tz = Math.Cos(f.a) * r;
            f.turnT = g.rng.Range(Cfg.Figure.TurnMin, Cfg.Figure.TurnMax);
        }

        /// <summary>
        /// 击退黑影：手朝它推出去才算。手只是抬起来、刚进视野，或者已经伸着不动，都不算。
        /// 一旦推中，手还对着它的这一小会儿里持续施力
        /// </summary>
        bool PointedAt(Effigy f, in PlayerInput input, out V3 palm, double dt)
        {
            palm = default;
            var o = input.headPos; f.pushT = Math.Max(0, f.pushT - dt);
            for (int side = 0; side < 2; side++)
            {
                if (!g.palm[side].HasValue || g.pinching[side]) continue;
                var d = g.palm[side].Value - o;
                if (V3.Len(d) < Cfg.Figure.ArmOut) continue;
                if (V3.AngleTo(o, V3.Norm(d), new V3(f.x, 1.1, f.z)) >= Cfg.Figure.PointCone) continue;
                palm = g.palm[side].Value;
                if (f.pushing && f.pushT > 0) return true;
                if (g.handAge[side] < Cfg.Figure.ThrustReady) continue;
                var to = V3.Norm(new V3(f.x, 1.1, f.z) - palm);
                if (V3.Dot(g.vel[side], to) >= Cfg.Figure.ThrustSpeed) { f.pushT = Cfg.Figure.ThrustKeep; return true; }
            }
            return false;
        }

        void UpdateEffigies(double dt, in PlayerInput input)
        {
            double near = (g.mood == "horror" ? Cfg.Figure.NearSpeed * 1.35 : Cfg.Figure.NearSpeed) * (1 + g.layerIdx * .1) * (black > 0 ? 4 : 1);
            edgeCd = Math.Max(0, edgeCd - dt); castFrom = null;
            foreach (var f in effigies)
            {
                if (!f.placed) continue;
                f.rustleCd -= dt;
                f.hold = Math.Max(0, f.hold - dt);
                var chest = new V3(f.x, 1.1, f.z);
                // 你看着它就不动：眼动或脸朝着它都算
                bool seen = !g.cover && black <= 0 && (input.hasGaze && V3.AngleTo(input.gazeOrigin, V3.Norm(input.gazeDir), chest) < Cfg.Figure.SeenCone
                    || V3.AngleTo(input.headPos, V3.Norm(input.headFwd), chest) < Cfg.Figure.HeadCone);
                V3 fromPalm = default; bool pushed = black <= 0 && PointedAt(f, input, out fromPalm, dt);
                if (pushed) castFrom = fromPalm;
                if (pushed && !f.pushing)
                {
                    g.Emit(new GameEvent("scare") { kind = "push", pos = new V3(f.x, 1.2, f.z), from = fromPalm });
                    // 沿着从你到它的直线往后退
                    double dx = f.x - g.feet.x, dz = f.z - g.feet.z, l = V3.Hypot(dx, dz); if (l == 0) l = 1;
                    f.pushX = dx / l; f.pushZ = dz / l;
                }
                f.pushing = pushed;
                if (pushed) { f.pushV = Math.Max(f.pushV, f.pushing && f.pushV > 0 ? Cfg.Figure.PushSpeed : Cfg.Figure.PushHit); f.hold = Cfg.Figure.Hold; f.edgeT = 0; }
                if (f.pushV > 0)
                {
                    double nx = f.x + f.pushX * f.pushV * dt, nz = f.z + f.pushZ * f.pushV * dt;
                    if (Math.Max(Math.Abs(nx), Math.Abs(nz)) <= Cfg.Figure.PushFar) { f.x = nx; f.z = nz; } else f.pushV = 0;
                    f.pushV = pushed ? f.pushV : Math.Max(0, f.pushV - Cfg.Figure.PushDecay * dt);
                    f.r = V3.Hypot(f.x, f.z); f.look = 0; f.tilt = Math.Min(.3, f.tilt + dt * .6);
                    if (f.pushV <= 0) { f.inHall = true; f.a = Math.Atan2(f.x, f.z); Retarget(f); }
                    continue;
                }
                if (f.hold > 0) { f.tilt = Math.Max(0, f.tilt - dt * .2); continue; }
                if (seen)
                {
                    f.moved = false;
                    f.look += dt;
                    f.tilt = Math.Min(.3, f.look * .05);    // 盯久了，它慢慢歪过头
                    continue;
                }
                f.look = 0;
                f.tilt = Math.Max(0, f.tilt - dt * .2);
                if (f.inHall) { f.turnT -= dt; if (f.turnT <= 0) Retarget(f); }
                double tx = f.tx - f.x, tz = f.tz - f.z, dist = V3.Hypot(tx, tz);
                if (dist > .06)
                {
                    double speed = f.inHall ? near : Cfg.Figure.FarSpeed, step = Math.Min(dist, speed * dt);
                    double nx = f.x + tx / dist * step, nz = f.z + tz / dist * step;
                    // 永远不会贴到你身上：离脚边一臂多就停
                    if (V3.Hypot(nx - g.feet.x, nz - g.feet.z) >= Cfg.Figure.KeepAway) { f.x = nx; f.z = nz; f.r = V3.Hypot(nx, nz); }
                    // 它开始动时，你能听见轻轻的脚步声：这就是回头的理由
                    if (!f.moved && f.rustleCd <= 0)
                    {
                        f.moved = true; f.rustleCd = g.rng.Range(6, 11);
                        g.Emit(new GameEvent("scare") { kind = "rustle", pos = new V3(f.x, 1, f.z) });
                    }
                }
                else if (!f.inHall) { f.inHall = true; Retarget(f); }
                else
                {
                    // 走到头了：没人管它，它身边的地板就开始裂。站进地板的比站在外面的更快
                    f.edgeT += dt;
                    double lim = MinR < g.floor.half ? Cfg.Figure.EdgeTimeInside : Cfg.Figure.EdgeTime;
                    if (f.edgeT >= lim && edgeCd <= 0 && CrackNear(f)) { f.edgeT = 0; edgeCd = Cfg.Figure.EdgeGap; }
                }
            }
        }

        bool CrackNear(Effigy f)
        {
            Tile best = null; double bd = double.PositiveInfinity;
            foreach (var t in g.floor.tiles)
            {
                if (t.state != TileState.Solid || t.door) continue;
                double d = V3.Hypot(t.x - f.x, t.z - f.z);
                if (d < bd) { bd = d; best = t; }
            }
            return best != null && g.floor.Crack(best, new CrackOpts { cause = "figure", player = g.standing });
        }

        /// <summary>脚步：从区域外一步步走到你身边，走到面前停下</summary>
        void SpawnSteps()
        {
            double a = g.rng.Range(0, Math.PI * 2), R = g.floor.half + .8;
            steps = new StepsState { x = Math.Sin(a) * R, z = Math.Cos(a) * R, t = 0, n = 0, side = 1 };
        }

        void UpdateSteps(double dt)
        {
            var s = steps;
            if (s == null) return;
            s.t -= dt;
            if (s.t > 0) return;
            double dx = g.feet.x - s.x, dz = g.feet.z - s.z, l = V3.Hypot(dx, dz);
            if (l == 0) l = 1;
            if (l < .7 || s.n > 12) { g.Emit(new GameEvent("scare") { kind = "stepsEnd", pos = new V3(s.x, 1.2, s.z) }); steps = null; return; }
            s.side = -s.side;
            double px = s.x + (-dz / l) * .09 * s.side, pz = s.z + (dx / l) * .09 * s.side;
            g.Emit(new GameEvent("scare") { kind = "step", pos = new V3(px, 0, pz), yaw = Math.Atan2(dx, dz), side = s.side });
            s.x += dx / l * .42; s.z += dz / l * .42;
            s.n++;
            s.t = .8;
        }
    }
}
