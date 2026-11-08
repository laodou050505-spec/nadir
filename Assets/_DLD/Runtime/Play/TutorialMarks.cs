using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>教学的地面标记：红圈是要塌的砖，白圈是该去的砖；一个上下浮动的箭头指着这一步要找的东西</summary>
    public class TutorialMarks : MonoBehaviour
    {
        public MeshRenderer danger, safe, arrow; public Transform cam;
        MaterialPropertyBlock mpb; float kD, kS, kA; Vector3 arrowPos;

        void Awake() { mpb = new MaterialPropertyBlock(); danger.enabled = safe.enabled = arrow.enabled = false; }      // 不能写在字段初始化里，Unity 不允许
        static readonly int Line = Shader.PropertyToID("_Line"), Half = Shader.PropertyToID("_Half"), Size = Shader.PropertyToID("_Size"), RevealL = Shader.PropertyToID("_Reveal"), Col = Shader.PropertyToID("_Color"), Tint = Shader.PropertyToID("_Tint"), RevealU = Shader.PropertyToID("_Reveal");

        void Ring(MeshRenderer r, Tile t, ref float k, Color c, float dt, float pulse)
        {
            k = Mathf.MoveTowards(k, t != null ? 1 : 0, dt * 2.5f); r.enabled = k > .001f; if (!r.enabled) return;
            if (t != null)
            {
                float s = (float)t.size; var tr = r.transform; tr.position = new Vector3((float)t.x, .018f, (float)t.z); tr.localScale = new Vector3(s * 1.3f, s * 1.3f, 1);
                mpb.Clear(); mpb.SetFloat(Half, s * .46f); mpb.SetFloat(Size, s * 1.3f); mpb.SetFloat(RevealL, k);  mpb.SetColor(Col, c * pulse); mpb.SetFloat(Line, .017f); r.SetPropertyBlock(mpb);
            }
        }

        public void Tick(float dt, Game g)
        {
            var tu = g != null && g.state == "play" ? g.tut : null;
            float time = Time.time;
            Ring(danger, tu?.Danger, ref kD, new Color(1.5f, .28f, .12f), dt, .72f + .28f * Mathf.Sin(time * 7));
            Ring(safe, tu?.Safe, ref kS, new Color(1.1f, 1.05f, .85f), dt, .8f + .2f * Mathf.Sin(time * 3.5f));
            var target = tu?.Target;
            kA = Mathf.MoveTowards(kA, target.HasValue ? 1 : 0, dt * 3); arrow.enabled = kA > .001f;
            if (!arrow.enabled) return;
            if (target.HasValue) arrowPos = InputSource.ToU(target.Value);
            var at = arrowPos + Vector3.up * (.24f + .05f * Mathf.Sin(time * 3.4f));
            var to = at - cam.position; to.y = 0;
            arrow.transform.SetPositionAndRotation(at, Quaternion.LookRotation(to.sqrMagnitude > .01f ? to.normalized : Vector3.forward) * Quaternion.Euler(0, 0, -90));
            mpb.Clear(); var cc = new Color(1, .93f, .72f, kA); mpb.SetColor(Tint, cc); mpb.SetFloat(RevealU, 1); arrow.SetPropertyBlock(mpb);
        }
    }
}
