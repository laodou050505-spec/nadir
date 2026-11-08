namespace DLD.Logic
{
    /// <summary>全部数值集中在这里，真机调手感只改这个文件。单位：米 / 秒</summary>
    public static class Cfg
    {
        public const int N = 5;                      // 地图固定 5×5
        public static readonly string[] Areas = { "xs", "small", "medium", "large" };
        public static double TileOf(string area) => area switch { "xs" => .5, "small" => .6, "large" => 1.0, _ => .75 };

        // 地砖生命周期
        public const double CrackTime = 1.8, FastCrack = 1.1, FallAnim = .45, RiseTime = 1.1;
        public static readonly double[] HoleTime = { 5.5, 8.5 };

        public static class Feet { public const double EdgeTol = .12, FallGrace = .85, LeanBack = .22, OobMargin = .3; }
        public static class Fall { public const double Seq = 2.2, Invuln = 2.6, Wake = 2.2; public const int Lives = 3; }
        public static class Shard { public const double Touch = .14, GazeCone = .1, GazeDwell = .35, PullSpeed = 1.5, Life = 20, Flicker = 4; }
        public static class Door
        {
            public const double Rise = 1.8, Enter = .35, ReadyAt = .5, GiveUp = 30;   // 拉开一半就够，剩下的自己裂开
            public const double Crack = .1, Reach = .75, PullSpan = .4, AutoAfter = 10, AutoSpeed = .22;   // 裂缝初始宽度、够得着的距离、双手拉开多远算全开、检测不到手多久后自动打开
        }
        public static class Figure
        {
            public const double ThrustSpeed = 1.0, ThrustReady = .45, ThrustKeep = .8, PushHit = 3.4, FarSpeed = .3, NearSpeed = .03, TurnMin = 6, TurnMax = 11, TurnAngle = .45, SeenCone = .65, HeadCone = .6;
            public const double PointCone = .4, ArmOut = .34, EdgeTime = 8, EdgeTimeInside = 4.5, EdgeGap = 5;
            public const double PushSpeed = 2.4, PushDecay = 3.2, PushFar = 6, Hold = 2.2, KeepAway = 1.25;
        }
        public static class Hand
        {
            public const double PullTime = 3.2, PullDecay = 1.5;   // 被抓住连续拖这么久就坠落；松开后恢复
            public const double Omen = 2.8, Rise = 1.5, Aim = .7, ReachSpeed = .33, ReachMax = 4, ReachY = .42, GrabDist = .3,
                SwatSpeed = .85, SwatDist = .34, Recoil = .7, SpawnRange = 1.6;
        }
        public static class Eye
        {
            public const double Depth = 12, Radius = 3.3, Warn = 4.2, Closing = 2.4, SwatSpeed = 1.3, SwatClose = 1.1, SwatReady = .15, OpenK = 1.9, StayAfter = 2.5, SpotCool = 2.6, SeenFill = .7, SeenDecay = 1.6, CoverDist = .17;
            public const int BurstRadius = 1;
        }
        public const double StayLimit = 4, MinSolidRatio = .34;
    }
}
