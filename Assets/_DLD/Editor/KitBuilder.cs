// 医院建筑模块：竖井层、大厅、走廊。尺寸都写在这里，改了重跑菜单 DLD/1 搭建场景。
using System.Collections.Generic;
using UnityEngine;

namespace DLD.EditorTools
{
    public static class KitBuilder
    {
        public const string Dir = "Assets/_DLD/Art/Generated/";
        public const float HW = 1.875f;      // 井口半宽（3.75 米地图）
        public const float RW = HW + 0.75f;  // 每层凹进去的墙
        public const float FloorH = 3f;
        public const int Levels = 40;
        public const float HallHalf = 6.5f, HallH = 4.6f, ArmHalf = 1.6f, ArmH = 3.5f, Bay = 4f, HoleHalf = 2.5f;
        public const int ArmBays = 6;
        public const float ShaftH = RW - .08f;   // 井壁几乎平整，长长的透视线才看得出深

        // ---------------------------------------------------------------- 竖井
        public static Mesh Shaft(int variant, bool near)
        {
            var k = new MeshKit();
            float h = ShaftH, r = RW, ring = 0.32f, H = FloorH;
            // 每层的圈梁
            k.Box("s_concrete", new Vector3(-r - .2f, -ring, -r - .2f), new Vector3(-h, 0, r + .2f), .025f);
            k.Box("s_concrete", new Vector3(h, -ring, -r - .2f), new Vector3(r + .2f, 0, r + .2f), .025f);
            k.Box("s_concrete", new Vector3(-h, -ring, -r - .2f), new Vector3(h, 0, -h), .025f);
            k.Box("s_concrete", new Vector3(-h, -ring, h), new Vector3(h, 0, r + .2f), .025f);
            // 凹进去的墙
            k.Box("s_concrete", new Vector3(-r - .2f, -H, -r - .2f), new Vector3(-r, -ring, r + .2f));
            k.Box("s_concrete", new Vector3(r, -H, -r - .2f), new Vector3(r + .2f, -ring, r + .2f));
            k.Box("s_concrete", new Vector3(-r, -H, -r - .2f), new Vector3(r, -ring, -r));
            k.Box("s_concrete", new Vector3(-r, -H, r), new Vector3(r, -ring, r + .2f));
            // 四角立柱
            foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                k.Box("s_concrete", new Vector3(sx * r - .12f, -H, sz * r - .12f), new Vector3(sx * r + .12f, -ring, sz * r + .12f), .02f);

            // 每面一盏笼式防爆灯（暖光）
            float lampY = -0.25f, lx = h - .13f;
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
            {
                var wall = d * h; wall.y = lampY;
                var c = d * lx; c.y = lampY;
                k.Cyl("s_steel", wall + d * .002f, wall - d * .045f, .085f, near ? 12 : 6);
                k.Cyl("s_lamp", wall - d * .045f, wall - d * .17f, .058f, near ? 12 : 6, true, .05f);
                if (!near) continue;
                var u = Vector3.Cross(d, Vector3.up);
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + .5f) * Mathf.PI / 2; var o = (Mathf.Cos(a) * u + Mathf.Sin(a) * Vector3.up) * .072f;
                    k.Tube("s_steel", new[] { wall - d * .045f + o, wall - d * .16f + o * .96f, wall - d * .19f + o * .3f, wall - d * .195f }, .005f, 4);
                }
            }
            // 粗管道（靠 +X 墙）：两根
            float px = h - .03f; int seg = near ? 14 : 6;
            foreach (var (z, pr) in new[] { (-1.45f, .105f), (-1.0f, .08f) })
            {
                k.Cyl("s_pipe", new Vector3(px, 0, z), new Vector3(px, -H, z), pr, seg, false);
                if (near)
                {
                    k.Cyl("s_steel", new Vector3(px, -.62f, z), new Vector3(px, -.68f, z), pr + .03f, seg);
                    k.Cyl("s_steel", new Vector3(px, -.72f, z), new Vector3(px, -.74f, z), pr + .015f, seg);
                    k.Box("s_steel", new Vector3(px, -1.9f, z - pr - .02f), new Vector3(px + .3f, -1.85f, z + pr + .02f));
                }
            }
            if (!near)
            {
                k.Box("s_steel", new Vector3(-.3f, -H, h - .05f), new Vector3(.3f, 0, h - .01f));     // 线槽（远处简化）
                k.Box("s_steel", new Vector3(-h + .03f, -H, -.24f), new Vector3(-h + .08f, 0, -.2f));  // 爬梯（远处简化）
                k.Box("s_steel", new Vector3(-h + .03f, -H, .2f), new Vector3(-h + .08f, 0, .24f));
                return k.Save(Dir + $"ShaftFar{variant}.asset", false);
            }
            // 细管 + 卡箍
            k.Cyl("s_pipe", new Vector3(h - .02f, 0, .55f), new Vector3(h - .02f, -H, .55f), .032f, 8, false);
            for (int i = 0; i < 2; i++) k.Box("s_steel", new Vector3(h - .07f, -1 - i * 1.2f, .5f), new Vector3(h + .01f, -.96f - i * 1.2f, .6f));
            // 爬梯（靠 −X 墙）
            float lx2 = -h + .07f;
            foreach (var z in new[] { -.22f, .22f })
            {
                k.Box("s_steel", new Vector3(lx2 - .015f, -H, z - .025f), new Vector3(lx2 + .015f, 0, z + .025f));
                for (int i = 0; i < 2; i++) k.Box("s_steel", new Vector3(-h, -.8f - i * 1.6f, z - .02f), new Vector3(lx2, -.77f - i * 1.6f, z + .02f));
            }
            for (int i = 0; i < 10; i++) k.Cyl("s_steel", new Vector3(lx2, -.15f - i * .3f, -.22f), new Vector3(lx2, -.15f - i * .3f, .22f), .011f, 5, false);
            // 穿孔线槽（靠 +Z 墙）
            float tz = h - .02f;
            k.Box("s_steel", new Vector3(-.3f, -H, tz - .012f), new Vector3(.3f, 0, tz));
            foreach (var x in new[] { -.3f, .3f }) k.Box("s_steel", new Vector3(x - .012f, -H, tz - .08f), new Vector3(x + .012f, 0, tz));
            for (int i = 0; i < 8; i++) k.Box("s_cable", new Vector3(-.26f + i * .07f, -H, tz - .05f), new Vector3(-.22f + i * .07f, 0, tz - .012f));
            for (int i = 0; i < 3; i++) k.Box("s_steel", new Vector3(-.31f, -.5f - i, tz - .09f), new Vector3(.31f, -.47f - i, tz - .075f));
            // 变体：横穿的管子，打破重复
            if (variant == 1)
            {
                k.Cyl("s_pipe", new Vector3(-r, -2.1f, r - .3f), new Vector3(r, -2.1f, r - .3f), .09f, 12, false);
                k.Cyl("s_pipe", new Vector3(r - .3f, -1.6f, -r), new Vector3(r - .3f, -1.6f, r), .06f, 10, false);
            }
            if (variant == 2)
                k.Tube("s_pipe", new[] { new Vector3(-r, -1.5f, -r + .3f), new Vector3(-1.3f, -1.5f, -r + .3f), new Vector3(-1.0f, -1.2f, -r + .3f), new Vector3(1f, -1.2f, -r + .3f), new Vector3(1.3f, -1.5f, -r + .3f), new Vector3(r, -1.5f, -r + .3f) }, .07f, 10);
            return k.Save(Dir + $"ShaftNear{variant}.asset", false);
        }

        // ---------------------------------------------------------------- 墙
        /// <summary>
        /// 一段墙：局部坐标 u 沿墙、y 向上、d 朝房间内。doors 是门的中心 u。
        /// 下半青色瓷砖、上半绿漆、踢脚、腰线、不锈钢扶手。
        /// </summary>
        static void Wall(MeshKit k, float u0, float u1, float H, IList<float> doors, bool rail = true)
        {
            const float dw = .62f, dh = 2.12f, tileTop = 1.15f;
            var cuts = new List<(float a, float b)>();
            float cur = u0;
            var ds = new List<float>(doors ?? new float[0]); ds.Sort();
            foreach (var c in ds) { cuts.Add((cur, c - dw)); cur = c + dw; }
            cuts.Add((cur, u1));

            foreach (var (a, b) in cuts)
            {
                if (b - a < .01f) continue;
                k.Box("paint", new Vector3(a, tileTop + .03f, -.2f), new Vector3(b, H, 0));
                k.Box("tile", new Vector3(a, .13f, -.2f), new Vector3(b, tileTop, .012f));
                k.Box("skirt", new Vector3(a, 0, -.2f), new Vector3(b, .13f, .022f), .006f);
                k.Box("trim", new Vector3(a, tileTop, -.2f), new Vector3(b, tileTop + .03f, .024f), .006f);
                if (!rail) continue;
                float ra = a + .12f, rb = b - .12f;
                if (rb - ra < .5f) continue;
                const float ry = .92f, rd = .085f;
                k.Tube("chrome", new[] { new Vector3(ra, ry, .012f), new Vector3(ra, ry, rd - .025f), new Vector3(ra + .025f, ry, rd), new Vector3(rb - .025f, ry, rd), new Vector3(rb, ry, rd - .025f), new Vector3(rb, ry, .012f) }, .019f, 10);
                int n = Mathf.Max(2, Mathf.RoundToInt((rb - ra) / 1.3f) + 1);
                for (int i = 1; i < n - 1; i++)
                {
                    float u = Mathf.Lerp(ra, rb, i / (n - 1f));
                    k.Cyl("chrome", new Vector3(u, ry - .05f, .012f), new Vector3(u, ry - .05f, rd - .01f), .008f, 6);
                    k.Cyl("chrome", new Vector3(u, ry - .05f, rd - .01f), new Vector3(u, ry - .015f, rd), .008f, 6);
                    k.Cyl("chrome", new Vector3(u, ry - .05f, .012f), new Vector3(u, ry - .05f, .02f), .03f, 10);
                }
            }
            foreach (var c in ds) Door(k, c, dw, dh, H);
        }

        static void Door(MeshKit k, float c, float dw, float dh, float H)
        {
            float a = c - dw, b = c + dw;
            k.Box("paint", new Vector3(a, dh, -.2f), new Vector3(b, H, 0));
            float fw = .07f;     // 门框
            k.Box("trim", new Vector3(a, 0, -.2f), new Vector3(a + fw, dh, .035f), .008f);
            k.Box("trim", new Vector3(b - fw, 0, -.2f), new Vector3(b, dh, .035f), .008f);
            k.Box("trim", new Vector3(a, dh - fw, -.2f), new Vector3(b, dh, .035f), .008f);
            // 门扇（凹进 8 公分）
            float l = a + fw, r = b - fw, top = dh - fw;
            float wl = l + .17f, wr = r - .17f, wb = 1.22f, wt = 1.82f;   // 小窗
            const float fd = -.08f;
            k.Box("wood", new Vector3(l, 0, -.14f), new Vector3(r, wb, fd));
            k.Box("wood", new Vector3(l, wt, -.14f), new Vector3(r, top, fd));
            k.Box("wood", new Vector3(l, wb, -.14f), new Vector3(wl, wt, fd));
            k.Box("wood", new Vector3(wr, wb, -.14f), new Vector3(r, wt, fd));
            k.Quad("glass", new Vector3(wl, wb, fd - .03f), new Vector3(wr, wb, fd - .03f), new Vector3(wr, wt, fd - .03f), new Vector3(wl, wt, fd - .03f), Vector3.forward);
            foreach (var (x0, x1, y0, y1) in new[] { (wl - .02f, wl + .012f, wb, wt), (wr - .012f, wr + .02f, wb, wt), (wl, wr, wb - .02f, wb + .012f), (wl, wr, wt - .012f, wt + .02f) })
                k.Box("trim", new Vector3(x0, y0, fd - .03f), new Vector3(x1, y1, fd + .006f));
            k.Box("trim", new Vector3(l, 0, fd), new Vector3(r, .22f, fd + .004f));          // 踢板
            k.Cyl("chrome", new Vector3(r - .09f, 1.02f, fd), new Vector3(r - .09f, 1.02f, fd + .05f), .011f, 8);
            k.Cyl("chrome", new Vector3(r - .09f, 1.02f, fd + .05f), new Vector3(r - .2f, 1.02f, fd + .05f), .011f, 8);
            k.Cyl("chrome", new Vector3(r - .09f, 1.02f, fd), new Vector3(r - .09f, 1.02f, fd + .006f), .03f, 12);
            // 门洞两侧和地面补缝
            k.Box("skirt", new Vector3(a, -.01f, -.2f), new Vector3(b, .0f, -.05f));
        }

        static void Pilaster(MeshKit k, float u, float H, float halfW = .2f, float d = .13f)
        {
            k.Box("paint", new Vector3(u - halfW, 1.18f, -.1f), new Vector3(u + halfW, H, d), .012f);
            k.Box("tile", new Vector3(u - halfW, .13f, -.1f), new Vector3(u + halfW, 1.15f, d + .012f), .004f);
            k.Box("skirt", new Vector3(u - halfW - .01f, 0, -.1f), new Vector3(u + halfW + .01f, .13f, d + .022f), .006f);
            k.Box("trim", new Vector3(u - halfW - .004f, 1.15f, -.1f), new Vector3(u + halfW + .004f, 1.18f, d + .024f), .006f);
        }

        /// <summary>局部 → 世界：墙起点、沿墙方向、朝内方向</summary>
        static Matrix4x4 WallFrame(Vector3 origin, Vector3 along, Vector3 inward)
        {
            var m = Matrix4x4.identity;
            m.SetColumn(0, along); m.SetColumn(1, Vector3.up); m.SetColumn(2, inward); m.SetColumn(3, new Vector4(origin.x, origin.y, origin.z, 1));
            return m;
        }

        // ---------------------------------------------------------------- 走廊（沿 +Z，从 z=0 开始，x 宽 ±ArmHalf）
        public static Mesh Arm(int variant)
        {
            var k = new MeshKit();
            float w = ArmHalf, H = ArmH, L = ArmBays * Bay;
            // 每开间一扇门，左右错开；变体换门的位置
            var left = new List<float>(); var right = new List<float>();
            for (int i = 0; i < ArmBays; i++)
            {
                float c = i * Bay + Bay * .5f;
                if ((i + variant) % 2 == 0) left.Add(c); else right.Add(c);
                if ((i + variant) % 3 == 0) right.Add(c + 1.0f);
            }
            const float start = .27f;     // 让过大厅的门套，两者不共面
            k.xf = WallFrame(new Vector3(-w, 0, 0), Vector3.forward, Vector3.right); Wall(k, start, L, H, left);
            k.xf = WallFrame(new Vector3(w, 0, 0), Vector3.forward, Vector3.left); Wall(k, start, L, H, right);
            for (int i = 1; i < ArmBays; i++)
            {
                k.xf = WallFrame(new Vector3(-w, 0, 0), Vector3.forward, Vector3.right); Pilaster(k, i * Bay, H);
                k.xf = WallFrame(new Vector3(w, 0, 0), Vector3.forward, Vector3.left); Pilaster(k, i * Bay, H);
            }
            k.xf = Matrix4x4.identity;
            k.Quad("terrazzo", new Vector3(-w, 0, 0), new Vector3(w, 0, 0), new Vector3(w, 0, L), new Vector3(-w, 0, L), Vector3.up);
            // 天花：吊顶板 + 每开间一道横梁
            k.Quad("ceiling", new Vector3(-w, H - .02f, 0), new Vector3(w, H - .02f, 0), new Vector3(w, H - .02f, L), new Vector3(-w, H - .02f, L), Vector3.down);
            for (int i = 1; i <= ArmBays; i++)
                k.Box("paint", new Vector3(-w, H - .26f, i * Bay - .19f), new Vector3(w, H, i * Bay + .19f), .012f);
            foreach (var x in new[] { -w, w - .1f })
                k.Box("paint", new Vector3(x, H - .12f, start), new Vector3(x + .1f, H, L));   // 檐线
            // 尽头墙
            k.Box("paint", new Vector3(-w, 0, L), new Vector3(w, H, L + .2f));
            return k.Save(Dir + $"Arm{variant}.asset", true);
        }

        // ---------------------------------------------------------------- 大厅
        public static Mesh Hall()
        {
            var k = new MeshKit();
            float S = HallHalf, H = HallH, a = ArmHalf, lintel = ArmH - .12f;
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0, 90 * i, 0);
                var along = rot * Vector3.right; var inward = rot * Vector3.back; var origin = rot * new Vector3(-S, 0, S);
                k.xf = WallFrame(origin, along, inward);
                float l = S - a, r = S + a;     // 走廊口在中间
                Wall(k, 0, l, H, i % 2 == 0 ? new[] { l * .46f } : new[] { l * .58f });
                Wall(k, r, 2 * S, H, i == 1 ? new float[0] : new[] { r + (2 * S - r) * .52f });
                // 门楣和走廊口包边
                k.Box("paint", new Vector3(l, lintel, -.25f), new Vector3(r, H, 0));
                k.Box("trim", new Vector3(l - .04f, lintel - .05f, -.25f), new Vector3(r + .04f, lintel, .03f), .008f);
                k.Box("paint", new Vector3(l - .14f, 1.18f, -.25f), new Vector3(l + .0f, lintel, .05f), .01f);
                k.Box("paint", new Vector3(r, 1.18f, -.25f), new Vector3(r + .14f, lintel, .05f), .01f);
                k.Box("tile", new Vector3(l - .14f, .13f, -.25f), new Vector3(l, 1.15f, .06f), .004f);
                k.Box("tile", new Vector3(r, .13f, -.25f), new Vector3(r + .14f, 1.15f, .06f), .004f);
                Pilaster(k, l * .22f + .3f, H, .22f, .16f);
                Pilaster(k, 2 * S - (l * .22f + .3f), H, .22f, .16f);
            }
            k.xf = Matrix4x4.identity;
            // 地面（中间留洞，洞里是地砖）
            float h = HoleHalf;
            k.Quad("terrazzo", new Vector3(-S, 0, -S), new Vector3(S, 0, -S), new Vector3(S, 0, -h), new Vector3(-S, 0, -h), Vector3.up);
            k.Quad("terrazzo", new Vector3(-S, 0, h), new Vector3(S, 0, h), new Vector3(S, 0, S), new Vector3(-S, 0, S), Vector3.up);
            k.Quad("terrazzo", new Vector3(-S, 0, -h), new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(-S, 0, h), Vector3.up);
            k.Quad("terrazzo", new Vector3(h, 0, -h), new Vector3(S, 0, -h), new Vector3(S, 0, h), new Vector3(h, 0, h), Vector3.up);
            // 天花：格栅梁 + 吊顶板
            k.Quad("ceiling", new Vector3(-S, H - .02f, -S), new Vector3(S, H - .02f, -S), new Vector3(S, H - .02f, S), new Vector3(-S, H - .02f, S), Vector3.down);
            for (int i = -2; i <= 2; i++)
            {
                float p = i * S / 2f;
                k.Box("paint", new Vector3(-S, H - .3f, p - .17f), new Vector3(S, H, p + .17f), .012f);
                k.Box("paint", new Vector3(p - .17f, H - .3f, -S), new Vector3(p + .17f, H, S), .012f);
            }
            return k.Save(Dir + "Hall.asset", true);
        }

        // ---------------------------------------------------------------- 小件
        /// <summary>车削网格：profile 从下到上 (半径, 高度)</summary>
        public static Mesh Lathe(string name, (float r, float y)[] prof, int seg = 20)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>(); int N = prof.Length;
            for (int i = 0; i < N; i++)
            {
                var a = prof[Mathf.Max(i - 1, 0)]; var b = prof[Mathf.Min(i + 1, N - 1)];
                var tan = new Vector2(b.r - a.r, b.y - a.y).normalized; var nor = new Vector2(tan.y, -tan.x);
                for (int k = 0; k <= seg; k++)
                {
                    float ang = k * Mathf.PI * 2 / seg; float c = Mathf.Cos(ang), s = Mathf.Sin(ang);
                    v.Add(new Vector3(c * prof[i].r, prof[i].y, s * prof[i].r)); n.Add(new Vector3(c * nor.x, nor.y, s * nor.x));
                }
            }
            for (int i = 0; i < N - 1; i++) for (int k = 0; k < seg; k++)
                {
                    int a = i * (seg + 1) + k, b = a + seg + 1;
                    t.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var m = new Mesh { name = name }; m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
            var path = Dir + name + ".asset"; UnityEditor.AssetDatabase.DeleteAsset(path); UnityEditor.AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>灯下的光雾锥：上窄下宽，uv.y=1 在灯口</summary>
        public static Mesh HazeCone()
        {
            const int seg = 16; float h = 2.2f; var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg; var d = new Vector3(Mathf.Cos(a) * .55f, 0, Mathf.Sin(a));
                v.Add(d * .5f); n.Add(d.normalized); uv.Add(new Vector2((float)i / seg, 1));
                v.Add(d * 1.5f + Vector3.down * h); n.Add(d.normalized); uv.Add(new Vector2((float)i / seg, 0));
            }
            for (int i = 0; i < seg; i++) { int a = i * 2; t.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); }
            var m = new Mesh { name = "HazeCone" }; m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateBounds();
            var path = Dir + "HazeCone.asset"; UnityEditor.AssetDatabase.DeleteAsset(path); UnityEditor.AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>荧光灯盘：tube 子网格单独发光，开关只换材质</summary>
        public static Mesh Troffer()
        {
            var k = new MeshKit();
            k.Box("trim", new Vector3(-.32f, -.07f, -.65f), new Vector3(.32f, 0, .65f), .008f);
            k.Box("glass", new Vector3(-.27f, -.075f, -.6f), new Vector3(.27f, -.06f, .6f));
            foreach (var x in new[] { -.12f, .12f }) k.Cyl("tube", new Vector3(x, -.11f, -.57f), new Vector3(x, -.11f, .57f), .017f, 8);
            foreach (var x in new[] { -.12f, .12f }) foreach (var z in new[] { -.58f, .58f }) k.Box("trim", new Vector3(x - .03f, -.13f, z - .02f), new Vector3(x + .03f, -.06f, z + .02f));
            return k.Save(Dir + "Troffer.asset", false);
        }

        /// <summary>出口灯：门上方的小灯箱（不写字，只是一块发光的青绿色）</summary>
        public static Mesh ExitBox()
        {
            var k = new MeshKit();
            k.Box("trim", new Vector3(-.2f, -.09f, 0), new Vector3(.2f, .09f, .06f), .006f);
            k.Box("tube", new Vector3(-.17f, -.065f, .06f), new Vector3(.17f, .065f, .066f));
            return k.Save(Dir + "ExitBox.asset", false);
        }
    }
}
