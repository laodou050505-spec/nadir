using System.Collections.Generic;
using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>地板表现：裂纹爬开 → 裂成几块下陷 → 各自翻滚坠进竖井 → 新砖从黑暗里升起</summary>
    public class TileField : MonoBehaviour
    {
        public ParticleSystem dust; public GameObject rimTiny, rimSmall, rimMain; public Transform mask; public bool hospitalOn = true;
        float tile = .75f;
        SlabView[] views;

        class Piece { public Transform t; public MeshRenderer r; public Vector3 vel, spin; public float delay, rnd; public Vector2 c; }
        class State { public SlabView v; public Piece[] pieces; public TileState last = TileState.Solid; public float fallT; public MaterialPropertyBlock mpb = new(); public MaterialPropertyBlock pmpb = new(); public bool broken; public Vector3 home, home0; }
        State[] st;

        static readonly int CrackK = Shader.PropertyToID("_CrackK"), CrackCol = Shader.PropertyToID("_CrackCol"), CrackMap = Shader.PropertyToID("_CrackMap"),
            CrackRect = Shader.PropertyToID("_CrackRect"), Emission = Shader.PropertyToID("_EmissionColor"), Tint = Shader.PropertyToID("_BaseColor");

        static Color CauseColor(string cause) => cause switch
        {
            // 裂缝里只透一点点光：够看清，不做成霓虹线。不同原因只差色温
            "eye" => new Color(.3f, .05f, .02f), "grab" => new Color(.05f, .07f, .12f), "stay" => new Color(.26f, .13f, .05f), "figure" => new Color(.05f, .06f, .07f),
            _ => new Color(.035f, .05f, .05f),
        };
        static readonly Color SlabTint = new Color(.5f, .53f, .52f);

        void Awake()
        {
            views = GetComponentsInChildren<SlabView>(true);
            st = new State[views.Length];
            var rnd = new System.Random(5);
            for (int i = 0; i < views.Length; i++)
            {
                var v = views[i];
                var s = st[i] = new State { v = v, home = v.transform.localPosition, home0 = v.transform.localPosition };
                s.pieces = new Piece[v.pieces.Length];
                for (int k = 0; k < v.pieces.Length; k++)
                    s.pieces[k] = new Piece { t = v.pieces[k].transform, r = v.pieces[k], c = v.centers[k], rnd = (float)rnd.NextDouble() };
                s.mpb.SetTexture(CrackMap, v.crack);
                var p = v.transform.position; float size = 3.75f / 5;
                s.mpb.SetVector(CrackRect, new Vector4(p.x, p.z, 1f / size, 0));
                s.mpb.SetFloat(CrackK, 0); s.mpb.SetColor(CrackCol, Color.black); s.mpb.SetColor(Emission, Color.black);
            }
        }

        void Start()
        {
            // 烘焙数据要等场景加载后才在渲染器上，碎块跟整块用同一块光照贴图
            foreach (var s in st)
                foreach (var p in s.pieces) { p.r.lightmapIndex = s.v.whole.lightmapIndex; p.r.lightmapScaleOffset = s.v.whole.lightmapScaleOffset; }
        }


        State Of(Tile t) { var name = $"Slab{t.i}{t.j}"; foreach (var s in st) if (s.v.name == name) return s; return null; }
        readonly Dictionary<Tile, State> map = new();

        public void Bind(Floor floor)
        {
            SetArea((float)floor.s);
            map.Clear();
            foreach (var t in floor.tiles) { var s = Of(t); map[t] = s; Reset(s); }
        }

        /// <summary>地图大小由玩家选：地砖等比缩放，方格、井口遮罩和外圈固定地面都跟着对齐</summary>
        public void SetArea(float size)
        {
            tile = size; float k = size / .75f;
            foreach (var s in st)
            {
                s.home = s.home0 * k; s.v.transform.localScale = new Vector3(k, 1, k); s.v.transform.localPosition = s.home;
                var p = transform.TransformPoint(s.home);
                s.mpb.SetVector(CrackRect, new Vector4(p.x, p.z, 1f / size, 0)); s.v.whole.SetPropertyBlock(s.mpb);
            }
            Shader.SetGlobalFloat("_DLDTile", size);
            if (mask) mask.localScale = Vector3.one * size * 5;
            Rims();
        }

        /// <summary>透视模式没有大厅，外圈地面也一起藏掉</summary>
        public void Rims()
        {
            if (rimTiny) rimTiny.SetActive(hospitalOn && tile < .59f);
            if (rimSmall) rimSmall.SetActive(hospitalOn && tile < .74f);
            if (rimMain) rimMain.SetActive(hospitalOn && tile < .99f);
        }

        void Reset(State s)
        {
            s.last = TileState.Solid; s.broken = false; s.fallT = 0;
            s.v.transform.localPosition = s.home; s.v.whole.enabled = true;
            foreach (var p in s.pieces) p.t.gameObject.SetActive(false);
            s.mpb.SetFloat(CrackK, 0); s.mpb.SetColor(Emission, Color.black); s.v.whole.SetPropertyBlock(s.mpb);
        }

        void Break(State s)
        {
            if (s.broken) return;
            s.broken = true; s.v.whole.enabled = false;
            foreach (var p in s.pieces)
            {
                p.t.gameObject.SetActive(true); p.t.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                s.pmpb.SetColor(Tint, SlabTint); p.r.SetPropertyBlock(s.pmpb);
            }
        }

        public void Tick(float dt, Game g, float time)
        {
            var stayTile = g.L.chase && g.stay != null && g.stay.warned ? g.stay.tile : null;
            foreach (var kv in map)
            {
                var t = kv.Key; var s = kv.Value;
                if (t.state != s.last)
                {
                    if (t.state == TileState.Fall)
                    {
                        s.fallT = 0;
                        Break(s);
                        foreach (var p in s.pieces)
                        {
                            float l = Mathf.Max(p.c.magnitude, .01f); float r = p.rnd;
                            p.vel = new Vector3(p.c.x / l * (.1f + r * .25f), -.15f - r * .3f, p.c.y / l * (.1f + r * .25f));
                            p.spin = new Vector3((r - .5f) * 280, (.5f - r) * 170, ((r * 7) % 1 - .5f) * 280);
                            p.delay = r * .14f;
                        }
                        if (dust) { dust.transform.position = s.v.transform.position; dust.Emit(16); }
                    }
                    if (t.state == TileState.Crack) { s.mpb.SetColor(CrackCol, CauseColor(t.cause)); }
                    if (t.state == TileState.Rise || t.state == TileState.Solid)
                    {
                        s.broken = false; s.v.whole.enabled = true;
                        foreach (var p in s.pieces) p.t.gameObject.SetActive(false);
                        s.mpb.SetFloat(CrackK, 0);
                    }
                    s.last = t.state;
                }

                var pos = s.home;
                float crackK = 0;
                switch (t.state)
                {
                    case TileState.Crack:
                        {
                            crackK = Mathf.Min(1, (float)(t.t / t.dur));
                            float shake = crackK * crackK * .008f;
                            pos.x += Random.Range(-.5f, .5f) * shake; pos.z += Random.Range(-.5f, .5f) * shake;
                            if (crackK > .6f)
                            {
                                // 后半段：砖已经碎开，各块开始下陷、微微错位
                                Break(s);
                                float k = (crackK - .6f) / .4f;
                                foreach (var p in s.pieces)
                                {
                                    p.t.localPosition = new Vector3(p.c.x * .04f * k, -k * k * (.015f + p.rnd * .035f), p.c.y * .04f * k);
                                    p.t.localRotation = Quaternion.Euler((p.c.y * 28 + (p.rnd - .5f) * 7) * k, 0, (-p.c.x * 28 + (.5f - p.rnd) * 7) * k);
                                }
                            }
                            break;
                        }
                    case TileState.Fall:
                    case TileState.Hole:
                        {
                            s.fallT += dt;
                            foreach (var p in s.pieces)
                            {
                                if (!p.t.gameObject.activeSelf) continue;
                                float ft = s.fallT - p.delay;
                                if (ft <= 0) continue;
                                p.vel.y = Mathf.Max(-16, p.vel.y - 6 * dt);
                                p.t.localPosition += p.vel * dt;
                                p.t.localRotation = Quaternion.Euler(p.spin * dt) * p.t.localRotation;
                                // 掉进黑暗：亮度跟着深度往下掉，不然烘焙的亮面会一路亮到底
                                float dark = Mathf.Clamp01(1 + p.t.localPosition.y * .22f);
                                s.pmpb.SetColor(Tint, SlabTint * dark); p.r.SetPropertyBlock(s.pmpb);
                                if (p.t.localPosition.y < -70) p.t.gameObject.SetActive(false);
                            }
                            break;
                        }
                    case TileState.Rise:
                        {
                            float k = Mathf.Min(1, (float)(t.t / Cfg.RiseTime));
                            pos.y = -2.2f * Mathf.Pow(1 - k, 3);
                            break;
                        }
                }
                s.v.transform.localPosition = pos;

                float stay = t == stayTile && t.state == TileState.Solid ? .3f + Mathf.Sin(time * 9) * .12f : 0;
                float door = t.door ? .11f : 0;
                var glow = new Color(.9f, .5f, .2f) * stay + new Color(1f, .72f, .4f) * door;
                s.mpb.SetFloat(CrackK, s.broken ? 0 : Mathf.Min(1, crackK / .6f) * 1.1f);
                s.mpb.SetColor(Emission, glow);
                if (s.v.whole.enabled) s.v.whole.SetPropertyBlock(s.mpb);
            }
        }
    }
}
