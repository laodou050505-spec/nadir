using System.Collections.Generic;
using UnityEngine;

namespace DLD
{
    /// <summary>
    /// 地板块网格：一整块 + 碎成几块的版本。顶面在 y=0，子网格 0 磨石顶面、1 混凝土断面。
    /// UV2 = 顶面平面映射，碎块和整块共用同一张光照贴图区域，裂开时光影完全连续。
    /// 裂缝是共享折线：相邻碎块的边用同一个哈希抖动，拼起来没有缝。
    /// </summary>
    public static class SlabMesh
    {
        public const float Thick = 0.1f, Bevel = 0.006f;

        class B
        {
            public List<Vector3> v = new(), n = new(); public List<Vector2> uv = new(), uv2 = new(); public List<int> top = new(), side = new();
            public int Add(Vector3 p, Vector3 nor, Vector2 u, Vector2 u2) { v.Add(p); n.Add(nor); uv.Add(u); uv2.Add(u2); return v.Count - 1; }
        }

        static Mesh Build(B b, string name)
        {
            var m = new Mesh { name = name };
            m.SetVertices(b.v); m.SetNormals(b.n); m.SetUVs(0, b.uv); m.SetUVs(1, b.uv2);
            m.subMeshCount = 2; m.SetTriangles(b.top, 0); m.SetTriangles(b.side, 1);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        /// <summary>把多边形（顺时针俯视，xz 平面）挤出成一块</summary>
        static void Extrude(B b, IList<Vector2> poly, float size, Vector2 uvOff)
        {
            Vector2 c = Vector2.zero; foreach (var p in poly) c += p; c /= poly.Count;
            float area = 0; for (int i = 0; i < poly.Count; i++) { var a = poly[i]; var d = poly[(i + 1) % poly.Count]; area += a.x * d.y - d.x * a.y; }
            int sign = area > 0 ? 1 : -1;       // Unity 正面顺时针（从 +y 看 xz）
            Vector2 U(Vector2 p) => p + uvOff;
            Vector2 U2(Vector2 p) => new Vector2(p.x / size + .5f, p.y / size + .5f);
            // 顶面扇形
            int ci = b.Add(new Vector3(c.x, 0, c.y), Vector3.up, U(c), U2(c));
            int first = b.v.Count;
            foreach (var p in poly) b.Add(new Vector3(p.x, 0, p.y), Vector3.up, U(p), U2(p));
            for (int i = 0; i < poly.Count; i++)
            {
                int a = first + i, d = first + (i + 1) % poly.Count;
                if (sign > 0) b.top.AddRange(new[] { ci, d, a }); else b.top.AddRange(new[] { ci, a, d });
            }
            // 侧面 + 底面（断面用混凝土）
            for (int i = 0; i < poly.Count; i++)
            {
                var p = poly[i]; var q = poly[(i + 1) % poly.Count];
                var e = (q - p).normalized; var nor = new Vector3(e.y * sign, 0, -e.x * sign);
                float l = (q - p).magnitude;
                var u0 = new Vector2(0, 0) + uvOff; var ux = new Vector2(l, 0) + uvOff; var uy = new Vector2(0, Thick) + uvOff; var uxy = new Vector2(l, Thick) + uvOff;
                // 断面取靠里一点的光照，不要吃到贴图边缘的黑
                Vector2 Sd(Vector2 v) => Vector2.Lerp(U2(v), U2(c), .3f);
                int i0 = b.Add(new Vector3(p.x, 0, p.y), nor, u0, Sd(p));
                int i1 = b.Add(new Vector3(q.x, 0, q.y), nor, ux, Sd(q));
                int i2 = b.Add(new Vector3(q.x, -Thick, q.y), nor, uxy, Sd(q));
                int i3 = b.Add(new Vector3(p.x, -Thick, p.y), nor, uy, Sd(p));
                if (sign > 0) b.side.AddRange(new[] { i0, i1, i2, i0, i2, i3 }); else b.side.AddRange(new[] { i0, i2, i1, i0, i3, i2 });
            }
            int cb = b.Add(new Vector3(c.x, -Thick, c.y), Vector3.down, U(c), U2(c));
            int fb = b.v.Count;
            foreach (var p in poly) b.Add(new Vector3(p.x, -Thick, p.y), Vector3.down, U(p), U2(c));   // 底面 UV2 压成一点，不和顶面抢光照贴图
            for (int i = 0; i < poly.Count; i++)
            {
                int a = fb + i, d = fb + (i + 1) % poly.Count;
                if (sign > 0) b.side.AddRange(new[] { cb, a, d }); else b.side.AddRange(new[] { cb, d, a });
            }
        }

        public static Mesh Whole(float size, float gap, Vector2 uvOff)
        {
            float h = size / 2 - gap;
            var b = new B();
            // 带一圈小倒角：外圈略低
            var outer = new[] { new Vector2(-h, -h), new Vector2(h, -h), new Vector2(h, h), new Vector2(-h, h) };
            Extrude(b, outer, size, uvOff);
            float r = h - Bevel;
            var inner = new[] { new Vector2(-r, -r), new Vector2(r, -r), new Vector2(r, r), new Vector2(-r, r) };
            // 用内圈重新铺顶面，外圈做成斜面
            b.top.Clear();
            int first = b.v.Count;
            foreach (var p in inner) b.Add(new Vector3(p.x, 0, p.y), Vector3.up, p + uvOff, new Vector2(p.x / size + .5f, p.y / size + .5f));
            b.top.AddRange(new[] { first, first + 3, first + 2, first, first + 2, first + 1 });
            for (int i = 0; i < 4; i++)
            {
                var o0 = outer[i]; var o1 = outer[(i + 1) % 4]; var n0 = inner[i]; var n1 = inner[(i + 1) % 4];
                var mid = (o0 + o1).normalized; var nor = new Vector3(mid.x, 1, mid.y).normalized;
                int a = b.Add(new Vector3(n0.x, 0, n0.y), nor, n0 + uvOff, new Vector2(n0.x / size + .5f, n0.y / size + .5f));
                int c = b.Add(new Vector3(n1.x, 0, n1.y), nor, n1 + uvOff, new Vector2(n1.x / size + .5f, n1.y / size + .5f));
                int d = b.Add(new Vector3(o1.x, -Bevel, o1.y), nor, o1 + uvOff, new Vector2(o1.x / size + .5f, o1.y / size + .5f));
                int e = b.Add(new Vector3(o0.x, -Bevel, o0.y), nor, o0 + uvOff, new Vector2(o0.x / size + .5f, o0.y / size + .5f));
                b.top.AddRange(new[] { a, e, d, a, d, c });
            }
            return Build(b, "SlabWhole");
        }

        // ---------------------------------------- 碎裂
        static Vector2 Jitter(Vector2 a, Vector2 b, float amp)
        {
            // 用端点坐标（与方向无关）做哈希：相邻碎块共享同一抖动
            var lo = (a.x < b.x || (a.x == b.x && a.y < b.y)) ? a : b; var hi = lo == a ? b : a;
            float h = Mathf.Sin(lo.x * 127.1f + lo.y * 311.7f + hi.x * 74.7f + hi.y * 269.5f) * 43758.5453f; h -= Mathf.Floor(h);
            var dir = hi - lo; var n = new Vector2(-dir.y, dir.x).normalized;
            return (a + b) * .5f + n * (h - .5f) * amp * dir.magnitude;
        }

        static List<Vector2> Rough(IList<Vector2> poly, float half)
        {
            var cur = new List<Vector2>(poly);
            for (int level = 0; level < 3; level++)
            {
                var next = new List<Vector2>();
                for (int i = 0; i < cur.Count; i++)
                {
                    var a = cur[i]; var b = cur[(i + 1) % cur.Count];
                    next.Add(a);
                    bool border = (Mathf.Abs(a.x - b.x) < 1e-4f && Mathf.Abs(Mathf.Abs(a.x) - half) < 1e-3f) || (Mathf.Abs(a.y - b.y) < 1e-4f && Mathf.Abs(Mathf.Abs(a.y) - half) < 1e-3f);
                    if ((a - b).magnitude > .05f) next.Add(border ? (a + b) * .5f : Jitter(a, b, .22f));
                }
                cur = next;
            }
            return cur;
        }

        static (List<Vector2>, List<Vector2>) Cut(List<Vector2> poly, Vector2 p, Vector2 n)
        {
            var a = new List<Vector2>(); var b = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                var s = poly[i]; var t = poly[(i + 1) % poly.Count];
                float ds = Vector2.Dot(s - p, n), dt = Vector2.Dot(t - p, n);
                (ds >= 0 ? a : b).Add(s);
                if (ds * dt < 0) { var m = s + (t - s) * (ds / (ds - dt)); a.Add(m); b.Add(m); }
            }
            return (a, b);
        }

