using System.Collections.Generic;
using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>无脸黑影：完全跟随规则层的位置。盯久了慢慢歪头；没有任何动画，看着时一动不动</summary>
    public class FigureField : MonoBehaviour
    {
        public GameObject prefab;
        readonly List<Transform> pool = new();
        readonly List<Rect> blocks = new();

        public void Bind(List<Effigy> list)
        {
            while (pool.Count < list.Count) pool.Add(Instantiate(prefab, transform).transform);
            for (int i = 0; i < pool.Count; i++) pool[i].gameObject.SetActive(i < list.Count);
            blocks.Clear(); CollectBlocks();
        }

        /// <summary>大厅道具的占地（外扩一个身位）：规则层只知道半径，黑影不能穿过药柜和担架</summary>
        void CollectBlocks()
        {
            foreach (var l in FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude))
            {
                if (l.transform.IsChildOf(transform)) continue;
                var rs = l.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                blocks.Add(Rect.MinMaxRect(b.min.x - .32f, b.min.z - .32f, b.max.x + .32f, b.max.z + .32f));
            }
        }

        Vector3 Clear(Vector3 p)
        {
            // 推得再远也不进墙：只有走廊口的方向可以退进去
            const float wall = 6.15f, mouth = 1.3f;
            if (Mathf.Abs(p.z) > mouth) p.x = Mathf.Clamp(p.x, -wall, wall);
            if (Mathf.Abs(p.x) > mouth) p.z = Mathf.Clamp(p.z, -wall, wall);
            foreach (var r in blocks)
                if (r.Contains(new Vector2(p.x, p.z)))
                {
                    // 推到最近的边，优先往大厅中央：不贴墙
                    float dl = p.x - r.xMin, dr = r.xMax - p.x, db = p.z - r.yMin, dt = r.yMax - p.z;
                    float m = Mathf.Min(dl, dr, db, dt);
                    if (m == dl) p.x = r.xMin; else if (m == dr) p.x = r.xMax; else if (m == db) p.z = r.yMin; else p.z = r.yMax;
                }
            return p;
        }

        public void Tick(List<Effigy> list, Vector3 head)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var f = list[i]; pool[i].gameObject.SetActive(f.placed); if (!f.placed) continue;
                var p = Clear(new Vector3((float)f.x, 0, (float)f.z));
                var to = head - p; to.y = 0;
                pool[i].SetPositionAndRotation(p, Quaternion.LookRotation(to.sqrMagnitude > .01f ? to.normalized : Vector3.forward) * Quaternion.Euler(-(float)f.pushV * 7f, 0, 0) * Quaternion.Euler(0, 0, (float)f.tilt * Mathf.Rad2Deg * .3f * (f.variant == 0 ? 1 : -1)));
            }
        }
    }
}
