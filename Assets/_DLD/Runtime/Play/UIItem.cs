using UnityEngine;

namespace DLD.Play
{
    /// <summary>界面上的一个可见元素：入场时从模糊里聚焦、轻轻浮起，退场时溶回去。所有动画都走这里</summary>
    public class UIItem : MonoBehaviour
    {
        public MeshRenderer r; public Vector3 home; public float delay, dur = .9f; public int sweep; public float shadow = .6f; public bool rgb;
        [HideInInspector] public Color tint = new Color(.86f, .84f, .78f, 1), glow = Color.clear;
        [HideInInspector] public float revealMul = 1; [HideInInspector] public Vector3 offset;
        float k, wait; bool on; MaterialPropertyBlock mpb;
        static readonly int Reveal = Shader.PropertyToID("_Reveal"), Tint = Shader.PropertyToID("_Tint"), Glow = Shader.PropertyToID("_Glow"), Sweep = Shader.PropertyToID("_Sweep"), Shadow = Shader.PropertyToID("_Shadow"), UseRgb = Shader.PropertyToID("_UseRgb");

        public bool Shown => on && k > .5f;
        public bool Gone => !on && k <= 0;
        public void Show(bool v, float extra = 0) { if (v == on) return; on = v; wait = v ? Mathf.Max(0, delay + extra) : 0; }
        public void Snap() { k = on ? 1 : 0; }

        public void Tick(float dt)
        {
            if (wait > 0) wait -= dt;
            else k = Mathf.MoveTowards(k, on ? 1 : 0, dt / (on ? dur : .4f));
            float e = 1 - (1 - k) * (1 - k) * (1 - k);
            transform.localPosition = home + offset + new Vector3(0, -.014f, .035f) * (1 - e);
            r.enabled = k > .001f;
            if (!r.enabled) return;
            mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetFloat(Reveal, e * revealMul); mpb.SetColor(Tint, tint); mpb.SetColor(Glow, glow); mpb.SetFloat(Sweep, sweep); mpb.SetFloat(Shadow, shadow); mpb.SetFloat(UseRgb, rgb ? 1 : 0);
            r.SetPropertyBlock(mpb);
        }
    }
}
