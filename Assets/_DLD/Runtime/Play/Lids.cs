using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 醒来时的眼皮：先黑一会儿，眼睛半睁又合上一次，再慢慢完全睁开。
    /// Play 是坠落后醒来（快，2.2 秒）；PlaySlow 是结局被阳光照醒（慢，5.4 秒，更柔）
    /// </summary>
    public class Lids : MonoBehaviour
    {
        public MeshRenderer view;
        public const float Duration = 2.2f;
        static readonly Vector2[] Fast = { new(0, 0), new(.3f, 0), new(.72f, .17f), new(.95f, .05f), new(1.42f, .42f), new(1.58f, .33f), new(Duration, 1) };
        static readonly Vector2[] Slow = { new(0, 0), new(1.0f, 0), new(1.9f, .16f), new(2.4f, .08f), new(3.7f, .5f), new(4.2f, .44f), new(5.4f, 1) };
        public static float SlowLength => Slow[Slow.Length - 1].x;
        static readonly int Open = Shader.PropertyToID("_Open");
        float t = -1; MaterialPropertyBlock mpb; Vector2[] keys = Fast;

        void Awake() { view.enabled = false; mpb = new MaterialPropertyBlock(); }
        public void Play() { keys = Fast; t = 0; Apply(0); }
        public void PlaySlow() { keys = Slow; t = 0; Apply(0); }
        public void Stop() { t = -1; view.enabled = false; }

        void Apply(float k) { view.enabled = true; mpb.SetFloat(Open, k); view.SetPropertyBlock(mpb); }

        public void Tick(float dt)
        {
            if (t < 0) return;
            t += dt;
            if (t >= keys[keys.Length - 1].x) { Stop(); return; }
            for (int i = 1; i < keys.Length; i++)
                if (t <= keys[i].x) { Apply(Mathf.SmoothStep(keys[i - 1].y, keys[i].y, (t - keys[i - 1].x) / (keys[i].x - keys[i - 1].x))); break; }
        }
    }
}
