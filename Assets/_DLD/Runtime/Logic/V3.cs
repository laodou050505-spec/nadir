using System;

namespace DLD.Logic
{
    /// <summary>规则层的向量：用 double，和 Three.js 原型的数值逐位对得上，便于做一致性对比</summary>
    public struct V3
    {
        public double x, y, z;
        public V3(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static double Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static double Len(V3 a) => Math.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z);
        public static double Dist(V3 a, V3 b) => Len(a - b);
        public static double DistXZ(V3 a, V3 b) => Hypot(a.x - b.x, a.z - b.z);
        public static double Hypot(double a, double b) => Math.Sqrt(a * a + b * b);
        public static V3 Norm(V3 a) { double l = Len(a); if (l == 0) l = 1; return new V3(a.x / l, a.y / l, a.z / l); }
        /// <summary>点到射线的夹角（dir 需归一化）</summary>
        public static double AngleTo(V3 origin, V3 dir, V3 p)
        {
            var d = p - origin; double l = Len(d); if (l == 0) l = 1;
            return Math.Acos(Math.Max(-1, Math.Min(1, Dot(d, dir) / l)));
        }
        public static double Clamp(double v, double a, double b) => Math.Max(a, Math.Min(b, v));
    }
}
