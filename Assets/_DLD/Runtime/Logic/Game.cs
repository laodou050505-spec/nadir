using System;
using System.Collections.Generic;
using System.Linq;

namespace DLD.Logic
{
    /// <summary>
    /// 《别看破洞》规则层：纯逻辑，不依赖 Unity。坐标系 = 游玩区域本地坐标（区域中心为原点，地面 y=0）。
    /// 每帧 Update(dt, input)，表现层用 Drain() 取事件。规则和 Three.js 原型逐条对应
    /// </summary>
    public class Game
    {
        public readonly long seed; public string area, mood;   // mood: normal 普通版 / horror 恐怖版（规则相同，氛围和惊吓不同）
        public string state = "menu";
        public List<GameEvent> events = new();
        public readonly Dictionary<int, int> fails = new();
        public bool god;

        public Rng rng; public int layerIdx; public Layer L; public Floor floor; public int assist;
        public System.Func<double, double, bool> safeAt;      // 这个位置是否在系统安全边界内（读不到边界时为空）
        public bool handsOn = true;      // 触手只在困难模式出现
        public double fallSeq = Cfg.Fall.Seq;      // 坠落多久，随难度变长，最短就是 Cfg.Fall.Seq
        public double pull, time, invuln, unsupported, shardDelay, handTimer, nextCollapse, lastBig, seq;
        public int lives, falls, collected, shardId, handId;
        public (double x, double z) feet; public Tile standing; public StayState stay; public bool oob;
        public List<Shard> shards; public DoorState door; public List<AbyssHand> hands; public EyeState eye;
        public bool cover; public V3?[] palm, fingertip, prevPalm = new V3?[2]; public bool[] pinching; public double[] speed, handAge; public V3[] vel;
        public AbyssHand grabbed; public ScareDirector scares; public ResultInfo result;
        public readonly HashSet<string> tipsShown = new();

        public Game(long seed = 1, string area = "medium", string mood = "normal") { this.seed = seed; this.area = area; this.mood = mood; }

        public void Emit(GameEvent e) => events.Add(e);
        public List<GameEvent> Drain() { var e = events; events = new(); return e; }
        void Flush() { events.AddRange(floor.events); floor.events.Clear(); }

        public Tutorial tut; public double quiet, spottedCd; bool spottedOnce;

        /// <summary>新手教学：一条固定的流程，走完进第一层</summary>
        public void StartTutorial() { tut = new Tutorial(this); god = true; Begin(0, Layers.Lesson, null); }
        public void StartLayer(int idx, long? seedOverride = null) { tut = null; god = false; Begin(idx, Layers.All[idx], seedOverride); }

        void Begin(int idx, Layer layer, long? seedOverride)
        {
            L = layer; quiet = 0;
            fails.TryGetValue(idx, out int fc);
            rng = new Rng(seedOverride ?? seed + idx * 977 + fc * 31);
            layerIdx = idx;
            floor = new Floor(Cfg.N, rng, Cfg.TileOf(area));
            // 失败辅助：同一层每失败一次，开裂预警 +0.2 秒（最多 +0.6）
            assist = Math.Min(3, fc);
            floor.crackTime = Cfg.CrackTime + assist * .2;
            state = "play"; time = 0; lives = Cfg.Fall.Lives; falls = 0; invuln = 0; unsupported = 0;
            feet = (0, 0); standing = null; stay = new StayState(); oob = false; collected = 0;
            shards = new(); shardId = 0; shardDelay = 2.5; door = null; hands = new(); handId = 0;
            handTimer = L.hands != null ? rng.Range(L.hands.interval) * .6 : double.PositiveInfinity;
            nextCollapse = 3; lastBig = -99;   // 开局给 3 秒喘口气
            eye = new EyeState { dur = L.eye != null ? rng.Range(L.eye.cool) * .7 : double.PositiveInfinity, pos = new V3(0, -Cfg.Eye.Depth, 0) };
            cover = false; palm = new V3?[2]; prevPalm = new V3?[2]; fingertip = new V3?[2]; pinching = new bool[2]; speed = new double[2]; vel = new V3[2]; handAge = new double[2];
            tipsShown.Clear(); seq = 0; grabbed = null; result = null; pull = 0;
            scares = new ScareDirector(this);
            Emit(new GameEvent("layerStart") { idx = idx });
            if (tut == null) Tip("start");
        }

