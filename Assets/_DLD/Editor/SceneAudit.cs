// 场景巡检：菜单 DLD/3 巡检。
// 1. 共面重叠：朝向相同、落在同一平面（2 毫米内）且互相盖住的三角形，这就是贴图闪烁的来源
// 2. 实体穿模：道具和建筑、道具和道具、墙上的牌和道具、天花破损和建筑
// 报告写到 /tmp/dld_audit.txt
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DLD.EditorTools
{
    public static class SceneAudit
    {
        class Tri { public Vector3 a, b, c, n; public string owner; public int sub; public float d; }

        static IEnumerable<MeshFilter> Generated()
        {
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
            {
                if (!mf.sharedMesh || !mf.sharedMesh.isReadable) continue;
                var n = mf.name;
                if (n == "Haze" || n == "Mist" || n.StartsWith("Bottom") || n == "ShaftMask" || n == "Fade" || mf.GetComponentInParent<DLD.Play.UIFlow>(true) != null || n == "FloorRimSmall" || n == "FloorRimTiny" || n.StartsWith("Slab") || n == "Contact") continue;
                if (n.StartsWith("L") && int.TryParse(n.Substring(1), out int lv) && lv > 7) continue;     // 远处的竖井层只查近的，形状一样
                if (mf.transform.IsChildOf(Object.FindAnyObjectByType<DLD.Play.GameRoot>().transform)) continue;
                yield return mf;
            }
        }

        static List<Tri> Tris()
        {
            var all = new List<Tri>();
            foreach (var mf in Generated())
            {
                var m = mf.sharedMesh; var v = m.vertices; var l2w = mf.transform.localToWorldMatrix;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var t = m.GetTriangles(s);
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 a = l2w.MultiplyPoint3x4(v[t[i]]), b = l2w.MultiplyPoint3x4(v[t[i + 1]]), c = l2w.MultiplyPoint3x4(v[t[i + 2]]);
                        var n = Vector3.Cross(b - a, c - a); if (n.sqrMagnitude < 1e-10f) continue; n.Normalize();
                        all.Add(new Tri { a = a, b = b, c = c, n = n, d = Vector3.Dot(n, a), owner = mf.name + "@" + mf.transform.position.ToString("0.0"), sub = s });
                    }
                }
            }
            return all;
        }

        static List<Vector2> Clip(List<Vector2> poly, Vector2 a, Vector2 b)
        {
            var o = new List<Vector2>();
            var e = b - a; var nrm = new Vector2(-e.y, e.x);
            for (int i = 0; i < poly.Count; i++)
            {
                var p = poly[i]; var q = poly[(i + 1) % poly.Count];
                float dp = Vector2.Dot(p - a, nrm), dq = Vector2.Dot(q - a, nrm);
                if (dp >= 0) o.Add(p);
                if (dp * dq < 0) o.Add(p + (q - p) * (dp / (dp - dq)));
            }
            return o;
        }
        static float Area(List<Vector2> p) { float s = 0; for (int i = 0; i < p.Count; i++) { var q = p[(i + 1) % p.Count]; s += p[i].x * q.y - q.x * p[i].y; } return Mathf.Abs(s) / 2; }

        static float Overlap(Tri x, Tri y, out Vector3 centre)
        {
            var u = Vector3.Cross(x.n, Mathf.Abs(x.n.y) > .9f ? Vector3.right : Vector3.up).normalized; var v = Vector3.Cross(x.n, u);
            Vector2 P(Vector3 p) => new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v));
            var A = new[] { P(x.a), P(x.b), P(x.c) }; var B = new[] { P(y.a), P(y.b), P(y.c) };
            if ((A[1].x - A[0].x) * (A[2].y - A[0].y) - (A[1].y - A[0].y) * (A[2].x - A[0].x) < 0) { var t = A[1]; A[1] = A[2]; A[2] = t; }
            var poly = new List<Vector2>(B);
            if ((B[1].x - B[0].x) * (B[2].y - B[0].y) - (B[1].y - B[0].y) * (B[2].x - B[0].x) < 0) { poly.Reverse(); }
            for (int i = 0; i < 3 && poly.Count > 0; i++) poly = Clip(poly, A[i], A[(i + 1) % 3]);
            centre = default;
            if (poly.Count < 3) return 0;
            float ar = Area(poly); var c2 = Vector2.zero; foreach (var p in poly) c2 += p; c2 /= poly.Count;
            centre = u * c2.x + v * c2.y + x.n * x.d;
            return ar;
        }

        [MenuItem("DLD/3 巡检")]
        public static string Run()
        {
            var sb = new StringBuilder();
            // ── 共面重叠 ──
            var tris = Tris();
            var buckets = new Dictionary<(int, int, int, long), List<Tri>>();
            foreach (var t in tris)
            {
                var key = (Mathf.RoundToInt(t.n.x * 50), Mathf.RoundToInt(t.n.y * 50), Mathf.RoundToInt(t.n.z * 50), (long)Mathf.RoundToInt(t.d / .002f));
                if (!buckets.TryGetValue(key, out var l)) buckets[key] = l = new();
                l.Add(t);
            }
            // ── 实体穿模 ──
            var temp = new List<Component>();
            foreach (var mf in Generated())
            {
                if (mf.name == "Papers" || mf.GetComponent<Collider>()) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc);
            }
            Physics.SyncTransforms();
            Physics.queriesHitBackfaces = true;
            var hits = new Dictionary<string, (float area, Vector3 c)>();
            foreach (var l in buckets.Values)
                for (int i = 0; i < l.Count; i++) for (int j = i + 1; j < l.Count; j++)
                    {
                        float ar = Overlap(l[i], l[j], out var c);
                        if (ar < 8e-4f) continue;                 // 小于 8 平方厘米的碎片不算
                        // 面前 6 厘米内有东西盖着 = 藏在结构里，看不到，不算
                        if (Physics.Raycast(c + l[i].n * .06f, -l[i].n, .05f)) continue;
                        string k = string.CompareOrdinal(l[i].owner, l[j].owner) < 0 ? $"{l[i].owner}#{l[i].sub} ~ {l[j].owner}#{l[j].sub}" : $"{l[j].owner}#{l[j].sub} ~ {l[i].owner}#{l[i].sub}";
                        hits.TryGetValue(k, out var h); hits[k] = (h.area + ar, c);
                    }
            Physics.queriesHitBackfaces = false;
            sb.AppendLine($"== 看得见的共面重叠 {hits.Count} 组（三角形 {tris.Count}） ==");
            foreach (var kv in hits.OrderByDescending(k => k.Value.area).Take(60)) sb.AppendLine($"{kv.Value.area:0.000}㎡ @ {kv.Value.c.ToString("0.00")}  {kv.Key}");

            // ── 实体穿模 ──
            var props = new List<(string n, Transform t, Bounds b)>();
            foreach (var l in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude))
            {
                if (l.transform.IsChildOf(Object.FindAnyObjectByType<DLD.Play.GameRoot>().transform)) continue;
                var rs = l.GetLODs()[0].renderers.Where(r => r).ToArray(); if (rs.Length == 0) continue;
                var root = l.transform; var b = new Bounds(); bool has = false;
                foreach (var r in rs)
                {
                    var lb = r.localBounds; var m = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    for (int i = 0; i < 8; i++)
                    {
                        var p = m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1)));
                        if (!has) { b = new Bounds(p, Vector3.zero); has = true; } else b.Encapsulate(p);
                    }
                }
                var bc = l.gameObject.AddComponent<BoxCollider>(); bc.center = b.center; bc.size = b.size; temp.Add(bc);
                props.Add((l.name + "@" + root.position.ToString("0.0"), root, b));
            }
            Physics.SyncTransforms();
            int nc = 0;
            sb.AppendLine("== 实体穿模 ==");
            // 用真实顶点：离墙、离扶手、离别的道具
            const float hall = 6.43f, arm = 1.53f, end = 30.48f;
            var boxes = new List<(string n, Bounds b)>();
            foreach (var l in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude))
            {
                if (l.transform.IsChildOf(Object.FindAnyObjectByType<DLD.Play.GameRoot>().transform)) continue;
                var lods = l.GetLODs(); var r = lods[lods.Length - 1].renderers[0]; var v = r.GetComponent<MeshFilter>().sharedMesh.vertices; var m = r.transform.localToWorldMatrix;
                string n = l.name + "@" + l.transform.position.ToString("0.0");
                float worst = 0; var bb = new Bounds(m.MultiplyPoint3x4(v[0]), Vector3.zero);
                foreach (var p in v)
                {
                    var w = m.MultiplyPoint3x4(p); bb.Encapsulate(w);
                    float ax = Mathf.Abs(w.x), az = Mathf.Abs(w.z), over;
                    if (ax <= hall && az <= hall) over = 0;
                    else if (ax > az) over = Mathf.Max(az - arm, ax - end);        // X 向走廊
                    else over = Mathf.Max(ax - arm, az - end);
                    if (ax > hall && az > hall) over = Mathf.Max(ax, az) - hall;
                    worst = Mathf.Max(worst, over);
                }
                if (worst > .005f) { sb.AppendLine($"{n} 伸出可用空间 {worst:0.000} 米"); nc++; }
                boxes.Add((n, bb));
            }
            for (int i = 0; i < boxes.Count; i++) for (int j = i + 1; j < boxes.Count; j++)
                {
                    var a2 = boxes[i].b; var b2 = boxes[j].b; a2.Expand(-.02f); b2.Expand(-.02f);
                    if (a2.Intersects(b2)) { sb.AppendLine($"{boxes[i].n} ~ {boxes[j].n}"); nc++; }
                }
            // 天花破损、线缆、牌子碰建筑
            foreach (var mf in Generated())
                if (mf.name == "CeilPanel" || mf.name == "SignPlate" || mf.name == "Exit" || mf.name == "Troffer")
                {
                    var bd = mf.sharedMesh.bounds;
                    var c = mf.transform.TransformPoint(bd.center);
                    foreach (var hit in Physics.OverlapBox(c, bd.extents * .85f, mf.transform.rotation))
                    {
                        if (hit.transform == mf.transform) continue;
                        if (hit.name == "CeilHole" || hit.name == "Cable") continue;
                        if (hit.name.StartsWith("Arm") && (mf.name == "SignPlate" || mf.name == "Exit")) continue;
                        if (hit.name == "Hall" && mf.name == "SignPlate") continue;
                        sb.AppendLine($"{mf.name}@{mf.transform.position.ToString("0.0")} ~ {hit.name}"); nc++;
                    }
                }
            sb.AppendLine($"穿模共 {nc} 处");
            foreach (var c in temp) Object.DestroyImmediate(c);
            System.IO.File.WriteAllText("/tmp/dld_audit.txt", sb.ToString());
            return $"共面 {hits.Count} 组，穿模 {nc} 处";
        }
    }
}
