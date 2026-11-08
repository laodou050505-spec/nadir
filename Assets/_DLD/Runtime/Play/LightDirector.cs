using System.Collections.Generic;
using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 灯光：层越深越暗；熄灯有预兆（灯先慢慢沉下去）；眼睛预警时全场微微压暗。
    /// 另外隔一阵会有一盏灯管接触不良：只有那一盏在闪，灯管和它照亮的那一片一起暗，其他灯照常亮
    /// </summary>
    public class LightDirector : MonoBehaviour
    {
        public SceneLook look; public AudioDirector sound; public Renderer dim;
        float cur = 1;

        class Tube { public Renderer r; public int slot; public Color on; public Renderer haze; public float hazePower; public Vector3 pos; }
        List<Tube> tubes; Tube active; Transform cam;
        readonly List<(float level, float dur)> pattern = new();
        int step; float stepT, next = -1, shown = 1;
        MaterialPropertyBlock mpb;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor"), Power = Shader.PropertyToID("_Power"), Flick = Shader.PropertyToID("_DLDFlick");

        /// <summary>找出所有亮着的灯管，以及挂在它下面的光雾</summary>
        void Find()
        {
            tubes = new List<Tube>(); mpb = new MaterialPropertyBlock();
            var hazes = new List<Renderer>();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i] && ms[i].name == "K_tubeOn") { tubes.Add(new Tube { r = r, slot = i, on = ms[i].GetColor(Emission), pos = r.bounds.center }); break; }
                if (r.sharedMaterial && r.sharedMaterial.name == "K_haze") hazes.Add(r);
            }
            foreach (var t in tubes)
            {
                float best = 1.2f;
                foreach (var h in hazes) { float d = Vector3.Distance(h.transform.position, t.pos); if (d < best) { best = d; t.haze = h; } }
                if (t.haze) t.hazePower = t.haze.sharedMaterial.GetFloat(Power);
            }
        }

        /// <summary>一次接触不良：灭、挣扎着打着、又灭……每段的长短都不一样，最后稳住</summary>
        void Plan()
        {
            pattern.Clear();
            int n = Random.Range(3, 7);
            for (int i = 0; i < n; i++)
            {
                pattern.Add((Random.Range(0f, .05f), Random.Range(.05f, .13f)));      // 灭：只剩灯丝一点暗红
                pattern.Add((Random.Range(.7f, 1.08f), Random.Range(.07f, .42f)));    // 又打着了，亮度不稳
            }
            if (Random.value < .45f) pattern.Insert(pattern.Count - 1, (0, Random.Range(.5f, 1.1f)));     // 有时彻底灭一下，再亮回来
            pattern.Add((1, 0));
            step = 0; stepT = pattern[0].dur;
        }

        /// <summary>优先挑玩家看得到、离得近的那盏</summary>
        Tube Pick(Vector3 head, Vector3 fwd)
        {
            Tube best = null; float bs = -9; fwd.y = 0; fwd.Normalize();
            foreach (var t in tubes)
            {
                if (!t.r || !t.r.gameObject.activeInHierarchy) continue;
                var to = t.pos - head; float d = to.magnitude; if (d > 11) continue;
                to.y = 0;
                float s = Vector3.Dot(to.normalized, fwd) - d * .05f + Random.value * .4f;
                if (s > bs) { bs = s; best = t; }
            }
            return best;
        }

        void Set(Tube t, float k)
        {
            if (!t.r) return;
            var c = t.on * k; if (k < .1f) c += new Color(.06f, .02f, .01f) * (1 - k * 10);     // 灭了以后两头的灯丝还有一点暗红
            mpb.Clear(); mpb.SetColor(Emission, c); t.r.SetPropertyBlock(mpb, t.slot);
            if (t.haze) { mpb.Clear(); mpb.SetFloat(Power, t.hazePower * k); t.haze.SetPropertyBlock(mpb); }
        }

        /// <summary>换层、回标题时把正在闪的灯恢复</summary>
        public void StopFlicker()
        {
            if (active != null) Set(active, 1);
            active = null; shown = 1; Shader.SetGlobalVector(Flick, Vector4.zero);
        }

        void TickFlicker(float dt, Game g, float time)
        {
            if (tubes == null) Find();
            if (next < 0) next = time + Random.Range(6f, 12f);
            if (active == null && time >= next && g.scares.black <= 0 && g.state == "play")
            {
                if (!cam && Camera.main) cam = Camera.main.transform;
                active = cam ? Pick(cam.position, cam.forward) : null;
                if (active != null) { Plan(); sound?.Play("lamp_tick", active.pos, .6f, .12f, 1, 0, 0, 2.5f); }
                next = time + Random.Range(14f, 30f);      // 时不时来一下，不频繁
            }
            if (active == null) return;
            stepT -= dt;
            if (stepT <= 0)
            {
                float before = pattern[step].level; step++;
                if (step >= pattern.Count) { StopFlicker(); return; }
                stepT = pattern[step].dur;
                if (pattern[step].level > .5f && before < .3f && Random.value < .7f) sound?.Play("lamp_tick", active.pos, .35f, .2f, 1, 0, 0, 2.5f);
            }
            // 灭是一下子的，打着时有一点启辉的延迟
            float want = pattern[step].level; shown = want < shown ? want : Mathf.MoveTowards(shown, want, dt * 22);
            Set(active, shown);
            Shader.SetGlobalVector(Flick, new Vector4(active.pos.x, active.pos.y, active.pos.z, (1 - shown) * .8f));
        }

        public void Tick(float dt, Game g, float time, float externalFade)
        {
            float baseLevel = Mathf.Lerp(1f, .5f, (float)g.L.decay) * (g.mood == "horror" ? .85f : 1f);
            var sc = g.scares;
            float target = baseLevel * Mathf.Lerp(1, .52f, EyeView.Dread);      // 巨眼睁开时整个大厅暗下来
            if (sc.black > 0) target = .035f;
            else if (sc.pending != null && sc.pending.kind == "blackout") target = baseLevel * Mathf.Lerp(.45f, 1f, (float)(sc.pending.t / sc.pending.dur)) * (1 + Mathf.Sin(time * 11) * .05f);
            if (g.eye.phase == "open") target *= .78f; else if (g.eye.phase == "warn") target *= .9f;
            cur = Mathf.Lerp(cur, target, 1 - Mathf.Exp(-dt * (sc.black > 0 ? 3 : 1.6f)));
            look.level = cur;
            look.fogDensity = Mathf.Lerp(.05f, .09f, (float)g.L.decay);
            if (sc.black > 0) StopFlicker(); else TickFlicker(dt, g, time);
        }
    }
}
