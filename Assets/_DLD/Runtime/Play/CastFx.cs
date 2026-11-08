using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 施法：伸手时掌心聚起冷光，黑影被一串气浪打退，身上崩出灰烬，地面荡开一圈。拍开触手是同一种力的小版本。
    /// 判定全在规则层，这里只管好看
    /// </summary>
    public class CastFx : MonoBehaviour
    {
        public MeshRenderer palm; public MeshRenderer[] pool; public ParticleSystem sparks, ash, dust;
        class Wave { public MeshRenderer r; public float t = -1, dur, delay, size, r0, r1, ring, glow; public Vector3 from, to; public Quaternion rot; }
        Wave[] waves; MaterialPropertyBlock mpb; float charge; Vector3 lastPalm;
        static readonly int R = Shader.PropertyToID("_R"), RingK = Shader.PropertyToID("_RingK"), GlowK = Shader.PropertyToID("_GlowK");

        void Awake()
        {
            mpb = new MaterialPropertyBlock(); waves = new Wave[pool.Length];
            for (int i = 0; i < pool.Length; i++) { waves[i] = new Wave { r = pool[i] }; pool[i].enabled = false; }
            palm.enabled = false;
        }

        void Fire(Vector3 from, Vector3 to, Quaternion rot, float delay, float dur, float r0, float r1, float ring, float glow)
        {
            foreach (var w in waves)
            {
                if (w.t >= 0) continue;
                w.t = 0; w.delay = delay; w.dur = dur; w.from = from; w.to = to; w.rot = rot; w.size = r1; w.r0 = r0 / r1; w.r1 = 1; w.ring = ring; w.glow = glow;
                w.r.transform.localScale = new Vector3(r1 * 2.2f, r1 * 2.2f, 1);
                return;
            }
        }

        /// <summary>打中黑影：空气里只有两道淡淡的波纹，到了之后地面的灰被掀开，黑影身上的灰烬往后飞</summary>
        public void Cast(Vector3 from, Vector3 target)
        {
            var dir = target - from; if (dir.sqrMagnitude < .01f) dir = Vector3.forward;
            var q = Quaternion.LookRotation(dir.normalized);
            float fly = Mathf.Clamp(dir.magnitude * .055f, .12f, .32f);
            for (int i = 0; i < 2; i++) Fire(from, target, q, i * .07f, fly + .1f, .1f, .5f + i * .12f, .34f - i * .1f, 0);
            Fire(target, target, q, fly * .85f, .22f, .1f, .55f, 0, .22f);
            burstLow = -1f; burstHigh = .55f; burstK = 1;
            Burst(target, dir.normalized, fly * .85f);
        }

        /// <summary>拍开触手：和击退黑影同一种力，只是更短更近：一道波纹、地上的灰被掀开、触手上崩出灰烬和火星</summary>
        public void Swat(Vector3 pos, Vector3 head, bool killed)
        {
            var dir = pos - head; dir.y = 0; dir = dir.sqrMagnitude > .01f ? dir.normalized : Vector3.forward;
            var q = Quaternion.LookRotation((pos - head).normalized);
            Fire(pos, pos + dir * .25f, q, 0, .3f, .05f, killed ? .62f : .42f, .34f, .2f);
            Fire(pos, pos + dir * .5f, q, .05f, .34f, .08f, killed ? .85f : .55f, .22f, 0);
            burstLow = -.3f; burstHigh = .25f; burstK = killed ? .8f : .5f;
            Burst(pos, dir, 0);
        }

        void Burst(Vector3 at, Vector3 dir, float delay)
        {
            burstAt = at; burstDir = dir; burstT = delay;
        }
        Vector3 burstAt, burstDir; float burstT = -1, burstLow = -1, burstHigh = .55f, burstK = 1;

        void EmitBurst()
        {
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < 46 * burstK; i++)
            {
                ep.position = burstAt + new Vector3(Random.Range(-.22f, .22f), Random.Range(burstLow, burstHigh), Random.Range(-.22f, .22f));
                ep.velocity = (burstDir * Random.Range(.8f, 3.1f) + Random.insideUnitSphere * .55f + Vector3.up * Random.Range(0, .35f));
                ep.startLifetime = Random.Range(.5f, 1.5f); ep.startSize = Random.Range(.02f, .07f); ep.rotation = Random.Range(0, 360f);
                ash.Emit(ep, 1);
            }
            // 地面的灰被一圈推开：这才是冲击波看得见的样子
            float a0 = Random.value * 6.28f;
            int ringN = (int)(34 * burstK);
            for (int i = 0; i < ringN; i++)
            {
                float a = a0 + (float)i / ringN * 6.28f + Random.Range(-.1f, .1f); var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                ep.position = new Vector3(burstAt.x, .06f, burstAt.z) + d * Random.Range(.12f, .3f);
                ep.velocity = d * Random.Range(1.1f, 2.6f) + Vector3.up * Random.Range(.03f, .22f) + burstDir * .35f;
                ep.startLifetime = Random.Range(.7f, 1.5f); ep.startSize = Random.Range(.22f, .62f); ep.rotation = Random.Range(0, 360f);
                dust.Emit(ep, 1);
            }
            for (int i = 0; i < 14 + (burstK < 1 ? 12 : 0); i++)
            {
                ep.position = burstAt + new Vector3(Random.Range(-.25f, .25f), Random.Range(burstLow, burstHigh), Random.Range(-.25f, .25f));
                ep.velocity = burstDir * Random.Range(.6f, 2.2f) + Random.insideUnitSphere * .9f;
                ep.startLifetime = Random.Range(.3f, .85f); ep.startSize = Random.Range(.007f, .018f);
                sparks.Emit(ep, 1);
            }
        }

        public void Tick(float dt, Vector3? castFrom, Vector3 head)
        {
            charge = Mathf.MoveTowards(charge, castFrom.HasValue ? 1 : 0, dt * (castFrom.HasValue ? 6 : 3.5f));
            palm.enabled = charge > .01f;
            if (castFrom.HasValue) lastPalm = castFrom.Value;
            if (palm.enabled)
            {
                var t = palm.transform;
                t.SetPositionAndRotation(lastPalm, Quaternion.LookRotation((lastPalm - head).normalized));
                t.localScale = Vector3.one * (.2f + .02f * Mathf.Sin(Time.time * 13));
                mpb.Clear(); mpb.SetFloat(GlowK, .3f * charge); mpb.SetFloat(RingK, .12f * charge); mpb.SetFloat(R, .42f + .05f * Mathf.Sin(Time.time * 9)); palm.SetPropertyBlock(mpb);
            }
            if (burstT >= 0) { burstT -= dt; if (burstT < 0) EmitBurst(); }
            foreach (var w in waves)
            {
                if (w.t < 0) continue;
                w.t += dt;
                float k = (w.t - w.delay) / w.dur;
                if (k >= 1) { w.t = -1; w.r.enabled = false; continue; }
                if (k < 0) continue;
                float e = 1 - (1 - k) * (1 - k);
                w.r.enabled = true;
                w.r.transform.SetPositionAndRotation(Vector3.Lerp(w.from, w.to, e), w.rot);
                float fade = (1 - k) * (1 - k) * Mathf.Clamp01(k * 9);
                mpb.Clear(); mpb.SetFloat(R, Mathf.Lerp(w.r0, .86f, e)); mpb.SetFloat(RingK, w.ring * fade); mpb.SetFloat(GlowK, w.glow * fade);
                w.r.SetPropertyBlock(mpb);
            }
        }
    }
}
