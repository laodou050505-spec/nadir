// 程序化建筑网格：倒角方盒、圆柱、管子、四边面。全部按世界米制投影 UV，按材质分子网格。
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DLD.EditorTools
{
    public class MeshKit
    {
        class Sub { public List<Vector3> v = new(), n = new(); public List<Vector2> uv = new(); public List<int> t = new(); }
        readonly Dictionary<string, Sub> subs = new();
        readonly List<string> order = new();
        public Matrix4x4 xf = Matrix4x4.identity;

        Sub S(string m) { if (!subs.TryGetValue(m, out var s)) { s = new Sub(); subs[m] = s; order.Add(m); } return s; }
        Vector3 P(Vector3 p) => xf.MultiplyPoint3x4(p);
        Vector3 D(Vector3 d) => xf.MultiplyVector(d).normalized;

        static Vector2 Proj(Vector3 p, Vector3 n)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            if (ay >= ax && ay >= az) return new Vector2(p.x, p.z);
            return ax >= az ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
        }

        /// <summary>四边面（已在世界坐标），outward 决定朝向</summary>
        void QuadW(string m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(n, outward) < 0) { (b, d) = (d, b); n = -n; }
            var s = S(m); int i = s.v.Count;
            foreach (var p in new[] { a, b, c, d }) { s.v.Add(p); s.n.Add(n); s.uv.Add(Proj(p, n)); }
            s.t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        void TriW(string m, Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(n, outward) < 0) { (b, c) = (c, b); n = -n; }
            var s = S(m); int i = s.v.Count;
            foreach (var p in new[] { a, b, c }) { s.v.Add(p); s.n.Add(n); s.uv.Add(Proj(p, n)); }
            s.t.AddRange(new[] { i, i + 1, i + 2 });
        }

        public void Quad(string m, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward) => QuadW(m, P(a), P(b), P(c), P(d), D(outward));

        /// <summary>方盒，ch 为倒角</summary>
        public void Box(string m, Vector3 min, Vector3 max, float ch = 0)
        {
            // 方盒拼接时面会重合，重合的面就是贴图闪烁。逐个挑一个不冲突的外扩量，让重合的面错开至少 2 毫米。地面不动，必须绝对平
            if (m != "terrazzo")
            {
                for (int k = 0; k < 16; k++)
                {
                    float g = k * .0022f;
                    if (FacesFree(min - Vector3.one * g, max + Vector3.one * g)) { min -= Vector3.one * g; max += Vector3.one * g; break; }
                }
                AddFaces(min, max);
            }
            var c = (min + max) * .5f; var e = (max - min) * .5f;
            ch = Mathf.Min(ch, e.x * .9f, e.y * .9f, e.z * .9f);
            Vector3 Corner(int sx, int sy, int sz, int axis)
            {
                var v = new Vector3(sx * e.x, sy * e.y, sz * e.z);
                for (int a = 0; a < 3; a++) if (a != axis) v[a] -= Mathf.Sign(v[a]) * ch;
                return P(c + v);
            }
            // 六个面
            for (int a = 0; a < 3; a++)
                for (int s = -1; s <= 1; s += 2)
                {
                    int b = (a + 1) % 3, d = (a + 2) % 3;
                    int[] sg = new int[3]; sg[a] = s;
                    Vector3 Q(int sb, int sd) { sg[b] = sb; sg[d] = sd; return Corner(sg[0], sg[1], sg[2], a); }
                    var o = Vector3.zero; o[a] = s;
                    QuadW(m, Q(-1, -1), Q(1, -1), Q(1, 1), Q(-1, 1), D(o));
                }
            if (ch <= 0) return;
            // 十二条倒角
            for (int a = 0; a < 3; a++)
            {
                int b = (a + 1) % 3, d = (a + 2) % 3;
                for (int sb = -1; sb <= 1; sb += 2)
                    for (int sd = -1; sd <= 1; sd += 2)
                    {
                        int[] g = new int[3]; g[b] = sb; g[d] = sd;
                        Vector3 V(int sa, int ax) { g[a] = sa; return Corner(g[0], g[1], g[2], ax); }
                        var o = Vector3.zero; o[b] = sb; o[d] = sd;
                        QuadW(m, V(-1, b), V(1, b), V(1, d), V(-1, d), D(o));
                    }
            }
            // 八个角
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                        TriW(m, Corner(x, y, z, 0), Corner(x, y, z, 1), Corner(x, y, z, 2), D(new Vector3(x, y, z)));
        }

        public void Cyl(string m, Vector3 p0, Vector3 p1, float r, int seg = 8, bool caps = true, float r1 = -1)
        {
            if (r1 < 0) r1 = r;
            var a = P(p0); var b = P(p1); var axis = (b - a).normalized;
            float sc = xf.MultiplyVector(Vector3.right).magnitude; r *= sc; r1 *= sc;
            var u = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right).normalized;
            var v = Vector3.Cross(axis, u);
            var s = S(m); int i0 = s.v.Count; float len = (b - a).magnitude;
            for (int k = 0; k <= seg; k++)
            {
                float t = k * Mathf.PI * 2 / seg; var n = Mathf.Cos(t) * u + Mathf.Sin(t) * v;
                s.v.Add(a + n * r); s.n.Add(n); s.uv.Add(new Vector2(t * r, 0));
                s.v.Add(b + n * r1); s.n.Add(n); s.uv.Add(new Vector2(t * r, len));
            }
            for (int k = 0; k < seg; k++)
            {
                int i = i0 + k * 2;
                s.t.AddRange(new[] { i, i + 3, i + 1, i, i + 2, i + 3 });
            }
            if (caps) { Fan(m, a, u, v, r, seg, -axis); Fan(m, b, u, v, r1, seg, axis); }
        }

        void Fan(string m, Vector3 c, Vector3 u, Vector3 v, float r, int seg, Vector3 normal)
        {
            for (int k = 0; k < seg; k++)
            {
                float t0 = k * Mathf.PI * 2 / seg, t1 = (k + 1) * Mathf.PI * 2 / seg;
                TriW(m, c, c + (Mathf.Cos(t0) * u + Mathf.Sin(t0) * v) * r, c + (Mathf.Cos(t1) * u + Mathf.Sin(t1) * v) * r, normal);
            }
        }

        /// <summary>沿折线的圆管（平滑法线）</summary>
        public void Tube(string m, IList<Vector3> pts, float r, int seg = 8)
        {
            var s = S(m); int i0 = s.v.Count; Vector3 u = default; float l = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                var p = P(pts[i]);
                var f = (P(pts[Mathf.Min(i + 1, pts.Count - 1)]) - P(pts[Mathf.Max(i - 1, 0)])).normalized;
                if (i == 0) u = Vector3.Cross(f, Mathf.Abs(f.y) < .9f ? Vector3.up : Vector3.right).normalized;
                u = (u - f * Vector3.Dot(u, f)).normalized;
                var v = Vector3.Cross(f, u);
                if (i > 0) l += (p - P(pts[i - 1])).magnitude;
                for (int k = 0; k <= seg; k++)
                {
                    float t = k * Mathf.PI * 2 / seg; var n = Mathf.Cos(t) * u + Mathf.Sin(t) * v;
                    s.v.Add(p + n * r); s.n.Add(n); s.uv.Add(new Vector2(t * r, l));
                }
            }
            for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < seg; k++)
                {
                    int a = i0 + i * (seg + 1) + k, b = a + 1, c = a + seg + 1, d = c + 1;
                    s.t.AddRange(new[] { a, d, b, a, c, d });
                }
        }

        struct Face { public Vector3 n; public float d; public Bounds b; }
        readonly List<Face> faces = new();

        IEnumerable<Face> FacesOf(Vector3 min, Vector3 max)
        {
            for (int axis = 0; axis < 3; axis++)
                for (int side = 0; side < 2; side++)
                {
                    var lo = min; var hi = max; float v = side == 0 ? min[axis] : max[axis]; lo[axis] = hi[axis] = v;
                    var n = Vector3.zero; n[axis] = side == 0 ? -1 : 1;
                    Vector3 a = xf.MultiplyPoint3x4(lo), b = xf.MultiplyPoint3x4(hi), wn = xf.MultiplyVector(n).normalized;
                    var bd = new Bounds(a, Vector3.zero); bd.Encapsulate(b);
                    yield return new Face { n = wn, d = Vector3.Dot(wn, a), b = bd };
                }
        }

        bool FacesFree(Vector3 min, Vector3 max)
        {
            foreach (var f in FacesOf(min, max))
                foreach (var o in faces)
                {
                    if (Vector3.Dot(f.n, o.n) < .99f || Mathf.Abs(f.d - o.d) > .0021f) continue;
                    var b1 = f.b; b1.Expand(-.002f + 0); var b2 = o.b;
                    bool hit = true; int flat = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        float lo = Mathf.Max(f.b.min[i], o.b.min[i]), hi = Mathf.Min(f.b.max[i], o.b.max[i]);
                        if (Mathf.Abs(f.n[i]) > .5f) continue;
                        if (hi - lo < .003f) { hit = false; break; }
                        flat++;
                    }
                    if (hit && flat == 2) return false;
                }
            return true;
        }

        void AddFaces(Vector3 min, Vector3 max) { foreach (var f in FacesOf(min, max)) faces.Add(f); }

        public static readonly Dictionary<string, string[]> Orders = new();
        public int Tris { get { int n = 0; foreach (var s in subs.Values) n += s.t.Count / 3; return n; } }
        public string[] Materials => order.ToArray();

        /// <summary>写成网格资源。lightmap=true 时生成第二套 UV</summary>
        public Mesh Save(string path, bool lightmap)
        {
            var mesh = new Mesh { name = System.IO.Path.GetFileNameWithoutExtension(path), indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            foreach (var m in order) { var s = subs[m]; v.AddRange(s.v); n.AddRange(s.n); uv.AddRange(s.uv); }
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv);
            mesh.subMeshCount = order.Count;
            int off = 0;
            for (int i = 0; i < order.Count; i++)
            {
                var s = subs[order[i]];
                var t = new int[s.t.Count]; for (int k = 0; k < t.Length; k++) t[k] = s.t[k] + off;
                mesh.SetTriangles(t, i); off += s.v.Count;
            }
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            if (lightmap)
            {
                var p = new UnwrapParam(); UnwrapParam.SetDefaults(out p); p.hardAngle = 60; p.packMargin = 0.02f;
                Unwrapping.GenerateSecondaryUVSet(mesh, p);
            }
            Orders[mesh.name] = order.ToArray();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
