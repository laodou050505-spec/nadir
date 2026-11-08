using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 裂隙里面：一走进传送门就到了这里（不再有黑暗包裹的过程），停一小会儿，再去下一层。
    /// 走进去那一刻就 Show（0.3 秒淡入，避免硬切），之后由 GameRoot 决定什么时候黑下去
    /// </summary>
    public class RiftRoom : MonoBehaviour
    {
        public MeshRenderer view;
        float t = -1; MaterialPropertyBlock mpb;
        static readonly int FadeId = Shader.PropertyToID("_Fade"), DirId = Shader.PropertyToID("_Dir");

        void Awake() { mpb = new MaterialPropertyBlock(); if (view) view.enabled = false; }

        /// <summary>ahead：走进去的方向，出口那团暖光就在这个方向偏下一点</summary>
        public void Show(Vector3 ahead)
        {
            ahead.y = 0; ahead = ahead.sqrMagnitude > 1e-4f ? ahead.normalized : Vector3.forward;
            mpb.SetVector(DirId, (ahead + Vector3.down * .08f).normalized);
            t = 0; Apply(0); view.enabled = true;
        }

        public void Hide() { t = -1; if (view) view.enabled = false; }

        void Apply(float k) { mpb.SetFloat(FadeId, k); view.SetPropertyBlock(mpb); }

        public void Tick(float dt)
        {
            if (t < 0) return;
            t += dt;
            float k = Mathf.Clamp01(t / .3f); Apply(k * k * (3 - 2 * k));      // 一进去就在里面：只用 0.3 秒避免硬切
        }
    }
}
