using System.Collections.Generic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 手。全场景模式看不到真手，显示 PICO SDK 的手模型；透视模式不显示任何虚拟手，
    /// 同一个模型换成抠洞材质，让真手不被虚拟地砖挡住。编辑器里没有手部追踪，不创建
    /// </summary>
    public class PlayerHands : MonoBehaviour
    {
        public Transform rig; public Material skin, hole;
        readonly List<(SkinnedMeshRenderer r, Material normal)> parts = new();
        bool mr; float dim;
        static readonly int HandAlpha = Shader.PropertyToID("_DLDHandAlpha");

        void Start()
        {
            if (Application.isEditor) return;
            foreach (var n in new[] { "Prefabs/HandLeft", "Prefabs/HandRight" })
            {
                var pf = Resources.Load<GameObject>(n);
                if (!pf) { Debug.LogWarning("找不到 PICO 手模型 " + n); continue; }
                var g = Instantiate(pf, rig);
                // SDK 手模型自带一根射线指示器（手前面那个钉子一样的东西），这个游戏用不到
                foreach (var t in g.GetComponentsInChildren<Transform>(true)) if (t.name == "DefaultRay") { foreach (var dr in t.GetComponentsInChildren<Renderer>(true)) dr.enabled = false; t.gameObject.SetActive(false); }
                foreach (var r in g.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { if (!r.enabled) continue; if (skin) r.sharedMaterial = skin; parts.Add((r, r.sharedMaterial)); }
            }
            SetPassthrough(mr, dim);
        }

        /// <summary>切换显示方式。dimAlpha 是压暗层的不透明度，普通透视为 0</summary>
        public void SetPassthrough(bool passthrough, float dimAlpha)
        {
            mr = passthrough; dim = dimAlpha;
            Shader.SetGlobalFloat(HandAlpha, dimAlpha);
            foreach (var (r, normal) in parts) if (r) r.sharedMaterial = passthrough && hole ? hole : normal;
        }
    }
}
