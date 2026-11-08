using UnityEngine;

namespace DLD.Play
{
    /// <summary>淡入淡出：头上套一个向内的球。VR 里不做屏幕后效，球最省</summary>
    public class ScreenFade : MonoBehaviour
    {
        public MeshRenderer view;
        Material mat;
        float alpha = 1, target = 1, speed = 1; Color col = Color.black;
        static readonly int C = Shader.PropertyToID("_Color");

        void Awake() { mat = view.material; }

        public bool Busy => !Mathf.Approximately(alpha, target);
        public void To(float a, float seconds, Color? color = null) { target = a; speed = 1 / Mathf.Max(seconds, .01f); if (color.HasValue) col = color.Value; }
        public void Tick(float dt)
        {
            alpha = Mathf.MoveTowards(alpha, target, speed * dt);
            view.enabled = alpha > .001f;
            col.a = alpha; mat.SetColor(C, col);
        }
    }
}