        static float Area(List<Vector2> p) { float v = 0; for (int i = 0; i < p.Count; i++) { var q = p[(i + 1) % p.Count]; v += p[i].x * q.y - q.x * p[i].y; } return Mathf.Abs(v) / 2; }

        /// <summary>碎成 count 块：返回每块的网格和中心。seed 相同时裂缝走向相同，所以裂纹图可以共用</summary>
        public static (Mesh mesh, Vector2 center)[] Fracture(float size, float gap, int count, int seed, Vector2 uvOff)
        {
            var polys = Polys(size, gap, count, seed); float h = size / 2 - gap;
            var res = new (Mesh, Vector2)[polys.Count];
            for (int i = 0; i < polys.Count; i++)
            {
                var rough = Rough(polys[i], h);
                Vector2 c = Vector2.zero; foreach (var q in rough) c += q; c /= rough.Count;
                var b = new B(); Extrude(b, rough, size, uvOff);
                res[i] = (Build(b, "SlabPiece" + i), c);
            }
            return res;
        }

        /// <summary>裂纹图：R 是线，G 是这段裂缝几时爬到（0 起点，1 最远）。和同 seed 的碎块完全重合</summary>
        public static Texture2D CrackMap(float size, float gap, int count, int seed, int res = 256)
        {
            var polys = Polys(size, gap, count, seed); float h = size / 2 - gap;
            var R = new System.Random(seed * 31 + 7);
            var origin = new Vector2((float)R.NextDouble() - .5f, (float)R.NextDouble() - .5f) * size * .4f;
            var cover = new float[res * res]; var when = new float[res * res];
            float ppm = res / size, maxD = size * 1.1f, half = 1.3f;   // half：线半宽（像素）
            foreach (var poly in polys)
            {
                var rough = Rough(poly, h);
                for (int i = 0; i < rough.Count; i++)
                {
                    var a = rough[i]; var b = rough[(i + 1) % rough.Count];
                    if (Mathf.Abs(Mathf.Abs(a.x) - h) < 1e-3f && Mathf.Abs(Mathf.Abs(b.x) - h) < 1e-3f) continue;   // 砖的外边不是裂缝
                    if (Mathf.Abs(Mathf.Abs(a.y) - h) < 1e-3f && Mathf.Abs(Mathf.Abs(b.y) - h) < 1e-3f) continue;
                    var pa = (a + Vector2.one * size / 2) * ppm; var pb = (b + Vector2.one * size / 2) * ppm;
                    int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.x, pb.x) - 3)), x1 = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(pa.x, pb.x) + 3));
                    int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(pa.y, pb.y) - 3)), y1 = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(pa.y, pb.y) + 3));
                    var ab = pb - pa; float l2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
                    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                        {
                            var p = new Vector2(x + .5f, y + .5f);
                            float k = Mathf.Clamp01(Vector2.Dot(p - pa, ab) / l2);
                            float d = (p - (pa + ab * k)).magnitude;
                            float c = Mathf.Clamp01(half + .5f - d);
                            if (c <= 0) continue;
                            int id = y * res + x;
                            if (c > cover[id]) { cover[id] = c; when[id] = Mathf.Clamp01(((a + (b - a) * k) - origin).magnitude / maxD); }
                        }
                }
            }
            var px = new byte[res * res * 2];
            for (int i = 0; i < cover.Length; i++) { px[i * 2] = (byte)(cover[i] * 255); px[i * 2 + 1] = (byte)(when[i] * 255); }
            var tex = new Texture2D(res, res, TextureFormat.RG16, true, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixelData(px, 0); tex.Apply(true, false);
            return tex;
        }

        static List<List<Vector2>> Polys(float size, float gap, int count, int seed)
        {
            var R = new System.Random(seed);
            float h = size / 2 - gap;
            var polys = new List<List<Vector2>> { new() { new Vector2(-h, -h), new Vector2(h, -h), new Vector2(h, h), new Vector2(-h, h) } };
            int guard = 0;
            while (polys.Count < count && guard++ < 100)
            {
                polys.Sort((x, y) => Area(y).CompareTo(Area(x)));
                var P = polys[0]; polys.RemoveAt(0);
                Vector2 c = Vector2.zero; foreach (var q in P) c += q; c /= P.Count;
                c += new Vector2((float)R.NextDouble() - .5f, (float)R.NextDouble() - .5f) * size * .12f;
                float ang = (float)R.NextDouble() * Mathf.PI;
                var (a, b) = Cut(P, c, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)));
                if (a.Count < 3 || b.Count < 3 || Mathf.Min(Area(a), Area(b)) < Area(P) * .2f) { polys.Add(P); continue; }
                polys.Add(a); polys.Add(b);
            }
            return polys;
        }
    }
}
