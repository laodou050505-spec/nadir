using System.Collections.Generic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 结局：被阳光照醒，回到真实世界。所有难度都一样（只有最后的亮度不同）：
    ///   走进传送门 → 传送门里的暗场慢慢黑下去（GameRoot 里做）
    ///   → 黑着的时候医院全部撤掉、切到透视、压暗层压得很暗
    ///   → 眼睛慢慢睁开（PlaySlow，5.4 秒，中途眨一下）
    ///   → 看到压暗的真实房间；眼前偏上方慢慢浮现一个日晕（太阳亮点 + 一圈淡淡的晕环）
    ///   → 房间跟着慢慢亮一些：普通模式回到它原来的压暗程度，简单和困难更亮
    ///   → 鸟叫和微风从一开始就慢慢响起来；看一会儿之后出文字和按钮
    /// 不给相机加任何整屏泛白，日晕只在一小块区域发光
    /// </summary>
    public class Ending : MonoBehaviour
    {
        public UIScreen card; public UIButton titleBtn, quitBtn; public AudioSource amb;
        public MeshRenderer halo;
        public bool Active { get; private set; }

        // 时间轴（秒，从黑场里切过来那一刻算起）
        const float HaloAt = .4f, HaloIn = 1.2f;                 // 眼睛还闭着时日晕就已经亮好，第一次眯开眼（约 1.9 秒）就看得到
        const float BrightAt = 5.6f, BrightIn = 8f;              // 房间亮得很慢，眼睛跟得上              // 睁开后房间慢慢变亮
        const float CardAt = 9f, CardDist = 1.5f;
        const float DimStart = .86f;                             // 刚睁眼时房间很暗
        const float HaloUp = 15f, HaloSide = 4f, HaloRing = 10f;       // 太阳：就在视线正前方略偏上（左 4°、上 15°），一睁眼就在眼前，不用找；主晕环半径 10°，下沿刚好在字卡上方

        float t, dimEnd, dimNow, glare; Vector3 sunDir; GameRoot root;
        readonly List<GameObject> hidden = new();
        MaterialPropertyBlock dimMpb, haloMpb; Renderer dimR; Vector3 cardDir;
        static readonly int SunDirId = Shader.PropertyToID("_SunDir"), RingId = Shader.PropertyToID("_Ring"), GlareId = Shader.PropertyToID("_Glare"), AlphaId = Shader.PropertyToID("_Alpha"), FadeId = Shader.PropertyToID("_Fade"), HandAlphaId = Shader.PropertyToID("_DLDHandAlpha");

        public void Begin(GameRoot r)
        {
            root = r; Active = true; t = 0; glare = 0;
            Debug.Log($"[NADIR] Ending.Begin mode={r.mode} cam={r.cam.transform.position}");
            // 医院里的东西全部撤掉（记下来，回标题时恢复）
            hidden.Clear();
            void Hide(GameObject g) { if (g && g.activeSelf) { g.SetActive(false); hidden.Add(g); } }
            foreach (var g in r.hospital) Hide(g);
            if (r.tiles) Hide(r.tiles.gameObject);
            Hide(GameObject.Find("Shaft")); Hide(GameObject.Find("ShaftMask"));
            // 回到真实世界：透视 + 压暗层
            if (r.mr) { r.mr.passthrough = true; r.mr.Apply(); }
            dimEnd = r.mode == 1 ? .62f : .45f;                  // 普通模式回到原来的压暗程度；简单、困难亮一些但不刺眼（困难模式一直在暗处）
            dimNow = DimStart;
            dimR = r.dim ? r.dim.GetComponent<Renderer>() : null;
            if (r.dim) r.dim.SetActive(true);
            SetDim(dimNow);
            if (r.playerHands) r.playerHands.SetPassthrough(true, dimNow);
            // 眼皮：此刻屏幕是黑的（传送门里已经黑下去了），眼皮从全闭开始，同一帧把黑幕撤掉，衔接上看不出来
            r.lids.PlaySlow();
            r.fade.To(0, .02f, Color.black);
            // 日晕先藏着
            if (halo) { halo.enabled = false; SetHalo(0); }
            card.HideNow();
            r.sound?.Ending();
            if (amb) { amb.volume = 0; amb.Play(); }
        }

        void SetDim(float a)
        {
            if (!dimR) return;
            dimMpb ??= new MaterialPropertyBlock();
            dimR.GetPropertyBlock(dimMpb); dimMpb.SetFloat(AlphaId, a); dimR.SetPropertyBlock(dimMpb);
        }

        void SetHalo(float k)
        {
            if (!halo) return;
            haloMpb ??= new MaterialPropertyBlock();
            halo.GetPropertyBlock(haloMpb); haloMpb.SetFloat(FadeId, k); haloMpb.SetFloat(GlareId, glare); haloMpb.SetVector(SunDirId, sunDir); halo.SetPropertyBlock(haloMpb);
        }

        /// <summary>
        /// 太阳挂在玩家此刻视线的正前方略偏上，之后方向固定（像真的太阳）：
        /// 日晕球套在相机外面，颜色只按视线方向算，所以没有视差、不是空中一块平面，转头时它在视野里移动
        /// </summary>
        void PlaceHalo()
        {
            sunDir = Home();
            haloMpb ??= new MaterialPropertyBlock();
            halo.GetPropertyBlock(haloMpb); haloMpb.SetVector(SunDirId, sunDir); haloMpb.SetFloat(RingId, HaloRing * Mathf.Deg2Rad); halo.SetPropertyBlock(haloMpb);
            halo.enabled = true;
        }

        /// <summary>太阳该在的位置：玩家此刻视线的正前方略偏上。
        /// 跟着玩家此刻的低头/抬头走一点（低头看地时太阳也放低，保证一睁眼就在视野里），但不会落到地平线以下，也不会高到要仰头</summary>
        Vector3 Home()
        {
            var c = root.cam.transform; var f = c.forward;
            float pitch = Mathf.Asin(Mathf.Clamp(f.y, -1, 1)) * Mathf.Rad2Deg;   // 抬头为正
            f.y = 0; f = f.sqrMagnitude > .01f ? f.normalized : Vector3.forward;
            float elev = Mathf.Clamp(pitch + HaloUp, 4f, 24f);
            f = Quaternion.AngleAxis(-HaloSide, Vector3.up) * f;
            return Quaternion.AngleAxis(-elev, Vector3.Cross(Vector3.up, f)) * f;
        }

        void PlaceCard(bool snap)
        {
            var c = root.cam.transform; var f = c.forward; f.y = 0; f = f.sqrMagnitude > .01f ? f.normalized : Vector3.forward;
            if (snap) cardDir = f;
            else if (Vector3.Angle(cardDir, f) > 30) cardDir = Vector3.Slerp(cardDir, f, 1 - Mathf.Exp(-Time.deltaTime * 1.4f)).normalized;
            card.transform.position = c.position + cardDir * CardDist + Vector3.up * -.05f;
            card.transform.rotation = Quaternion.LookRotation(cardDir);
        }

        public void Stop()
        {
            if (!Active) return;
            Active = false; card.HideNow();
            foreach (var g in hidden) if (g) g.SetActive(true);
            hidden.Clear();
            if (dimR) dimR.SetPropertyBlock(null);              // 压暗层回到材质里的原值，ApplyMode 再决定开不开
            if (halo) { SetHalo(0); halo.enabled = false; }
            if (amb) { amb.Stop(); amb.volume = 0; }
            if (root) root.lids.Stop();
        }

        static float Ease(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }

        public void Tick(float dt)
        {
            if (!Active) return;
            t += dt;
            // 太阳和日晕：眼睛还闭着时就在玩家视线正前方略偏上亮好
            if (t >= HaloAt && halo && !halo.enabled) PlaceHalo();
            if (halo && halo.enabled)
            {
                // 太阳固定在出现时的位置（正前方略偏上），之后转头不跟；脸越正对着它，周围的暖光越强（慢慢跟上，不会一下子变）
                var c = root.cam.transform; var toSun = sunDir;
                float face = Mathf.Clamp01(Mathf.InverseLerp(Mathf.Cos(40 * Mathf.Deg2Rad), Mathf.Cos(6 * Mathf.Deg2Rad), Vector3.Dot(c.forward, toSun)));
                glare = Mathf.Lerp(glare, face * face * (3 - 2 * face), 1 - Mathf.Exp(-dt * 2.5f));
                SetHalo(Ease((t - HaloAt) / HaloIn));
            }
            // 房间：睁开后慢慢变亮到最终亮度
            dimNow = Mathf.Lerp(DimStart, dimEnd, Ease((t - BrightAt) / BrightIn));
            SetDim(dimNow);
            Shader.SetGlobalFloat(HandAlphaId, dimNow);          // 真手跟周围的房间一样亮
            // 鸟叫和微风：从黑场里就开始慢慢响起来
            if (amb) amb.volume = Mathf.Lerp(amb.volume, .6f * Mathf.Clamp01((t - .3f) / 6f), 1 - Mathf.Exp(-dt * 1.2f));
            // 文字：睁开、看见日晕之后再出现，在玩家视线正前方、日晕下面
            if (t > CardAt && !card.Open) { PlaceCard(true); card.Show(); }
            if (card.gameObject.activeSelf) PlaceCard(false);
            card.Tick(dt);
        }
    }
}