        public void Tip(string key) { if (tut == null && tipsShown.Add(key)) Emit(new GameEvent("tip") { kind = key }); }

        public void Update(double dt, in PlayerInput input)
        {
            if (state != "play" && state != "falling") return;
            dt = Math.Min(dt, .1);
            if (state == "falling") { UpdateFall(dt, input); return; }
            ReadHands(dt, input);
            UpdateFeet(input);
            // 走出边界：整个游戏停住，等玩家回来
            bool outside = !floor.Inside(feet.x, feet.z, Cfg.Feet.OobMargin);
            if (outside != oob) { oob = outside; Emit(new GameEvent(outside ? "oob" : "inbounds")); }
            if (oob) return;

            time += dt;
            if (time >= 6) Tip("t6");
            if (time >= 16) Tip("t16");
            invuln = Math.Max(0, invuln - dt);
            quiet = Math.Max(0, quiet - dt); floor.hold = quiet > 0;
            if (tut != null) tut.Update(dt, input); else Director(dt);
            floor.Update(dt, standing);
            Flush();
            UpdateSupport(dt);
            if (state != "play") return;
            UpdateStay(dt);
            UpdateShards(dt, input);
            UpdateDoor(dt, input);
            UpdateHands(dt);
            UpdateEye(dt, input);
            scares.Update(dt, input);
        }

        void ReadHands(double dt, in PlayerInput input)
        {
            bool cov = false;
            for (int side = 0; side < 2; side++)
            {
                var h = input.Hand(side);
                if (!h.tracked) { palm[side] = null; fingertip[side] = null; speed[side] = 0; pinching[side] = false; handAge[side] = 0; vel[side] = default; continue; }
                handAge[side] += dt;
                pinching[side] = h.pinch;
                fingertip[side] = h.hasTip ? h.tip : null;
                prevPalm[side] = palm[side];
                double sp = palm[side].HasValue ? V3.Dist(palm[side].Value, h.palm) / Math.Max(dt, 1e-3) : 0;
                speed[side] = speed[side] * .35 + sp * .65;
                if (palm[side].HasValue)
                {
                    double inv = .6 / Math.Max(dt, 1e-3); var pv = palm[side].Value; var ov = vel[side];
                    vel[side] = new V3(ov.x * .4 + (h.palm.x - pv.x) * inv, ov.y * .4 + (h.palm.y - pv.y) * inv, ov.z * .4 + (h.palm.z - pv.z) * inv);
                }
                palm[side] = h.palm;
                // 捂眼：手掌贴在脸前
                var d = h.palm - input.headPos;
                if (V3.Len(d) < Cfg.Eye.CoverDist && V3.Dot(d, input.headFwd) > .02) cov = true;
            }
            if (cov != cover) { cover = cov; Emit(new GameEvent(cov ? "coverOn" : "coverOff")); }
        }

        /// <summary>脚的位置：头的水平投影，低头/前倾时往后收一点</summary>
        void UpdateFeet(in PlayerInput input)
        {
            var f = input.headFwd; double l = V3.Hypot(f.x, f.z); if (l == 0) l = 1;
            double down = Math.Max(0, -f.y), back = V3.Clamp(down * 1.2, 0, 1) * Cfg.Feet.LeanBack;
            feet = (input.headPos.x - f.x / l * back, input.headPos.z - f.z / l * back);
        }

        void UpdateSupport(double dt)
        {
            var t = floor.SupportAt(feet.x, feet.z);
            standing = t;
            if (t != null) { unsupported = 0; return; }
            unsupported += dt;
            // 地板越大，跨一格洞要的时间越长，宽限也跟着放宽
            double grace = Cfg.Feet.FallGrace * Math.Max(1, floor.s / .75);
            if (unsupported >= grace && invuln <= 0 && !god) StartFall();
        }

        void UpdateStay(double dt)
        {
            if (!L.chase) return;
            var t = standing;
            if (t == null || t != stay.tile) { stay = new StayState { tile = t }; return; }
            if (t.state != TileState.Solid || t.door) return;
            stay.t += dt;
            if (!stay.warned && stay.t >= Cfg.StayLimit - 1.3) { stay.warned = true; Emit(new GameEvent("stayWarn") { tile = t }); Tip("stay"); }
            if (stay.t >= Cfg.StayLimit)
            {
                if (floor.Crack(t, new CrackOpts { cause = "stay", player = t, keepMin = false })) stay = new StayState { tile = t, t = -99, warned = true };
                else stay.t = Cfg.StayLimit - 1.3;
            }
        }

