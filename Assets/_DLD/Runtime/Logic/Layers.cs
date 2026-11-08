using System.Collections.Generic;

namespace DLD.Logic
{
    public class HandRule { public int max; public double[] interval; public int hp; }
    public class EyeRule { public double[] cool, open; }

    public class Layer
    {
        public int id; public double decay; public string name;
        public int shards, shardActive; public double[] shardGap, shardY; public double shardOverHole;
        public double[] interval; public double minInterval, heat;
        public (string k, double w)[] patterns; public int[] randomCount;
        public bool chase; public HandRule hands; public EyeRule eye; public double par;
        public double figureMin = 2.375;    // 黑影最多逼到离中心多近：前两层在地板外，之后一层比一层往中间挤
        public double blackoutK = 1;        // 熄灯类惊吓的权重倍数（停电层加大）
    }

    /// <summary>同一座医院的八层深度。每往下一层新加一种压力，场景也更破败（decay 0..1）</summary>
    public static class Layers
    {
        static double[] R(double a, double b) => new[] { a, b };
        /// <summary>新手教学用的一层：什么都不会自己发生，全由 Tutorial 安排</summary>
        public static readonly Layer Lesson = new Layer { id = 0, decay = 0, name = "lesson", shards = 1, shardGap = R(9, 9), shardActive = 1, shardY = R(1.2, 1.2), interval = R(99, 99), minInterval = 99, patterns = new[] { ("random", 1.0) }, randomCount = new[] { 1, 1 }, hands = new HandRule { max = 1, interval = R(1e6, 1e6), hp = 1 }, eye = new EyeRule { cool = R(1e6, 1e6), open = R(5, 5) }, par = 1e6 };
        /// <summary>
        /// 十二层。前三层各教一样东西；第 6 层是喘息层（不加新东西，节奏放慢）；第 9 层是停电层（熄灯更勤）；
        /// 最后三层逐步拉满。灯从 5 盏涨到 11 盏，每层比原来的八层版本长四成左右。
        /// blackoutK 是熄灯类惊吓的权重倍数，只在停电层和最后两层加大
        /// </summary>
        public static readonly Layer[] All =
        {
            new Layer { id = 1, decay = 0, name = "lobby", shards = 5, shardGap = R(7, 9), shardActive = 1, shardY = R(.95, 1.3), shardOverHole = 0,
                interval = R(3.4, 4.4), minInterval = 2.3, heat = .011, patterns = new[] { ("random", 1.0) }, randomCount = new[] { 1, 2 }, chase = false, par = 90 },
            new Layer { id = 2, decay = .08, name = "corridor", shards = 6, shardGap = R(7.5, 9.5), shardActive = 1, shardY = R(.75, 1.35), shardOverHole = .2,
                interval = R(3.2, 4), minInterval = 2.1, heat = .013, patterns = new[] { ("random", 3.0), ("under", 1.0) }, randomCount = new[] { 1, 2 }, chase = true,
                hands = new HandRule { max = 1, interval = R(8, 11), hp = 1 }, par = 110 },
            new Layer { id = 3, figureMin = 2.2, decay = .17, name = "ward", shards = 6, shardGap = R(7.5, 9.5), shardActive = 1, shardY = R(.65, 1.4), shardOverHole = .28,
                interval = R(3.1, 3.8), minInterval = 2, heat = .014, patterns = new[] { ("random", 3.0), ("under", 1.0) }, randomCount = new[] { 1, 2 }, chase = true,
                hands = new HandRule { max = 1, interval = R(8, 11), hp = 1 }, eye = new EyeRule { cool = R(13, 17), open = R(3.5, 4.5) }, par = 120 },
            new Layer { id = 4, figureMin = 2, decay = .26, name = "waiting", shards = 7, shardGap = R(7.5, 9.5), shardActive = 1, shardY = R(.6, 1.4), shardOverHole = .32,
                interval = R(3.1, 3.8), minInterval = 2, heat = .015, patterns = new[] { ("random", 2.0), ("sweep", 2.0) }, randomCount = new[] { 1, 2 }, chase = false,
                hands = new HandRule { max = 1, interval = R(8, 11), hp = 1 }, eye = new EyeRule { cool = R(13, 17), open = R(3.5, 4.5) }, par = 130 },
            new Layer { id = 5, figureMin = 1.8, decay = .35, name = "surgery", shards = 7, shardGap = R(7.5, 9.5), shardActive = 2, shardY = R(.55, 1.4), shardOverHole = .35,
                interval = R(2.9, 3.6), minInterval = 1.85, heat = .016, patterns = new[] { ("random", 2.0), ("ring", 2.0), ("under", 1.0) }, randomCount = new[] { 1, 2 }, chase = true,
                hands = new HandRule { max = 2, interval = R(8, 11), hp = 1 }, eye = new EyeRule { cool = R(12, 16), open = R(4, 5) }, par = 140 },
            // 喘息层：不加新东西，塌得慢一点、少一盏灯，让人缓口气，但更暗了。不用横扫：大地图上横扫要走很远才躲得开
            new Layer { id = 6, figureMin = 1.8, decay = .45, name = "chapel", shards = 6, shardGap = R(8, 10), shardActive = 1, shardY = R(.6, 1.4), shardOverHole = .25,
                interval = R(3.6, 4.4), minInterval = 2.3, heat = .012, patterns = new[] { ("random", 1.0) }, randomCount = new[] { 1, 2 }, chase = false,
                hands = new HandRule { max = 1, interval = R(10, 13), hp = 1 }, eye = new EyeRule { cool = R(16, 20), open = R(3.5, 4.5) }, par = 140 },
            new Layer { id = 7, figureMin = 1.6, decay = .55, name = "dark", shards = 8, shardGap = R(7.5, 9.5), shardActive = 2, shardY = R(.55, 1.45), shardOverHole = .4,
                interval = R(2.9, 3.5), minInterval = 1.8, heat = .016, patterns = new[] { ("random", 2.0), ("checker", 1.0), ("under", 1.0) }, randomCount = new[] { 1, 2 }, chase = true,
                hands = new HandRule { max = 2, interval = R(7, 10), hp = 1 }, eye = new EyeRule { cool = R(11, 14), open = R(4, 5) }, par = 150 },
            new Layer { id = 8, figureMin = 1.45, decay = .63, name = "morgue", shards = 8, shardGap = R(7.5, 9.5), shardActive = 2, shardY = R(.55, 1.45), shardOverHole = .42,
                interval = R(2.8, 3.4), minInterval = 1.7, heat = .017, patterns = new[] { ("random", 2.0), ("sweep", 1.0), ("ring", 1.0), ("under", 1.0) }, randomCount = new[] { 1, 2 }, chase = true,
                hands = new HandRule { max = 2, interval = R(7, 10), hp = 1 }, eye = new EyeRule { cool = R(11, 14), open = R(4, 5) }, par = 160 },
            // 停电层：灯经常整片熄掉，只能靠灯泡和裂隙的光找路
            new Layer { id = 9, figureMin = 1.35, decay = .72, name = "outage", shards = 9, shardGap = R(7.5, 9.5), shardActive = 2, shardY = R(.55, 1.45), shardOverHole = .42, blackoutK = 3,
                interval = R(2.9, 3.5), minInterval = 1.8, heat = .016, patterns = new[] { ("random", 2.0), ("checker", 1.0), ("under", 1.0) }, randomCount = new[] { 1, 2 }, chase = true,
                hands = new HandRule { max = 2, interval = R(7, 10), hp = 1 }, eye = new EyeRule { cool = R(12, 15), open = R(4, 5) }, par = 170 },
            new Layer { id = 10, figureMin = 1.2, decay = .82, name = "basement", shards = 9, shardGap = R(7.5, 9.5), shardActive = 2, shardY = R(.5, 1.45), shardOverHole = .45,
                interval = R(2.7, 3.3), minInterval = 1.6, heat = .018, patterns = new[] { ("random", 2.0), ("sweep", 1.0), ("ring", 1.0), ("checker", 1.0), ("under", 1.0) }, randomCount = new[] { 1, 3 }, chase = true,
                hands = new HandRule { max = 2, interval = R(6, 9), hp = 2 }, eye = new EyeRule { cool = R(10, 13), open = R(4.5, 5.5) }, par = 180 },
            new Layer { id = 11, figureMin = 1.0, decay = .92, name = "boiler", shards = 10, shardGap = R(7.5, 9.5), shardActive = 2, shardY = R(.5, 1.45), shardOverHole = .48, blackoutK = 1.6,
                interval = R(2.6, 3.1), minInterval = 1.5, heat = .019, patterns = new[] { ("random", 2.0), ("sweep", 1.0), ("ring", 1.0), ("checker", 1.0), ("under", 2.0) }, randomCount = new[] { 1, 3 }, chase = true,
                hands = new HandRule { max = 3, interval = R(6, 8.5), hp = 2 }, eye = new EyeRule { cool = R(9, 12), open = R(5, 6) }, par = 195 },
            new Layer { id = 12, figureMin = 0.85, decay = 1, name = "bottom", shards = 11, shardGap = R(7.5, 9.5), shardActive = 2, shardY = R(.5, 1.45), shardOverHole = .5, blackoutK = 1.6,
                interval = R(2.5, 3), minInterval = 1.45, heat = .02, patterns = new[] { ("random", 2.0), ("sweep", 1.0), ("ring", 1.0), ("checker", 1.0), ("under", 2.0) }, randomCount = new[] { 1, 3 }, chase = true,
                hands = new HandRule { max = 3, interval = R(5, 8), hp = 2 }, eye = new EyeRule { cool = R(8, 11), open = R(5, 6.5) }, par = 210 },
        };
    }
}
