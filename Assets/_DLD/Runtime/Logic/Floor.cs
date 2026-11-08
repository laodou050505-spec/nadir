using System;
using System.Collections.Generic;
using System.Linq;

namespace DLD.Logic
{
    public enum TileState { Solid, Crack, Fall, Hole, Rise }

    public class Tile
    {
        public int i, j, k; public double x, z;
        public TileState state = TileState.Solid;
        public double t, dur, holeDur, size; public bool door; public string cause;
        public V3 Pos => new V3(x, 0, z);
    }

    public class CrackOpts { public double? dur, holeDur; public string cause = "random"; public Tile player; public bool force, keepMin = true; }

    /// <summary>地板：N×N 块地砖的生命周期、各种塌陷方式和公平性检查</summary>
    public class Floor
    {
        public readonly int n; public readonly double s, half; public readonly Rng rng;
        public double crackTime = Cfg.CrackTime, time;
        public bool hold;        // 刚醒来和教学时：什么都不会开裂
        public readonly List<Tile> tiles = new();
        public List<(double at, Tile tile, CrackOpts opts)> queue = new();
        public readonly List<GameEvent> events = new();

        public Floor(int n, Rng rng, double tile)
        {
            this.n = n; s = tile; half = n * s / 2; this.rng = rng;
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
                    tiles.Add(new Tile { size = tile, i = i, j = j, k = j * n + i, x = (i - (n - 1) / 2.0) * s, z = (j - (n - 1) / 2.0) * s });
        }

        public Tile At(int i, int j) => i < 0 || j < 0 || i >= n || j >= n ? null : tiles[j * n + i];
        public Tile TileAt(double x, double z) => At((int)Math.Floor((x + half) / s), (int)Math.Floor((z + half) / s));
        public bool Inside(double x, double z, double margin = 0) => Math.Abs(x) <= half + margin && Math.Abs(z) <= half + margin;
        public static bool Supportive(Tile t) => t != null && (t.state == TileState.Solid || t.state == TileState.Crack);
        public static bool Stable(Tile t) => t != null && (t.state == TileState.Solid || t.state == TileState.Rise);
        public List<Tile> Neighbors(Tile t, int r = 1)
        {
            var o = new List<Tile>();
            for (int dj = -r; dj <= r; dj++) for (int di = -r; di <= r; di++)
                {
                    if (di == 0 && dj == 0) continue;
                    var q = At(t.i + di, t.j + dj); if (q != null) o.Add(q);
                }
            return o;
        }
        public int CountSafe() { int c = 0; foreach (var t in tiles) if (t.state == TileState.Solid || t.state == TileState.Rise) c++; return c; }

        /// <summary>脚下（含洞边容差）有没有能站的砖</summary>
        public Tile SupportAt(double x, double z)
        {
            var t = TileAt(x, z);
            if (Supportive(t)) return t;
            Tile best = null; double bd = double.PositiveInfinity;
            foreach (var o in tiles)
            {
                if (!Supportive(o)) continue;
                double dx = Math.Max(0, Math.Abs(x - o.x) - s / 2), dz = Math.Max(0, Math.Abs(z - o.z) - s / 2), d = V3.Hypot(dx, dz);
                if (d <= Cfg.Feet.EdgeTol && d < bd) { bd = d; best = o; }
            }
            return best;
        }

        /// <summary>玩家站在 p 上时还有没有退路：一步之内（含跨过一格洞的直线两步）有完好或正在升起的砖</summary>
        public bool HasEscape(Tile p, Tile exclude = null)
        {
            if (p == null) return true;
            bool Ok(Tile t) => t != null && t != exclude && t != p && Stable(t);
            foreach (var t in Neighbors(p, 1)) if (Ok(t)) return true;
            foreach (var (di, dj) in new[] { (2, 0), (-2, 0), (0, 2), (0, -2) })
            {
                var mid = At(p.i + di / 2, p.j + dj / 2); var far = At(p.i + di, p.j + dj);
                if (mid != null && !Supportive(mid) && Ok(far)) return true;
            }
            return false;
        }

        public bool Adjacent(Tile a, Tile b) => Math.Max(Math.Abs(a.i - b.i), Math.Abs(a.j - b.j)) <= 2;

        /// <summary>让一块砖开裂，返回是否成功。player 用于公平检查</summary>
        public bool Crack(Tile t, CrackOpts o)
        {
            if (hold || t == null || t.door || t.state != TileState.Solid) return false;
            if (!o.force)
            {
                if (o.keepMin && CountSafe() - 1 < Math.Ceiling(tiles.Count * Cfg.MinSolidRatio)) return false;
                var player = o.player;
                if (player != null && (t == player ? !HasEscape(player, t) : !HasEscape(player, t) && Adjacent(t, player))) return false;
            }
            t.state = TileState.Crack; t.t = 0; t.dur = o.dur ?? crackTime; t.holeDur = o.holeDur ?? rng.Range(Cfg.HoleTime); t.cause = o.cause;
            events.Add(new GameEvent("crack") { tile = t, cause = o.cause });
            return true;
        }

