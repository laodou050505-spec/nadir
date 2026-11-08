using UnityEngine;

namespace DLD.Play
{
    /// <summary>井里的触手：从黑暗深处伸上来，在井中间缓慢摇摆，不碰四壁，够不到洞口。越深的层越多，让人不想往下看</summary>
    public class DeepArms : MonoBehaviour
    {
        public Tentacle prefab;
        Tentacle[] items; float[] seed; int active, frame; float retreat;
        readonly Vector3[] ctrl = new Vector3[5];

        public void Bind(int layerIdx)
        {
            if (items == null)
            {
                items = new Tentacle[7]; seed = new float[7];
                for (int i = 0; i < 7; i++) { items[i] = Instantiate(prefab, transform); seed[i] = i * 2.399f; }
            }
            active = layerIdx < 0 ? 0 : Mathf.Min(items.Length, 1 + layerIdx);
            for (int i = 0; i < 7; i++) items[i].gameObject.SetActive(i < active);
        }

        public void Tick(float time, Vector3? avoid = null, float eyeBottom = -999)
        {
            if (items == null) return;
            frame++;
            retreat = Mathf.MoveTowards(retreat, eyeBottom > -900 ? 24 : 0, Time.deltaTime * 9);
            for (int i = 0; i < active; i++)
            {
                float s = seed[i], ang = s;
                var wallDir = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang));
                float k = Mathf.Max(Mathf.Abs(wallDir.x), Mathf.Abs(wallDir.z)); wallDir /= k;   // 投到方形井壁上
                var root = wallDir * 1.05f + Vector3.down * 56;
                float reach = -4.5f - (i * 3.3f % 9) - (Mathf.Sin(time * .17f + s) * .5f + .5f) * 5;   // 最高也到不了井口
                reach = Mathf.Max(reach - retreat, -36);             // 巨眼出现时慢慢退到它下面
                var tip = wallDir * (.85f + Mathf.Sin(time * .23f + s * 2) * .25f) + Vector3.up * reach;
                tip += new Vector3(Mathf.Sin(time * .19f + s) * .4f, 0, Mathf.Cos(time * .16f + s) * .4f);
                ctrl[0] = root;
                ctrl[1] = wallDir * 1.1f + Vector3.up * Mathf.Min(-34, reach - 11);
                ctrl[2] = wallDir * 1.05f + Vector3.up * Mathf.Min(-20 - reach * .4f, reach - 7);
                ctrl[3] = wallDir * 1f + Vector3.up * (reach - 3.5f);
                ctrl[4] = tip;
                if ((frame + i) % 2 == 0) items[i].Shape(ctrl, 5, time * .55f, .22f, s, -1, 1, 1.7f, avoid);       // 深处的隔帧更新，看不出来，省一半
            }
        }
    }
}