        // ───────── 塌陷导演 ─────────
        void Director(double dt)
        {
            nextCollapse -= dt;
            if (nextCollapse > 0) return;
            string kind = rng.Weighted(L.patterns);
            bool big = kind == "sweep" || kind == "ring" || kind == "checker";
            if (big && time - lastBig < 12) kind = "random";
            // 大塌陷只在场上几乎完好时出现，不然叠在一起没路可走
            if (big && floor.CountSafe() < floor.tiles.Count * .75) kind = "random";
            // 5×5 的地图更大：每次多塌几块，压力不被稀释
            int extra = floor.n >= 5 ? 2 : 0;
            int n = floor.Pattern(kind, standing, new[] { L.randomCount[0] + extra, L.randomCount[1] + extra });
            bool isBig = kind == "sweep" || kind == "ring" || kind == "checker";
            if (isBig && n > 0) { lastBig = time; Emit(new GameEvent("pattern") { kind = kind }); Tip(kind); }
            double k = 1 / (1 + L.heat * time);
            double next = Math.Max(L.minInterval, rng.Range(L.interval) * k) + assist * .25;
            if (isBig) next += kind == "sweep" ? floor.n * 1.05 + 1.5 : 5.5;
            nextCollapse = next;
        }

        // ───────── 坠落 ─────────
#if UNITY_EDITOR
        public void DebugFall() => StartFall();
#endif
        void StartFall()
        {
            state = "falling"; seq = 0; lives--; falls++; grabbed = null; pull = 0;
            foreach (var h in hands) if (h.phase != "recoil") SetHandPhase(h, "recoil");
            if (eye.phase == "open" || eye.phase == "warn") SetEye("closing", Cfg.Eye.Closing);
            eye.seen = 0;
            Emit(new GameEvent("fall_player") { lives = lives });
        }

        void UpdateFall(double dt, in PlayerInput input)
        {
            seq += dt;
            UpdateFeet(input);
            // 坠落时手缩回去
            foreach (var h in hands) { h.t += dt; if (h.phase == "recoil" && h.t >= Cfg.Hand.Recoil) h.dead = true; }
            hands = hands.Where(h => !h.dead).ToList();
            if (seq < fallSeq) return;
            if (lives <= 0) { Lose(); return; }
            // 醒来：梦把你接住——脚下和身边各补一块
            var f = floor;
            double fx = V3.Clamp(feet.x, -f.half + .01, f.half - .01), fz = V3.Clamp(feet.z, -f.half + .01, f.half - .01);
            var t = f.TileAt(fx, fz);
            f.Restore(t);
            if (!f.HasEscape(t))
            {
                var nb = rng.Shuffle(f.Neighbors(t, 1));
                if (nb.Count > 0) f.Restore(nb[0]);
            }
            // 睁眼的时候不能还在塌：开裂的全部接住，排队的取消，安静到眼睛睁开之后
            foreach (var c in f.tiles) if (c.state == TileState.Crack) f.Restore(c);
            f.queue.Clear(); quiet = Cfg.Fall.Wake; f.hold = true; stay = new StayState();
            handTimer = Math.Max(handTimer, Cfg.Fall.Wake + 1);
            Flush();
            invuln = Cfg.Fall.Invuln; unsupported = 0; state = "play";
            nextCollapse = Math.Max(nextCollapse, Cfg.Fall.Wake + .8);
            Emit(new GameEvent("wake") { lives = lives });
        }

        void Lose()
        {
            state = "lost";
            fails.TryGetValue(layerIdx, out int c); fails[layerIdx] = c + 1;
            Emit(new GameEvent("lost") { idx = layerIdx });
        }

        void Clear()
        {
            state = "clear";
            int stars = 1;
            if (falls == 0) stars++;
            if (time <= L.par) stars++;
            result = new ResultInfo { stars = stars, time = time, falls = falls, par = L.par };
            fails[layerIdx] = 0;
            Emit(new GameEvent("clear") { idx = layerIdx, stars = stars });
        }

