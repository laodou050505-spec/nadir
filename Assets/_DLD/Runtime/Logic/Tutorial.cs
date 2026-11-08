namespace DLD.Logic
{
    /// <summary>
    /// 玩法介绍：把游戏的几件事排成一条线，一次只教一件，做到了才往下走。全程不会掉下去。
    /// 每一步都有时限，手或眼检测不好时也不会卡死
    /// </summary>
    public class Tutorial
    {
        readonly Game g; int step; double t; Tile crack; bool spawned; Effigy fig;
        public bool DoorOK { get; private set; }
        /// <summary>地上要标出来的：红圈的危险砖、白圈的安全砖；箭头指的目标</summary>
        public Tile Danger { get; private set; } public Tile Safe { get; private set; } public V3? Target { get; private set; }
        public Tutorial(Game game) { g = game; }

        void Next(string tip) { step++; t = 0; spawned = false; g.Emit(new GameEvent("tip") { kind = "tut:" + tip }); }

        void Say(string tip, int to) { step = to == 1 ? 7 : to; t = 0; g.Emit(new GameEvent("tip") { kind = "tut:" + tip }); }

        public void Update(double dt, in PlayerInput input)
        {
            t += dt;
            g.floor.hold = true;       // 平时不塌，只有教学自己动地板
            switch (step)
            {
                case 0: if (t > 1.2) Say("goal1", 1); break;
                case 7: if (t > 3.6) Say("goal2", 8); break;
                case 8: if (t > 3.6) Say("h_wrist", 9); break;
                case 9: if (t > 4.2) { step = 0; Next("h_step"); } break;
                case 1:        // 脚下开裂，走开
                    if (!spawned)
                    {
                        crack = g.standing ?? g.floor.TileAt(g.feet.x, g.feet.z); spawned = true;
                        g.floor.hold = false; g.floor.Crack(crack, new CrackOpts { force = true, dur = 6, holeDur = 1e5, cause = "stay" }); g.floor.hold = true;
                        Danger = crack;
                        // 白框放在玩家正前方那块砖上：一眼就看得到，不用转身找
                        double bestD = -9; double hl = System.Math.Max(V3.Hypot(input.headFwd.x, input.headFwd.z), .01);
                        foreach (var n in g.floor.Neighbors(crack, 1))
                        {
                            if (n.state != TileState.Solid) continue;
                            double dx = n.x - crack.x, dz = n.z - crack.z, dl = System.Math.Max(V3.Hypot(dx, dz), .01), dot = (dx * input.headFwd.x + dz * input.headFwd.z) / (dl * hl) - (g.floor.IsOuter(n) ? .4 : 0);
                            if (dot > bestD) { bestD = dot; Safe = n; }
                        }
                        if (Safe != null) Target = new V3(Safe.x, .25, Safe.z);
                    }
                    if ((g.standing != crack && t > 1.5) || t > 14) { Danger = null; Safe = null; Target = null; Next("h_gaze"); }
                    break;
                case 2:        // 盯住碎片捏合，或者直接碰到
                    if (!spawned) { spawned = true; g.SpawnShardAhead(input, 1.0, 1.2); }
                    Target = g.shards.Count > 0 ? g.shards[0].pos : null;
                    if (g.collected >= 1 || t > 45) { Target = null; g.collected = 1; g.ClearShards(); if (g.handsOn) Next("h_swat"); else { step = 3; Next("h_push"); } }
                    break;
                case 3:        // 拍开触手：先在手边开一个洞
                    if (!spawned)
                    {
                        if (t > 1.2) spawned = g.SpawnTutorialHand();
                        else if (t < dt * 1.5) g.OpenHoleNearby();
                    }
                    else if (g.hands.Count == 0 || t > 40) { Target = null; g.RecallHands(); Next("h_push"); }
                    else Target = g.hands[0].pos;
                    break;
                case 4:        // 施法击退黑影
                    if (!spawned) { spawned = true; fig = g.scares.PlaceAhead(input, 3.1); }
                    if (fig != null) Target = new V3(fig.x, 2.5, fig.z);
                    if ((fig != null && fig.pushing) || t > 25) { Target = null; Next("h_eye"); }
                    break;
                case 5:        // 巨眼睁开时别看破洞
                    if (!spawned) { spawned = true; g.OpenEyeNow(); }
                    if ((g.eye.phase == "closed" && t > 3) || t > 18) { DoorOK = true; Next("h_tear"); }
                    break;
                case 6:        // 走到裂隙前，撕开，走进去
                    if (g.door != null) Target = new V3(g.door.tile.x, 2.55, g.door.tile.z);
                    if (g.door != null && g.door.ready && !spawned) { spawned = true; g.Emit(new GameEvent("tip") { kind = "tut:h_enter" }); }
                    break;
            }
        }
    }
}
