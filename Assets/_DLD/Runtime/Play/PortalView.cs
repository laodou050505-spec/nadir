using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>出口：碎片集齐后，最远那块砖上方裂开一条细缝，玩家用双手把它撕开。全场唯一的暖光</summary>
    public class PortalView : MonoBehaviour
    {
        public Renderer gate; public Transform halo, pool; public ParticleSystem embers; public SceneLook look; public Transform[] chips;
        Material mat; Tile tile; float shown;
        static readonly int Open = Shader.PropertyToID("_Open"), Yaw = Shader.PropertyToID("_Yaw");

        void Awake() { mat = gate.material; }

        // 被撕下来的空间碎片：悬在裂口两边，慢慢被吸回去。这些是真的立体块，走到侧面也看得到
        void Chips(float open, float time)
        {
            for (int i = 0; i < chips.Length; i++)
            {
                float s = i * 1.618f, life = Mathf.Repeat(time * (.06f + (i % 5) * .012f) + s, 1);
                float side = i % 2 == 0 ? 1 : -1, y = (Mathf.Repeat(s * 3.7f, 1) - .5f) * 2.1f, yy = Mathf.Clamp01(1 - y * y / 1.3f);
                float gap = (.1f + .62f * open * yy) * .75f, x = side * (gap + (1 - life) * (.22f + Mathf.Repeat(s * 2.3f, 1) * .36f));
                float z = (Mathf.Repeat(s * 5.1f, 1) - .5f) * .55f * (1 - life * .7f);
                var c = chips[i]; c.localPosition = new Vector3(x, 1.22f + y + Mathf.Sin(time * .7f + s) * .025f, z);
                c.localRotation = Quaternion.Euler(s * 40 + time * 22, s * 77 + time * 15, s * 13);
                float k = Mathf.Clamp01(life * 6) * Mathf.Clamp01((1 - life) * 3) * Mathf.Clamp01(open * 3 + .15f);
                c.localScale = new Vector3(1, .75f + Mathf.Repeat(s * 3.1f, 1) * .9f, .28f + Mathf.Repeat(s * 4.3f, 1) * .4f) * (.016f + Mathf.Repeat(s * 7.3f, 1) * .04f) * k;     // 压扁的碎块，更像撕下来的薄片
            }
        }

        public void Hide()
        {
            tile = null; gameObject.SetActive(false);
            if (look) look.lampPowerB = 0;
        }

        public void Show(Tile t, Vector3 head)
        {
            tile = t; gameObject.SetActive(true); shown = 0;
            var p = new Vector3((float)t.x, 0, (float)t.z); var to = head - p; to.y = 0;
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(to.normalized));
            embers.Clear(); embers.Play();
        }

        public void Tick(Game g, Vector3 head, float time)
        {
            if (tile == null || g.door == null) return;
            shown = Mathf.Lerp(shown, (float)g.door.open, 1 - Mathf.Exp(-Time.deltaTime * (g.door.ready ? 2.6f : 9)));
            float open = shown;       // 拉到一半后余下的部分平滑裂开，不跳
            mat.SetFloat(Open, open);
            Chips(open, time);
            // 全景里长廊的尽头对准裂隙背后
            var back = -transform.forward; mat.SetFloat(Yaw, -Mathf.Atan2(back.x, back.z) / (2 * Mathf.PI));
            halo.localScale = Vector3.one * Mathf.Lerp(.25f, 1, open);
            pool.localScale = new Vector3(4.2f, 4.2f, 1) * Mathf.Lerp(.3f, 1, open);
            look.lampB = transform.position + Vector3.up * 1.2f + transform.forward * .3f;
            look.lampColorB = new Color(1f, .58f, .24f);
            look.lampPowerB = open * .9f;
        }
    }
}