        // ───────── 碎片 ─────────
        void UpdateShards(double dt, in PlayerInput input)
        {
            int need = L.shards - collected - shards.Count;
            if (tut == null && door == null && need > 0 && shards.Count < L.shardActive)
            {
                shardDelay -= dt;
                if (shardDelay <= 0) { SpawnShard(); shardDelay = rng.Range(L.shardGap); }
            }
            foreach (var s in shards)
            {
                s.life -= dt;
                if (s.life <= 0) { s.dead = true; Emit(new GameEvent("shardFade") { shard = s }); continue; }
                double a = input.hasGaze ? V3.AngleTo(input.gazeOrigin, V3.Norm(input.gazeDir), s.pos) : 9;
                if (a < Cfg.Shard.GazeCone && !cover) s.gazeT = Math.Min(Cfg.Shard.GazeDwell + .3, s.gazeT + dt);
                else s.gazeT = Math.Max(0, s.gazeT - dt * 2);
                bool locked = s.gazeT >= Cfg.Shard.GazeDwell;
                if (locked && !s.locked) Emit(new GameEvent("shardLock") { shard = s });
                s.locked = locked;
                // 眼睛锁定 + 手捏合 = 隔空抓取。捏住后眼睛就自由了，可以去看别处
                if (s.pulled < 0 && locked)
                    for (int side = 0; side < 2; side++)
                    {
                        int sd = side;
                        if (pinching[side] && palm[side].HasValue && !shards.Any(o => o.pulled == sd)) { s.pulled = side; break; }
                    }
                if (s.pulled >= 0 && (!pinching[s.pulled] || !palm[s.pulled].HasValue)) s.pulled = -1;
                if (s.pulled >= 0)
                {
                    var d = palm[s.pulled].Value - s.pos; double l = V3.Len(d);
                    if (l > 1e-3)
                    {
                        double k = Math.Min(l, Cfg.Shard.PullSpeed * dt) / l;
                        s.pos.x += d.x * k; s.pos.y += d.y * k; s.pos.z += d.z * k;
                    }
                    s.life = Math.Max(s.life, Cfg.Shard.Flicker + 1);   // 抓在手里的碎片不会消散
                }
                for (int side = 0; side < 2; side++)
                {
                    bool nearPalm = palm[side].HasValue && V3.Dist(palm[side].Value, s.pos) < Cfg.Shard.Touch;
                    bool nearTip = fingertip[side].HasValue && V3.Dist(fingertip[side].Value, s.pos) < Cfg.Shard.Touch * .8;
                    if (nearPalm || nearTip)
                    {
                        s.dead = true; collected++;
                        Emit(new GameEvent("shard") { shard = s, side = side, collected = collected, goal = L.shards });
                        break;
                    }
                }
            }
            shards = shards.Where(s => !s.dead).ToList();
            if (door == null && collected >= L.shards && (tut == null || tut.DoorOK)) SpawnDoor();
        }

        // ───────── 教学用 ─────────
        static (double x, double z) Flat(V3 f) { double l = V3.Hypot(f.x, f.z); return l < .01 ? (0, 1) : (f.x / l, f.z / l); }

        public void SpawnShardAhead(in PlayerInput input, double dist, double y)
        {
            var (fx, fz) = Flat(input.headFwd); double lim = floor.half - .2;
            var sh = new Shard { id = ++shardId, pos = new V3(V3.Clamp(feet.x + fx * dist, -lim, lim), y, V3.Clamp(feet.z + fz * dist, -lim, lim)), life = 1e6 };
            shards.Add(sh); Emit(new GameEvent("shardSpawn") { shard = sh });
        }
        public void ClearShards() { foreach (var s in shards) Emit(new GameEvent("shardFade") { shard = s }); shards.Clear(); }

        /// <summary>挑一块玩家没站着、但够得着的砖塌掉</summary>
        public void OpenHoleNearby()
        {
            Tile best = null; double bd = 1e9;
            foreach (var t in floor.tiles)
            {
                if (t == standing || t.door || t.state != TileState.Solid) continue;
                double d = V3.Hypot(t.x - feet.x, t.z - feet.z); if (d < bd) { bd = d; best = t; }
            }
            if (best == null) return;
            floor.Collapse(best); best.holeDur = 1e5; Flush();
        }
        public bool SpawnTutorialHand() => SpawnHand(true);
        public void RecallHands() { foreach (var h in hands) if (h.phase != "recoil") SetHandPhase(h, "recoil"); grabbed = null; pull = 0; }
        public void OpenEyeNow() => SetEye("warn", Cfg.Eye.Warn);

