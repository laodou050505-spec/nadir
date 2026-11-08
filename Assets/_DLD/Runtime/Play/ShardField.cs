using System.Collections.Generic;
using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>碎片（发光安瓿瓶）的表现：浮现、缓慢转动、快消散时闪、被盯住时变亮</summary>
    public class ShardField : MonoBehaviour
    {
        public GameObject prefab;
        readonly Dictionary<Shard, (Transform t, float born)> live = new();
        readonly List<Shard> gone = new();

        public void Clear() { foreach (var v in live.Values) Destroy(v.t.gameObject); live.Clear(); }
        public void Spawn(Shard s, float time) { var g = Instantiate(prefab, transform); live[s] = (g.transform, time); }
        public void Remove(Shard s) { if (live.TryGetValue(s, out var v)) { Destroy(v.t.gameObject); live.Remove(s); } }

        public void Tick(float time)
        {
            foreach (var kv in live)
            {
                var s = kv.Key; var t = kv.Value.t;
                float age = time - kv.Value.born;
                float scale = Mathf.SmoothStep(0, 1, age / .6f) * (s.locked ? 1.3f : 1f);
                if (s.life < Cfg.Shard.Flicker) scale *= .75f + .25f * Mathf.Sin(age * 16);
                t.localScale = Vector3.one * scale;
                var p = InputSource.ToU(s.pos); p.y += Mathf.Sin(age * 1.4f + s.id) * .012f;
                t.position = p;
                t.rotation = Quaternion.Euler(Mathf.Sin(age * .7f + s.id) * 10, age * 26, Mathf.Cos(age * .5f) * 12);
            }
        }
    }
}