        public void Schedule(Tile t, double delay, CrackOpts o) => queue.Add((time + delay, t, o));

        public void Update(double dt, Tile player)
        {
            time += dt;
            if (queue.Count > 0)
            {
                var due = queue.Where(q => q.at <= time).ToList();
                if (due.Count > 0)
                {
                    queue = queue.Where(q => q.at > time).ToList();
                    foreach (var q in due) { q.opts.player = player; Crack(q.tile, q.opts); }
                }
            }
            foreach (var t in tiles)
            {
                if (t.state == TileState.Solid) continue;
                t.t += dt;
                if (t.state == TileState.Crack && t.t >= t.dur) { t.state = TileState.Fall; t.t = 0; events.Add(new GameEvent("fall") { tile = t }); }
                else if (t.state == TileState.Fall && t.t >= Cfg.FallAnim) { t.state = TileState.Hole; t.t = 0; }
                else if (t.state == TileState.Hole && t.t >= t.holeDur) { t.state = TileState.Rise; t.t = 0; events.Add(new GameEvent("rise") { tile = t }); }
                else if (t.state == TileState.Rise && t.t >= Cfg.RiseTime) { t.state = TileState.Solid; t.t = 0; t.cause = null; events.Add(new GameEvent("solid") { tile = t }); }
            }
        }

        /// <summary>立刻把一块砖恢复（坠落后“梦把你接住”）</summary>
        public void Restore(Tile t)
        {
            if (t == null) return;
            t.state = TileState.Solid; t.t = 0; t.cause = null;
            queue = queue.Where(q => q.tile != t).ToList();
            events.Add(new GameEvent("restore") { tile = t });
        }

        /// <summary>这块砖立刻塌掉：被拖下去时用</summary>
        public void Collapse(Tile t)
        {
            if (t == null || t.door) return;
            t.state = TileState.Fall; t.t = 0; t.holeDur = rng.Range(Cfg.HoleTime);
            queue = queue.Where(q => q.tile != t).ToList();
            events.Add(new GameEvent("fall") { tile = t });
        }

        public bool IsOuter(Tile t) => t.i == 0 || t.j == 0 || t.i == n - 1 || t.j == n - 1;

        /// <summary>塌陷方式：返回实际排进队列的砖数</summary>
        public int Pattern(string kind, Tile player, int[] count)
        {
            var solid = tiles.Where(t => t.state == TileState.Solid && !t.door).ToList();
            int c = 0;
            void Put(Tile t, double delay, CrackOpts o) { Schedule(t, delay, o); c++; }
            switch (kind)
            {
                case "random":
                    {
                        int cnt = rng.Int(count[0], count[1]); var l = rng.Shuffle(solid);
                        for (int k = 0; k < Math.Min(cnt, l.Count); k++) Put(l[k], k * .35, new CrackOpts { cause = "random" });
                        break;
                    }
                case "under":
                    {
                        if (player == null) return Pattern("random", player, count);
                        var nb = rng.Shuffle(Neighbors(player, 1).Where(t => t.state == TileState.Solid && !t.door).ToList());
                        for (int k = 0; k < Math.Min(2, nb.Count); k++) Put(nb[k], k * .25, new CrackOpts { cause = "under" });
                        break;
                    }
                case "sweep":
                    {
                        // 一道裂缝带从一边扫到另一边：洞很短，扫过去马上升回来
                        bool axisI = rng.Chance(.5); int dir = rng.Chance(.5) ? 1 : -1; const double step = 1.05;
                        for (int r = 0; r < n; r++)
                        {
                            int row = dir > 0 ? r : n - 1 - r;
                            foreach (var t in tiles) if ((axisI ? t.i : t.j) == row && !t.door)
                                    Put(t, r * step, new CrackOpts { cause = "sweep", dur = crackTime * .85, holeDur = .3, keepMin = false });
                        }
                        break;
                    }
                case "ring":
                    {
                        // 外圈先塌，你被赶到中间；外圈回来时中间再塌
                        var outer = tiles.Where(t => IsOuter(t) && !t.door).ToList();
                        var inner = tiles.Where(t => !IsOuter(t) && !t.door).ToList();
                        foreach (var t in outer) Put(t, 0, new CrackOpts { cause = "ring", holeDur = 2.2, keepMin = false });
                        double back = crackTime + Cfg.FallAnim + 2.2 + Cfg.RiseTime * .6;
                        foreach (var t in inner) Put(t, back, new CrackOpts { cause = "ring", holeDur = 2.6, keepMin = false });
                        break;
                    }
                case "checker":
                    {
                        int par = rng.Int(0, 1);
                        foreach (var t in tiles) if ((t.i + t.j) % 2 == par && !t.door) Put(t, 0, new CrackOpts { cause = "checker", holeDur = 2.6, keepMin = false });
                        break;
                    }
            }
            return c;
        }
    }
}
