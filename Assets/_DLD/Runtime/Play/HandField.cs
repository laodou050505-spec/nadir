using System.Collections.Generic;
using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>深渊之手的表现：触手从井的深处一路伸上来，穿过破洞，越过洞沿，梢部停在规则层给的位置</summary>
    public class HandField : MonoBehaviour
    {
        public Tentacle prefab;
        class Item
        {
            public Tentacle t; public float seed, taper = 2.4f, curl = .45f; public int n, holeI; public Vector3[] path;
            public bool back; public float reeled, speed; public Vector3 pass;
        }
        readonly List<Item> leaving = new();
        readonly Dictionary<AbyssHand, Item> live = new();
        readonly List<AbyssHand> gone = new();
        readonly Vector3[] ctrl = new Vector3[20];
        const float Knee = .5f;      // 触手最高到膝盖，再高就太吓人了
#if UNITY_EDITOR
        public static long RingChecks, RingClips;
#endif

        public void Clear() { foreach (var i in live.Values) Destroy(i.t.gameObject); live.Clear(); foreach (var i in leaving) Destroy(i.t.gameObject); leaving.Clear(); }

        /// <summary>
        /// 沿自己的身体缩回去：记下被拍时的整条路径，梢部顺着这条路径一路退，身体不变形、不跳，也不断。
        /// 地面上慢一点，看得清；钻进洞以后加速，一直退到井深处才消失
        /// </summary>
        readonly Vector3[] trimmed = new Vector3[20];
        bool Retract(Item it, float time)
        {
            float dt = Time.deltaTime; var p = it.path;
            it.speed = Mathf.Min(it.speed + dt * (it.reeled < 1.2f ? 1.6f : 16), 22); it.reeled += it.speed * dt;
            it.taper = Mathf.Lerp(it.taper, 2.4f, 1 - Mathf.Exp(-dt * 3)); it.curl = Mathf.MoveTowards(it.curl, 0, dt);
            float left = it.reeled; int last = it.n - 1;
            while (last > 0)
            {
                float seg = Vector3.Distance(p[last], p[last - 1]);
                if (left < seg) break;
                left -= seg; last--;
            }
            if (last < 1) return true;      // 全部缩完
            System.Array.Copy(p, trimmed, last);
            float s2 = Vector3.Distance(p[last], p[last - 1]);
            trimmed[last] = Vector3.Lerp(p[last], p[last - 1], left / Mathf.Max(s2, 1e-3f));
            int cnt = last + 1;
            if (trimmed[last].y < -15) return true;
            float holeT = it.holeI < cnt ? (float)it.holeI / (cnt - 1) : -1;
            it.t.Shape(trimmed, cnt, time, .03f, it.seed, holeT, it.curl, it.taper, null, Knee);
            return false;
        }

        public void Tick(Game g, Vector3 head, float time, bool eyeVisible = false)
        {
            var hands = g.hands;
            foreach (var h in hands)
                if (!live.ContainsKey(h))
                {
                    live[h] = new Item { t = Instantiate(prefab, transform), seed = h.id * 1.9f };
                }
            gone.Clear();
            foreach (var kv in live) if (!hands.Contains(kv.Key)) gone.Add(kv.Key);
            // 规则层已经结束的手：表现上还要继续缩完，不能当场消失
            foreach (var h in gone) { var it0 = live[h]; live.Remove(h); if (it0.back && it0.t.gameObject.activeSelf) leaving.Add(it0); else Destroy(it0.t.gameObject); }
            for (int i = leaving.Count - 1; i >= 0; i--) if (Retract(leaving[i], time)) { Destroy(leaving[i].t.gameObject); leaving.RemoveAt(i); }
            foreach (var h in hands)
            {
                var it = live[h]; var t = it.t;
                bool show = h.phase != "omen";
                if (t.gameObject.activeSelf != show) t.gameObject.SetActive(show);
                if (!show) continue;
                if (h.phase == "recoil" && !it.back && it.path != null) { it.back = true; it.reeled = 0; it.speed = .5f; }
                if (it.back) { if (Retract(it, time)) t.gameObject.SetActive(false); continue; }
                var tip = InputSource.ToU(h.pos); var a = InputSource.ToU(h.anchor);
                // 穿洞点：触手很粗，只能从洞的中间穿过，再从洞沿上方跨过去，不穿地砖
                var pass = a; pass.y = 0;
                if (h.src != null) { var c = new Vector3((float)h.src.x, 0, (float)h.src.z); pass = Vector3.Lerp(a, c, .88f); pass.y = 0; }
                // 钻出来时从洞中间往上，缩回时沿原路退回洞里：梢部不能隔着地砖直上直下
                if (h.phase == "rise") { float k = Mathf.Clamp01(tip.y / .5f); var xz = Vector3.Lerp(pass, tip, k * k); tip.x = xz.x; tip.z = xz.z; }
                var deep = new Vector3(Mathf.Clamp(pass.x * 1.5f, -2.3f, 2.3f), -16, Mathf.Clamp(pass.z * 1.5f, -2.3f, 2.3f));
                int n = 0;
                if (eyeVisible)
                {
                    // 巨眼占满了井的中间：贴着离得最近的井角绕过去，不从眼球里穿
                    var cor = new Vector3(pass.x >= 0 ? 1.78f : -1.78f, 0, pass.z >= 0 ? 1.78f : -1.78f);
                    ctrl[n++] = cor + Vector3.down * 17; ctrl[n++] = cor + Vector3.down * 9; ctrl[n++] = cor + Vector3.down * 3.1f;
                }
                else
                {
                    ctrl[n++] = deep;
                    ctrl[n++] = Vector3.Lerp(deep, pass, .55f) + new Vector3(Mathf.Sin(it.seed) * .5f, 0, Mathf.Cos(it.seed) * .5f);
                }
                ctrl[n++] = pass + Vector3.down * 1.3f;
                int holeI = n;
                ctrl[n++] = pass + Vector3.down * .1f;
                float above = Mathf.Max(tip.y, 0);
                var flat = tip - pass; flat.y = 0;
                if (above > .05f)
                {
                    // 手就在洞口上方时直接往上走，只有离得够远才拱过洞沿，否则管子会打结
                    if (flat.magnitude > .4f)
                    {
                        // 圆润的拱：急弯会让粗管子折出褶皱
                        ctrl[n++] = pass + Vector3.up * .3f + (tip - pass) * .08f;
                        var arch = Vector3.Lerp(pass, tip, .3f); arch.y = .3f; ctrl[n++] = arch;
                        arch = Vector3.Lerp(pass, tip, .68f); arch.y = .28f; ctrl[n++] = arch;
                    }
                    else ctrl[n++] = Vector3.Lerp(pass, tip, .5f);
                }
                if (h.phase == "grab")
                {
                    // 缠住小腿：从脚踝一圈圈绕上去
                    var foot = new Vector3((float)g.feet.x, 0, (float)g.feet.z);
                    for (int k = 0; k < 8; k++)
                    {
                        float a2 = k * 1.3f + time * .4f;
                        ctrl[n++] = foot + new Vector3(Mathf.Cos(a2) * .19f, .08f + k * .048f, Mathf.Sin(a2) * .19f);
                    }
                }
                else ctrl[n++] = tip;
                // 地面以上的部分绕开黑影
                for (int i = 3; i < n; i++)
                {
                    if (ctrl[i].y < .05f) continue;
                    foreach (var f in g.scares.effigies)
                    {
                        if (!f.placed) continue;
                        var dxz = new Vector3(ctrl[i].x - (float)f.x, 0, ctrl[i].z - (float)f.z); float dd = dxz.magnitude;
                        if (dd < .45f && dd > .001f) ctrl[i] += dxz / dd * (.45f - dd);
                    }
                }
                float holeT = (float)holeI / (n - 1);
                it.path ??= new Vector3[ctrl.Length]; System.Array.Copy(ctrl, it.path, n); it.n = n; it.holeI = holeI; it.pass = pass;
                it.taper = h.phase == "grab" ? 6f : 2.4f; it.curl = h.phase == "grab" ? 0 : .45f;
                float sway = h.phase == "grab" ? .02f : h.phase == "reach" ? .05f : .09f;
                t.Shape(ctrl, n, time, sway, it.seed, holeT, h.phase == "grab" ? 0 : .45f, h.phase == "grab" ? 6f : 2.4f, null, Knee);   // 缠腿时要细得多，弯得过来
#if UNITY_EDITOR
                foreach (var p in t.Centers)
                {
                    if (p.y < -.14f || p.y > .03f) continue;
                    RingChecks++;
                    var tl = g.floor.TileAt(p.x, p.z);
                    if (tl == null ? true : tl.state == TileState.Solid || tl.state == TileState.Crack) RingClips++;
                }
#endif
            }
        }
    }
}
