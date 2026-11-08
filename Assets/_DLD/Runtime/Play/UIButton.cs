using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 一张夹在观片灯上的胶片：用手指点。手指靠近时灯管闪两下亮起来，按下时胶片跟着往里陷，松开弹回。选中的被红蜡笔圈出来。
    /// 判定只看指尖和按钮平面的关系，和视线无关
    /// </summary>
    public class UIButton : MonoBehaviour
    {
        public Vector2 size = new Vector2(.1f, .1f);
        public UIItem[] parts; public UIItem back, mark; public int seed;
        public System.Action onPress;
        public bool active = true, lit;
        float hover, press, litK, push;
        static readonly Color Dim = new Color(.74f, .71f, .6f, 1), Bone = new Color(.98f, .94f, .78f, 1), Cold = new Color(.1f, .12f, .12f, 0);
        Vector3 scale0; bool init;

        public bool Live => gameObject.activeInHierarchy && parts.Length > 0 && parts[0].Shown;

        /// <summary>指尖到按钮平面的距离（米，正数在玩家这一侧）。inside：是否落在按钮范围内，四周放宽一点</summary>
        public float Depth(Vector3 tip, out bool inside)
        {
            const float margin = .012f;
            float sc = transform.parent.lossyScale.x; var o = tip - transform.position;
            inside = Mathf.Abs(Vector3.Dot(o, transform.right)) < size.x * sc / 2 + margin && Mathf.Abs(Vector3.Dot(o, transform.up)) < size.y * sc / 2 + margin;
            return Vector3.Dot(o, -transform.forward);
        }

        /// <summary>编辑器调试用：视线和按钮最近一点的夹角</summary>
        public float Angle(Ray ray)
        {
            var pl = new Plane(transform.forward, transform.position);
            if (!pl.Raycast(ray, out float d) || d > 6) return 180;
            var o = ray.GetPoint(d) - transform.position; float sc = transform.parent.lossyScale.x;
            float x = Mathf.Clamp(Vector3.Dot(o, transform.right), -size.x * sc / 2, size.x * sc / 2), y = Mathf.Clamp(Vector3.Dot(o, transform.up), -size.y * sc / 2, size.y * sc / 2);
            return Vector3.Angle(ray.direction, transform.position + transform.right * x + transform.up * y - ray.origin);
        }

        public void Press() { press = 1; onPress?.Invoke(); }

        /// <param name="near">手指靠近的程度 0..1</param>
        /// <param name="sink">手指压进平面多深（米，已按父级缩放换算）</param>
        public void Tick(float dt, float near, float sink)
        {
            if (!init) { init = true; scale0 = transform.localScale; }
            hover = Mathf.MoveTowards(hover, near, dt * 9);
            press = Mathf.MoveTowards(press, 0, dt * 2.6f);
            litK = Mathf.MoveTowards(litK, lit ? 1 : 0, dt * 5);
            push = Mathf.Lerp(push, sink, 1 - Mathf.Exp(-dt * 22));
            float h = hover * hover * (3 - 2 * hover);
            transform.localScale = scale0 * (1 + h * .025f);
            var c = Color.Lerp(Dim, Bone, Mathf.Max(litK, h));
            if (!active) c.a = .3f + h * .12f;
            var g = active ? Cold * h * 1.6f : Color.clear;
            foreach (var p in parts) { p.tint = c; p.glow = g; p.offset = new Vector3(0, 0, push); }
            if (back)
            {
                // 老灯管：亮起来的那一下不稳，亮稳了就不再动
                float rise = h * (1 - h) * 4, flick = 1 - rise * .55f * Mathf.Clamp01(Mathf.Sin(Time.time * 53 + seed) * Mathf.Sin(Time.time * 31 + seed * 2) * 3);
                float lamp = (active ? Mathf.Lerp(.52f, 1.22f, Mathf.Max(h, litK * .62f)) : .3f) * flick + press * .8f;
                back.tint = new Color(lamp, lamp, lamp, 1); back.offset = new Vector3(0, 0, push);
            }
            if (mark) mark.offset = new Vector3(0, 0, push);
        }
    }
}
