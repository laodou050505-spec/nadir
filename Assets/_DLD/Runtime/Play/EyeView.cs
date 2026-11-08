using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>井里的眼：一颗完整的眼球，比竖井略小，永远不碰墙。预警时从很深处升上来，睁开时停在井口下面，一直转着看玩家</summary>
    public class EyeView : MonoBehaviour
    {
        public Transform eye; public SceneLook look;
        Material mat;
        float y = -70, red, pupil = 1, sacT = 2, blinkT = 5, blink; Vector2 sac, sacTo;
        public static float Dread;      // 0..1：灯光跟着它压暗
        public const float Radius = 2.08f;      // 井壁横梁内缘在 2.2 米
        public bool Visible => eye && eye.gameObject.activeSelf && y > -40;
        public float Bottom => eye && eye.gameObject.activeSelf ? -24 : -999;     // 从预警开始，深处的触手就要让出位置
        static readonly int Open = Shader.PropertyToID("_Open"), Pupil = Shader.PropertyToID("_Pupil");

        void Awake() { mat = eye.GetComponent<Renderer>().material; }

        /// <summary>被拍了一下：猛地往井里缩，瞳孔收紧，还歪一下</summary>
        public void Slap() { slap = 1; eye.rotation *= Quaternion.Euler(Random.Range(-16f, 16f), Random.Range(-22f, 22f), 0); }
        float slap;

        public void Clear() { y = -70; red = 0; Dread = 0; if (eye) eye.gameObject.SetActive(false); look.shaftRed = 0; look.eyeY = -120; }

        public void Tick(float dt, Game g, Vector3 head)
        {
            var E = g.eye;
            if (g.L.eye == null) { Clear(); return; }
            slap = Mathf.MoveTowards(slap, 0, dt / 1.6f);
            float targetY = slap > 0 ? -26 : E.phase == "open" ? (E.seeing ? -1.5f : -2.3f) : E.phase == "warn" ? -8 : E.phase == "closing" ? -14 : -70;   // 球顶离地面三米多，透过破洞看到的是一颗完整的眼球
            y = Mathf.Lerp(y, targetY, 1 - Mathf.Exp(-dt * (slap > 0 ? 3.2f : g.state == "falling" ? 3.5f : E.phase == "closed" ? 1.2f : E.phase == "closing" ? .55f : E.phase == "open" ? 1.1f : .9f)));   // 玩家掉下来时不能撞进眼球
            bool vis = y > -68;
            if (eye.gameObject.activeSelf != vis) eye.gameObject.SetActive(vis);
            eye.position = new Vector3(0, y - Radius, 0);        // 永远在井的正中，左右没有余地
            // 活的眼睛不会死盯：大部分时间盯着玩家，隔几秒飞快地扫一下；一直有极细的颤动；偶尔眨一下
            sacT -= dt; if (sacT <= 0) { sacT = Random.Range(1.6f, 4.6f); sacTo = Random.insideUnitCircle * 6.5f; }
            sac = Vector2.Lerp(sac, sacT > .35f ? Vector2.zero : sacTo, 1 - Mathf.Exp(-dt * 26));
            var tr = new Vector2(Mathf.PerlinNoise(Time.time * 2.1f, 3) - .5f, Mathf.PerlinNoise(9, Time.time * 2.3f) - .5f) * 1.1f;
            var want = Quaternion.LookRotation((head - eye.position).normalized, Vector3.forward) * Quaternion.Euler(sac.y + tr.y, sac.x + tr.x, 0);
            eye.rotation = Quaternion.Slerp(eye.rotation, want, 1 - Mathf.Exp(-dt * 22));
            blinkT -= dt; if (blinkT <= 0 && E.phase == "open") { blinkT = Random.Range(4f, 8.5f); blink = 1; }
            blink = Mathf.MoveTowards(blink, 0, dt / .26f); float bk = 1 - Mathf.Sin(blink * Mathf.PI) * .95f;
            pupil = Mathf.Lerp(pupil, (slap > 0 ? .8f : E.seeing ? 1.5f : 1.04f) + Mathf.Sin(Time.time * .8f) * .03f, 1 - Mathf.Exp(-dt * 4));     // 瞳孔一直在微微呼吸
            mat.SetFloat(Open, Mathf.Clamp01((float)E.open * 1.1f) * bk);
            mat.SetFloat(Pupil, pupil);
            float redTarget = E.phase == "open" ? (E.seeing ? .62f : .46f) : E.phase == "warn" ? (float)(E.t / E.dur) * .25f : 0;
            red = Mathf.Lerp(red, redTarget, 1 - Mathf.Exp(-dt * 2));
            look.shaftRed = red; look.eyeY = y; Dread = Mathf.Lerp(Dread, E.phase == "open" ? 1 : E.phase == "warn" ? (float)(E.t / E.dur) * .6f : 0, 1 - Mathf.Exp(-dt * 1.4f));
            // 砖缝透出红光：这是眼睛要睁开的预警
            Shader.SetGlobalColor("_DLDSeep", new Color(.9f, .1f, .03f) * red * .45f);
        }
    }
}