        void SpawnShard()
        {
            var f = floor; var fp = new V3(feet.x, 0, feet.z);
            bool overHole = rng.Chance(L.shardOverHole);
            var cands = f.tiles.Where(t => !t.door && Safe(t.x, t.z) && V3.DistXZ(t.Pos, fp) >= .6 && !shards.Any(s => V3.DistXZ(s.pos, t.Pos) < .4)).ToList();
            if (overHole)
            {
                var holes = cands.Where(t => t.state == TileState.Hole || t.state == TileState.Fall || t.state == TileState.Crack).ToList();
                if (holes.Count > 0) cands = holes;
            }
            if (cands.Count == 0) cands = f.tiles.Where(t => !t.door).ToList();
            // 偏远的位置权重更大：逼你走动
            var w = new List<(string, double)>();
            for (int i = 0; i < cands.Count; i++) w.Add((i.ToString(), .3 + V3.DistXZ(cands[i].Pos, fp)));
            var tile = cands[int.Parse(rng.Weighted(w))];
            double px = tile.x + rng.Range(-.1, .1), py = rng.Range(L.shardY), pz = tile.z + rng.Range(-.1, .1);
            var sh = new Shard { id = ++shardId, pos = new V3(px, py, pz), life = Cfg.Shard.Life, overHole = !Floor.Supportive(tile) };
            shards.Add(sh);
            Emit(new GameEvent("shardSpawn") { shard = sh });
        }

        bool Safe(double x, double z) => safeAt == null || safeAt(x, z);

        void SpawnDoor()
        {
            // 裂隙不放在最外圈，也不放到系统安全边界外面：玩家要能走到跟前伸手拉
            var f = floor; Tile best = null; double bd = -1; var fp = new V3(feet.x, 0, feet.z);
            for (int pass = 0; pass < 2 && best == null; pass++)
                foreach (var t in f.tiles)
                {
                    if (f.IsOuter(t) || t == standing || (pass == 0 && !Safe(t.x, t.z))) continue;
                    double d = V3.DistXZ(t.Pos, fp); if (d > bd) { bd = d; best = t; }
                }
            best ??= f.tiles[f.tiles.Count / 2];
            best.door = true;
            f.queue = f.queue.Where(q => q.tile != best).ToList();
            if (best.state != TileState.Solid) f.Restore(best);
            Flush();
            door = new DoorState { tile = best };
            Emit(new GameEvent("doorSpawn") { tile = best });
            Tip("door");
        }

        /// <summary>
        /// 门：先是一道细缝，玩家走到跟前，双手捏住往两边拉，裂缝跟着手张开，拉够了才能进。
        /// 手一直检测不到（比如硬件不支持）时自动慢慢打开，绝不卡关
        /// </summary>
        void UpdateDoor(double dt, in PlayerInput input)
        {
            var d = door;
            if (d == null) return;
            d.t += dt;
            if (d.t < Cfg.Door.Rise) d.open = Cfg.Door.Crack * d.t / Cfg.Door.Rise;
            else if (!d.ready)
            {
                var c = new V3(d.tile.x, 1.2, d.tile.z);
                bool both = palm[0].HasValue && palm[1].HasValue;
                bool near = both && V3.DistXZ(palm[0].Value, c) < Cfg.Door.Reach && V3.DistXZ(palm[1].Value, c) < Cfg.Door.Reach;
                bool pull = near && pinching[0] && pinching[1];
                if (pull)
                {
                    double sep = V3.Dist(palm[0].Value, palm[1].Value);
                    if (!d.pulling) { d.pulling = true; d.pull0 = sep; d.open0 = d.open; Emit(new GameEvent("doorPull")); }
                    d.open = Math.Max(d.open, d.open0 + (sep - d.pull0) / Cfg.Door.PullSpan);
                }
                else if (d.pulling) { d.pulling = false; Emit(new GameEvent("doorRelease")); }
                d.noHands = palm[0].HasValue || palm[1].HasValue ? 0 : d.noHands + dt;
                if (d.noHands >= Cfg.Door.AutoAfter || d.t > Cfg.Door.GiveUp) d.open += Cfg.Door.AutoSpeed * dt;      // 手一直检测不到，或者 30 秒都没拉开：自己慢慢裂开，绝不卡关
                if (d.open >= Cfg.Door.ReadyAt) { d.open = 1; d.ready = true; d.pulling = false; Emit(new GameEvent("doorReady") { tile = d.tile }); }
            }
            if (d.ready && standing == d.tile)
            {
                d.enterT += dt;
                if (d.enterT >= Cfg.Door.Enter) Clear();
            }
            else d.enterT = 0;
        }

