using System.Collections.Generic;

namespace DLD.Logic
{
    /// <summary>规则层发给表现层的事件。表现层只读，不反向改规则</summary>
    public class GameEvent
    {
        public string type, cause, kind, what, phase; public Tile tile; public Shard shard; public AbyssHand hand;
        public V3 pos, from; public double dur, yaw; public int side, lives, idx, collected, goal, stars; public bool killed; public string handSide;
        public GameEvent(string type) { this.type = type; }
    }

    /// <summary>back：手背朝向的单位向量（读不到时为零）；wrist：手腕位置</summary>
    public struct HandInput { public bool tracked, pinch, hasTip; public V3 palm, tip, back, wrist; }

    /// <summary>每帧输入。坐标系 = 游玩区域本地坐标：区域中心为原点，地面 y=0</summary>
    public struct PlayerInput
    {
        public V3 headPos, headFwd, gazeOrigin, gazeDir; public bool hasGaze; public HandInput left, right;
        public HandInput Hand(int side) => side == 0 ? left : right;
    }

    public class Shard { public int id; public V3 pos; public double life, gazeT; public bool locked, dead, overHole; public int pulled = -1; }

    public class AbyssHand
    {
        public int id, hp; public Tile src; public V3 anchor, pos; public string phase; public double omenDur, t, cool, riseDur; public bool dead;
    }

    public class DoorState { public Tile tile; public double t, enterT, open, pull0, open0, noHands; public bool ready, pulling; }

    public class EyeState { public string phase = "closed"; public double t, dur, open, seen; public bool seeing; public V3 pos; public int spotted; }

    public class StayState { public Tile tile; public double t; public bool warned; }

    public class Effigy { public int id, variant; public double a, r, look, tilt, rustleCd, x, z, tx, tz, turnT, pushX, pushZ, edgeT, pushV, hold, pushT; public bool inHall; public bool moved, pushing, placed; }

    public class StepsState { public double x, z, t; public int n, side; }

    public class PendingScare { public string kind; public double t, dur; public V3 pos; }

    public class ResultInfo { public int stars, falls; public double time, par; }
}
