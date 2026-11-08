using System.Collections.Generic;
using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>危险在视野外时，视野边上有一个很小的手绘箭头指向它。看到了就自己消失，不抢画面</summary>
    public class DangerArrows : MonoBehaviour
    {
        public Transform cam; public MeshRenderer[] pool;
        const float Radius = .3f, Depth = .95f, InView = .78f;      // cos 约 39°：已经在视野里就不用提示
        class Slot { public float a, k; public bool init; public MaterialPropertyBlock mpb; }
        Slot[] slots; readonly List<(Vector3 pos, float urgency, Color col)> threats = new();
        static readonly int Tint = Shader.PropertyToID("_Tint"), Reveal = Shader.PropertyToID("_Reveal");
        static readonly Color Red = new Color(.95f, .32f, .2f), Bone = new Color(.92f, .88f, .74f);

        void Awake() { slots = new Slot[pool.Length]; for (int i = 0; i < pool.Length; i++) { slots[i] = new Slot { mpb = new MaterialPropertyBlock() }; pool[i].enabled = false; } }

        public void Clear() { if (slots == null) return; for (int i = 0; i < pool.Length; i++) { slots[i].k = 0; pool[i].enabled = false; } }

        public void Tick(float dt, Game g)
        {
            threats.Clear();
            var head = cam.position;
            if (g != null && g.state == "play")
            {
                foreach (var h in g.hands)
                {
                    if (h.phase == "recoil") continue;
                    var p = InputSource.ToU(h.phase == "omen" ? h.anchor : h.pos); p.y = Mathf.Max(p.y, .3f);
                    threats.Add((p, h.phase == "omen" ? .7f : 1, Red));
                }
                foreach (var f in g.scares.effigies)
                {
                    if (!f.placed) continue;
                    var p = new Vector3((float)f.x, 1.2f, (float)f.z); float d = Vector3.Distance(new Vector3(p.x, head.y, p.z), head);
                    if (d < 4.2f) threats.Add((p, Mathf.Clamp01(1.4f - d / 4.2f), Bone));
                }
                threats.Sort((a, b) => b.urgency.CompareTo(a.urgency));
            }
            int used = 0;
            foreach (var t in threats)
            {
                if (used >= pool.Length) break;
                var l = cam.InverseTransformDirection((t.pos - head).normalized);
                if (l.z > InView) continue;
                var s = slots[used]; var v = new Vector2(l.x, l.y);
                if (v.sqrMagnitude < 1e-4f) v = Vector2.right;
                float a = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                s.a = s.init && s.k > .05f ? Mathf.LerpAngle(s.a, a, 1 - Mathf.Exp(-dt * 10)) : a; s.init = true;
                s.k = Mathf.MoveTowards(s.k, 1, dt * 3);
                Apply(used, t.col, t.urgency);
                used++;
            }
            for (int i = used; i < pool.Length; i++)
            {
                slots[i].k = Mathf.MoveTowards(slots[i].k, 0, dt * 4);
                if (slots[i].k > 0) Apply(i, last[i], .6f); else pool[i].enabled = false;
            }
        }

        readonly Color[] last = new Color[4];
        void Apply(int i, Color col, float u)
        {
            var s = slots[i]; var r = pool[i]; r.enabled = true; last[i] = col;
            float rad = s.a * Mathf.Deg2Rad, pulse = .75f + .25f * Mathf.Sin(Time.time * 6);
            r.transform.localPosition = new Vector3(Mathf.Cos(rad) * Radius, Mathf.Sin(rad) * Radius * .8f, Depth);
            r.transform.localRotation = Quaternion.Euler(0, 0, s.a);
            var c = col; c.a = s.k * Mathf.Lerp(.55f, 1, u) * pulse;
            r.GetPropertyBlock(s.mpb); s.mpb.SetColor(Tint, c); s.mpb.SetFloat(Reveal, 1); r.SetPropertyBlock(s.mpb);
        }
    }
}