        // ───────── 深渊之手 ─────────
        void UpdateHands(double dt)
        {
            if (L.hands != null && handsOn)
            {
                handTimer -= dt;
                if (handTimer <= 0)
                {
                    int live = hands.Count(h => h.phase != "recoil");
                    if (live < L.hands.max && SpawnHand()) handTimer = rng.Range(L.hands.interval) * (1 + assist * .15);
                    else handTimer = 1;
                }
            }
            foreach (var h in hands)
            {
                h.t += dt;
                h.cool = Math.Max(0, h.cool - dt);
                var src = h.src;
                // 洞口被升起的地砖顶住：手被挤回去
                if (src != null && h.phase != "recoil" && (src.state != TileState.Hole || src.holeDur - src.t < 1.6)) { SetHandPhase(h, "recoil"); continue; }   // 砖升回来之前先缩回去
                if (h.phase == "omen") { if (h.t >= h.omenDur) SetHandPhase(h, "rise"); }
                else if (h.phase == "rise")
                {
                    double k = Math.Min(1, h.t / (h.riseDur != 0 ? h.riseDur : Cfg.Hand.Rise));
                    h.pos.y = -.7 + (Cfg.Hand.ReachY + .7) * (1 - (1 - k) * (1 - k));
                    if (k >= 1) SetHandPhase(h, "reach");
                }
                else if (h.phase == "reach")
                {
                    h.pos.y = Cfg.Hand.ReachY + Math.Sin(h.t * 3.1 + h.id) * .04;
                    if (h.t >= Cfg.Hand.Aim)
                    {   // 先停住张开手指“瞄”你一下，再扑过来
                        double dx = feet.x - h.pos.x, dz = feet.z - h.pos.z, l = V3.Hypot(dx, dz);
                        if (l > 1e-3) { double m = Math.Min(l, Cfg.Hand.ReachSpeed * dt) / l; h.pos.x += dx * m; h.pos.z += dz * m; }
                        if (V3.Hypot(h.pos.x - feet.x, h.pos.z - feet.z) < Cfg.Hand.GrabDist && invuln <= 0)
                        {
                            SetHandPhase(h, "grab"); grabbed = h; Emit(new GameEvent("grab") { hand = h });
                        }
                        else if (h.t > Cfg.Hand.ReachMax || V3.DistXZ(h.pos, h.anchor) > 1.5) SetHandPhase(h, "recoil");
                    }
                }
                else if (h.phase == "grab")
                {
                    h.pos.x += (feet.x - h.pos.x) * Math.Min(1, dt * 6);
                    h.pos.z += (feet.z - h.pos.z) * Math.Min(1, dt * 6);
                    h.pos.y = Cfg.Hand.ReachY - .1;
                    // 抓着你往下拖：视线慢慢沉下去，拖够了就掉下去。一巴掌拍开才能脱身
                    pull += dt / Cfg.Hand.PullTime;
                    if (pull >= 1 && invuln <= 0 && !god)
                    {
                        floor.Collapse(standing); Flush();
                        pull = 0; StartFall(); return;
                    }
                }
                else if (h.phase == "recoil")
                {
                    h.pos.y -= dt * 2.2;
                    if (h.t >= Cfg.Hand.Recoil) h.dead = true;
                }
                // 拍：手速够快、碰到它。不只看梢部那一个点：从洞口到梢部整根身体都能拍；
                // 手这一帧扫过的路径也算，挥得快时不会从两帧之间漏过去
                if (h.phase != "recoil" && h.pos.y > .05 && h.cool <= 0)
                    for (int side = 0; side < 2; side++)
                    {
                        if (!palm[side].HasValue || speed[side] < Cfg.Hand.SwatSpeed) continue;
                        if (SwatHits(prevPalm[side] ?? palm[side].Value, palm[side].Value, h))
                        {
                            h.hp--; h.cool = .35;
                            Emit(new GameEvent("swat") { hand = h, side = side, killed = h.hp <= 0 });
                            if (h.hp <= 0) SetHandPhase(h, "recoil");
                            else
                            {   // 被拍退一点
                                double dx = h.anchor.x - h.pos.x, dz = h.anchor.z - h.pos.z, l = V3.Hypot(dx, dz); if (l == 0) l = 1;
                                h.pos.x += dx / l * .18; h.pos.z += dz / l * .18;
                                if (h.phase == "grab") SetHandPhase(h, "reach");
                                h.t = 0;
                            }
                            break;
                        }
                    }
            }
            if (grabbed != null && (grabbed.dead || grabbed.phase != "grab")) { grabbed = null; Emit(new GameEvent("release")); }
            if (grabbed == null) pull = Math.Max(0, pull - dt / Cfg.Hand.PullDecay);
            hands = hands.Where(h => !h.dead).ToList();
        }

        /// <summary>
        /// 手这一帧从 a 扫到 b，有没有碰到触手：触手当成从洞口 anchor 到梢部 pos 的一根粗管（只取地面以上那段），
        /// 手的路径和它的最短距离小于 SwatDist 就算。触手只到膝盖，手在它上方到腰的高度挥过去也算，不逼玩家弯腰
        /// </summary>
        static bool SwatHits(V3 a, V3 b, AbyssHand h)
        {
            var s0 = new V3(h.anchor.x, Math.Max(h.anchor.y, .05), h.anchor.z); var s1 = h.pos;
            for (int k = 0; k <= 8; k++)
            {
                double u = k / 8.0; var p = new V3(a.x + (b.x - a.x) * u, a.y + (b.y - a.y) * u, a.z + (b.z - a.z) * u);
                // 点到线段的最近点
                var d = new V3(s1.x - s0.x, s1.y - s0.y, s1.z - s0.z); double dd = d.x * d.x + d.y * d.y + d.z * d.z;
                double t = dd < 1e-6 ? 1 : Math.Max(0, Math.Min(1, ((p.x - s0.x) * d.x + (p.y - s0.y) * d.y + (p.z - s0.z) * d.z) / dd));
                var c = new V3(s0.x + d.x * t, s0.y + d.y * t, s0.z + d.z * t);
                if (p.y > 1.05) continue;      // 高过腰就不算：胸前做别的动作不能误拍到脚边的触手
                double up = p.y - c.y, dy = up > 0 ? Math.Max(0, up - .5) : -up;
                if (V3.Hypot(p.x - c.x, p.z - c.z) < Cfg.Hand.SwatDist && dy < Cfg.Hand.SwatDist) return true;
            }
            return false;
        }

        void SetHandPhase(AbyssHand h, string phase) { h.phase = phase; h.t = 0; Emit(new GameEvent("handPhase") { hand = h, phase = phase }); }

        bool SpawnHand(bool fast = false)
        {
            if (!handsOn) return false;      // 所有入口都在这里拦住
            var f = floor; var cands = new List<(Tile src, V3 anchor)>();
            foreach (var t in f.tiles)
            {
                if (t.state != TileState.Hole || t.t < .3 || t.door || t.holeDur - t.t < 3) continue;   // 只从破洞出来；洞要撑得够久
                if (hands.Any(h => h.src == t)) continue;
                double px = V3.Clamp(feet.x, t.x - f.s / 2, t.x + f.s / 2), pz = V3.Clamp(feet.z, t.z - f.s / 2, t.z + f.s / 2);
                double d = V3.Hypot(px - feet.x, pz - feet.z);
                if (d < .15 || d > Cfg.Hand.SpawnRange) continue;
                cands.Add((t, new V3(px, 0, pz)));
            }
            if (cands.Count == 0) return false;
            var c = rng.Pick(cands);
            var h2 = new AbyssHand
            {
                id = ++handId, src = c.src, anchor = c.anchor, pos = new V3(c.anchor.x, -.7, c.anchor.z), phase = "omen",
                omenDur = fast ? 1 : Cfg.Hand.Omen, hp = L.hands?.hp ?? 1, riseDur = fast ? 1 : Cfg.Hand.Rise,
            };
            hands.Add(h2);
            Emit(new GameEvent("handSpawn") { hand = h2 });
            Tip("hand");
            return true;
        }

        // ───────── 眼睛 ─────────
        void SetEye(string phase, double dur) { if (phase == "open") spottedOnce = false; eye.phase = phase; eye.t = 0; eye.dur = dur; Emit(new GameEvent("eye") { phase = phase }); }

        void UpdateEye(double dt, in PlayerInput input)
        {
            if (L.eye == null) return;
            eye.t += dt;
            eye.pos.x = Math.Sin(time * .13) * .8;
            eye.pos.z = Math.Cos(time * .11) * .8;
            if (eye.phase == "closed" && eye.t >= eye.dur) { SetEye("warn", Cfg.Eye.Warn); Tip("eye"); }
            else if (eye.phase == "warn" && eye.t >= eye.dur) SetEye("open", rng.Range(L.eye.open) * Cfg.Eye.OpenK);
            else if (eye.phase == "open" && eye.t >= eye.dur) SetEye("closing", Cfg.Eye.Closing);
            else if (eye.phase == "closing" && eye.t >= eye.dur) SetEye("closed", rng.Range(L.eye.cool) * (1 + assist * .1));
            double target = eye.phase == "open" ? 1 : eye.phase == "warn" ? .12 : 0;
            eye.open += (target - eye.open) * Math.Min(1, dt * (eye.phase == "open" ? 6 : 4));

            bool seeing = eye.phase == "open" && LookingIntoEye(input);
            if (seeing != eye.seeing) { eye.seeing = seeing; Emit(new GameEvent(seeing ? "seenStart" : "seenStop")); }
            spottedCd = Math.Max(0, spottedCd - dt);
            // 低头、朝破洞挥一掌或往下一拍都能拍走它。往上抬手不算：
            // 速度只数水平和向下的部分；手从腰间进入视野时追踪常常刚重新接上，所以只等很短一下
            if (eye.phase == "open" && input.headFwd.y < -.2)
                for (int side = 0; side < 2; side++)
                {
                    if (!palm[side].HasValue || pinching[side] || handAge[side] < Cfg.Eye.SwatReady) continue;
                    var v = vel[side]; double flat = V3.Hypot(v.x, v.z), drive = flat + Math.Max(0, -v.y);
                    if (drive < Cfg.Eye.SwatSpeed || v.y > .35 * flat || palm[side].Value.y > 1.4) continue;
                    Emit(new GameEvent("eyeSwat") { pos = palm[side].Value }); eye.seen = 0;
                    SetEye("closing", Cfg.Eye.SwatClose); break;
                }
            if (seeing && invuln <= 0 && spottedCd <= 0) eye.seen += dt / Cfg.Eye.SeenFill;
            else eye.seen = Math.Max(0, eye.seen - dt / Cfg.Eye.SeenDecay);
            if (eye.seen >= 1) Spotted();
        }

        /// <summary>视线是否穿过地上的破洞：眼睛大得填满整个深渊，从任何一个洞往下看都会对上它</summary>
        public bool LookingIntoEye(in PlayerInput input)
        {
            if (!input.hasGaze || cover) return false;
            var o = input.gazeOrigin; var d = V3.Norm(input.gazeDir);
            if (d.y > -.05) return false;
            double t = -o.y / d.y, px = o.x + d.x * t, pz = o.z + d.z * t;
            if (!floor.Inside(px, pz)) return false;      // 看的是区域外的真实地面
            var tile = floor.TileAt(px, pz);
            return !(tile != null && (tile.state == TileState.Solid || tile.state == TileState.Crack || tile.state == TileState.Rise));
        }

        void Spotted()
        {
            eye.seen = 0; spottedCd = Cfg.Eye.SpotCool;
            // 一次睁眼只罚一次：之后它继续盯着你、继续压迫，但不会反复把地板震裂，不然太不公平
            if (spottedOnce) return;
            spottedOnce = true; eye.spotted++;
            Emit(new GameEvent("spotted"));
            var p = standing;
            if (p != null)
            {
                var tiles = new List<Tile> { p };
                if (Cfg.Eye.BurstRadius > 0) tiles.AddRange(floor.Neighbors(p, Cfg.Eye.BurstRadius));
                foreach (var t in tiles) floor.Crack(t, new CrackOpts { dur = Cfg.FastCrack, cause = "eye", force = true });
            }
            if (L.hands != null) { SpawnHand(true); SpawnHand(true); }
            // 被看见之后它不走：继续盯着你，隔一会儿才会再次发作
            eye.dur += Cfg.Eye.StayAfter;
        }
    }
}
