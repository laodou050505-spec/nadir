using System.Collections.Generic;
using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 流程：安全须知 → 标题页 ↔ 关卡与设置 → 游戏。标题留在远处，能按的东西都在手边。
    /// 操作只有一种：伸手指点。手指必须从玩家这一侧穿过按钮才算按下，视线不参与
    /// </summary>
    public class UIFlow : MonoBehaviour
    {
        public GameRoot root; public UIScreen cover, safety, menu, pageMain, pageOpts, hint; public MeshRenderer coverBack;
        public Transform menuRoot, nearSafety, nearMenu;
        public UIButton safetyGo, safetyLang, langBtn, beginBtn, enterBtn, backBtn, lessonBtn, boundBtn, quitBtn, pauseBtn, resumeBtn, toTitleBtn; public UIScreen pauseChip, pauseMenu; public UIItem[] capModes; public GameObject[] floorRow; public Transform rowB; public UIButton[] modes, areas, floors;
        public UIItem capArea, capNofit, capSmall, capBound, capSit, handsNote, countdown, tipIcon, tipText, tipLine, ripple, reticle;
        public UILang tipIconLang, tipTextLang;
        public Transform titleRoot, rippleRoot, reticleRoot; public MeshRenderer areaLine; public UILang[] pips;
        public Texture2D[] pipTex, tipIcons, tipZh, tipEn, floorZh, floorEn; public string[] hintKeys;
        public AudioDirector sound;

        static readonly float[] Sides = { 2.5f, 3f, 3.75f, 5f };
        const float CoverTime = 4.6f, SafetyWait = 7f, HoverFar = .09f, ArmAt = .012f, ReleaseAt = .03f, Cooldown = .3f;
        static readonly int Half = Shader.PropertyToID("_Half"), RevealId = Shader.PropertyToID("_Reveal");
        static readonly Color Ember = new Color(.9f, .34f, .22f, 1);

        struct TipCard { public Texture2D icon, text; public float dur; public bool big; }
        readonly Queue<TipCard> queue = new();
        string state = "boot"; float t, hintT = -1, cool, rippleT = -1, lineK, lineHalf = 1.875f, modeSwap = -1, boundMsgT;
        UIButton over; float overDepth; Vector3 overTip;
        readonly UIButton[] armed = new UIButton[2]; readonly bool[] latched = new bool[2]; readonly bool[] was = new bool[2];
        bool pendingLang, oob, hintBig; Vector3 hintPos; int page, pendingPage = -1;
        int mode, unlocked, area, floor = -1, pendingFloor, pendingMode; MaterialPropertyBlock lineMpb;

        public string AreaName => Cfg.Areas[area];

        void Awake()
        {
            mode = PlayerPrefs.GetInt("DLD_mode", 1); unlocked = PlayerPrefs.GetInt("DLD_unlocked", 1); area = PlayerPrefs.GetInt("DLD_area", -1);
            var sys = Application.systemLanguage;
            UILang.Chinese = PlayerPrefs.GetInt("DLD_lang", sys == SystemLanguage.Chinese || sys == SystemLanguage.ChineseSimplified || sys == SystemLanguage.ChineseTraditional ? 1 : 0) == 1;
            lineMpb = new MaterialPropertyBlock();
        }

        void Start()
        {
            for (int i = 0; i < modes.Length; i++) { int k = i; modes[i].onPress = () => SetMode(k); }
            for (int i = 0; i < areas.Length; i++) { int k = i; areas[i].onPress = () => SetArea(k); }
            for (int i = 0; i < floors.Length; i++) { int k = i; floors[i].onPress = () => SetFloor(k); }
            beginBtn.onPress = () => pendingPage = 1;
            enterBtn.onPress = () => Go(PlayerPrefs.GetInt("DLD_lessonDone", 0) == 0); lessonBtn.onPress = () => Go(true);
            enterBtn.lit = beginBtn.lit = safetyGo.lit = true;      // 主操作常画着圈
            backBtn.onPress = () => pendingPage = 0;
            quitBtn.onPress = () => { PlayerPrefs.Save(); Application.Quit(); };
            if (root.ending)
            {
                root.ending.titleBtn.onPress = () => root.EndingToMenu();
                root.ending.quitBtn.onPress = () => { PlayerPrefs.Save(); Application.Quit(); };
                root.ending.titleBtn.lit = true;
            }
            pauseBtn.onPress = () => SetPause(true);
            resumeBtn.onPress = () => SetPause(false);
            toTitleBtn.onPress = () => { pauseMenu.HideNow(); pauseChip.HideNow(); root.ExitToMenu(); };            boundBtn.onPress = () => { if (!root.mr || !root.mr.OpenSystemBoundary()) boundMsgT = 6; };
            safetyGo.onPress = () => { PlayerPrefs.Save(); safety.Hide(); state = "toMenu"; };
            langBtn.onPress = safetyLang.onPress = () => { if (pendingLang) return; pendingLang = true; if (state == "menu") { menu.Hide(); pageMain.Hide(); } else safety.Hide(); };
            Relang();
            lampBar.HideNow(); wristLamps.HideNow(); foreach (var r in lampSlots) r.enabled = false; foreach (var r in wristSlots) r.enabled = false;
            pauseChip.HideNow(); pauseMenu.HideNow(); cover.HideNow(); safety.HideNow(); menu.HideNow(); pageMain.HideNow(); pageOpts.HideNow(); hint.HideNow(); areaLine.enabled = false; SetBack(0);
            ripple.Show(false); ripple.Snap(); reticle.Show(false); reticle.Snap(); handsNote.Show(false); handsNote.Snap(); root.ApplyMode(mode);
#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool("DLD_BotPlay", false)) { backA = 0; state = "play"; root.ApplyMode(2); root.StartRun(root.startLayer); return; }
#endif
            // 开场：系统启动画面之后，封面再停一会儿
            state = "cover"; t = 0; Face(cover.transform, 3f, .05f); cover.Show(); SetBack(1); root.fade.To(0, 1f, Color.black);
        }

        void SetBack(float a)
        {
            lineMpb.SetColor(BackColor, new Color(0, 0, 0, a)); coverBack.SetPropertyBlock(lineMpb); coverBack.enabled = a > .001f;
            lineMpb.Clear();
        }
        static readonly int BackColor = Shader.PropertyToID("_Color"); float backA = 1;

        /// <summary>须知没读完之前，继续和语言按钮都不出现</summary>
        void SafetyBtns(bool on)
        {
            foreach (var b in new[] { safetyGo, safetyLang }) { foreach (var p in b.parts) p.Show(on); if (b.back) b.back.Show(on); }
        }

        /// <summary>
        /// 界面一直待在够得着的地方：玩家从坐到站、往后退了一步，面板都会慢慢跟过来，不用低头或走回去。
        /// 只在差得够多时才动（有死区），平时纹丝不动，手指不会点偏
        /// </summary>
        bool following;
        void Follow(Transform p, float dist, float dy, float dt)
        {
            var c = root.cam.transform; var f = p.forward; f.y = 0; f.Normalize();
            var want = c.position + f * dist + Vector3.up * dy;
            var off = want - p.position; float d = off.magnitude;
            if (d > .14f) following = true; else if (d < .02f) following = false;
            if (following) p.position += off * (1 - Mathf.Exp(-dt * 3.2f));
        }

        // ───────── 暂停 ─────────
        void SetPause(bool on)
        {
            if (on && (state != "play" || root.game == null)) return;      // 只有对局里才能暂停
            root.Paused = on; AudioListener.pause = on;
            if (on) { pauseChip.HideNow(); wristOn = false; Face(pauseMenu.transform, 1.6f, -.03f); pauseMenu.Show(-3); }
            else { pauseMenu.Hide(); }
        }

        // ───────── 手背：像看手表一样翻手看一眼，才出现暂停和进度 ─────────
        // 平时视野里什么都不挂。手背朝着眼睛、离脸 20～55 厘米、稳住 0.35 秒才出现，手放下就收起。
        // 掌心朝上是 PICO 的系统菜单手势，手背朝自己正好相反，不会冲突
        bool wristOn; int wristSide; float wristHold, wristLost;
        bool WristFacing(in HandInput h, Vector3 eye, out Vector3 wrist)
        {
            wrist = InputSource.ToU(h.wrist);
            if (!h.tracked) return false;
            var back = InputSource.ToU(h.back); if (back.sqrMagnitude < .5f) return false;
            var toEye = eye - wrist; float d = toEye.magnitude;
            // 门槛 .86：手平伸掌心朝下（推黑影的姿势）只有 .74，不会误弹
            return d > .2f && d < .55f && Vector3.Dot(back, toEye / d) > .86f;
        }
        readonly Vector3[] wristPrev = new Vector3[2]; float tapCool;

        void TickPause(float dt)
        {
            var g = root.game; var inp = root.LastInput; var c = root.cam.transform;
            bool can = state == "play" && g != null && g.state == "play" && !root.Paused;
            bool facing = false; Vector3 wpos = default; int side = wristSide;
            if (can)
                for (int k = 0; k < 2 && !facing; k++)
                {
                    int s = wristOn ? wristSide : k;
                    bool f0 = WristFacing(inp.Hand(s), c.position, out var w);
                    // 看手表是停住的动作：手还在快速移动（挥、推、拍）时不算，出现之后才允许轻微晃动
                    float spd = (w - wristPrev[s]).magnitude / Mathf.Max(dt, 1e-3f); wristPrev[s] = w;
                    if (f0 && (wristOn || spd < .35f)) { facing = true; side = s; wpos = w; }
                    if (wristOn) break;
                }
            // 直接点手背就暂停：另一只手的食指尖碰到这只手的手背（手背中心离手腕 6 厘米）。
            // 不用先等按钮出现。只有手背大致朝向自己时才算，拍触手、推黑影时两手撞到一起不会误触
            tapCool -= dt;
            if (can && tapCool <= 0)
                for (int s = 0; s < 2; s++)
                {
                    var hb = inp.Hand(s); var other = inp.Hand(1 - s);
                    if (!hb.tracked || !other.tracked || !other.hasTip) continue;
                    var back = InputSource.ToU(hb.back); if (back.sqrMagnitude < .5f) continue;
                    var w = InputSource.ToU(hb.wrist); var palmP = InputSource.ToU(hb.palm);
                    var center = Vector3.Lerp(w, palmP, .7f) + back * .02f;
                    var toEye = (c.position - center).normalized;
                    if (Vector3.Dot(back, toEye) < .35f) continue;
                    var tip = InputSource.ToU(other.tip);
                    if (Vector3.Distance(tip, center) < .045f) { tapCool = .8f; sound.Play("ui_press", null, .45f, .02f); SetPause(true); return; }
                }
            wristHold = facing ? wristHold + dt : 0;
            wristLost = facing ? 0 : wristLost + dt;
            if (!wristOn && wristHold > .35f) { wristOn = true; wristSide = side; pauseChip.Show(-.3f); sound.Play("ui_tick", null, .25f, .03f); }
            else if (wristOn && (wristLost > .25f || !can)) { wristOn = false; pauseChip.Hide(); }
            if (pauseChip.gameObject.activeSelf && facing)
            {
                // 贴在手背上方一点，正对眼睛
                var up = Vector3.ProjectOnPlane(c.up, (c.position - wpos).normalized).normalized;
                var want = wpos + (c.position - wpos).normalized * .025f + up * .055f;
                var t0 = pauseChip.transform; t0.position = Vector3.Lerp(t0.position, want, 1 - Mathf.Exp(-dt * 18));
                t0.rotation = Quaternion.LookRotation((t0.position - c.position).normalized, up);
            }
            pauseChip.Tick(dt);
            pauseMenu.Tick(dt);
        }

        // ───────── 收集进度 ─────────
        // 不常驻：开局和每收到一盏灯时，在前方 1.2 米出现几秒，停在原地不跟着头走，然后淡出。
        // 想随时看，就看一眼手背：进度也画在暂停按钮上面
        public UIScreen lampBar, wristLamps; public MeshRenderer[] lampSlots, wristSlots;      // 最多 12 格
        public Texture2D lampOff, lampOn, goalDoneZh, goalDoneEn; public Texture2D[] goalZh, goalEn;
        int lampGoal, lampGot; float[] lampPop; float lampShowT; MaterialPropertyBlock lampMpb;
        static readonly int MapId = Shader.PropertyToID("_BaseMap"), TintId = Shader.PropertyToID("_Tint"), GlowId = Shader.PropertyToID("_Glow"), RevealU = Shader.PropertyToID("_Reveal");
        const float LampShowFor = 3.2f;

        /// <summary>新的一层：告诉玩家要收几盏，进度清零</summary>
        public void LampsBegin(int goal, bool announce)
        {
            lampGoal = Mathf.Clamp(goal, 0, lampSlots.Length); lampGot = 0; lampPop = new float[lampSlots.Length];
            if (lampGoal > 0) { if (announce) { queue.Enqueue(new TipCard { text = (UILang.Chinese ? goalZh : goalEn)[lampGoal - 1], dur = 4f }); lampShowT = 6.5f; PlaceLamps(); } }
            else { lampBar.HideNow(); lampShowT = 0; }
        }

        /// <summary>收到一盏：进度出现几秒，那一格亮起来、弹一下；集齐了提示去撕开</summary>
        public void LampGot(int collected, int goal)
        {
            lampGot = Mathf.Clamp(collected, 0, lampGoal);
            if (lampGot > 0 && lampGot <= lampPop.Length) lampPop[lampGot - 1] = 1;
            if (lampGoal > 0) { if (lampShowT <= 0 || !lampBar.Open) PlaceLamps(); lampShowT = LampShowFor; }
            if (lampGot >= lampGoal && lampGoal > 0 && root.game?.tut == null) { queue.Clear(); if (hintT > 0) hintT = .001f; queue.Enqueue(new TipCard { text = UILang.Chinese ? goalDoneZh : goalDoneEn, dur = 4f }); }
        }

        public void LampsHide() { lampBar.Hide(); lampShowT = 0; }

        /// <summary>摆到玩家前方偏下 1.2 米处，之后不动：离得远、不跟头，就不会觉得贴在脸上</summary>
        void PlaceLamps()
        {
            var c = root.cam.transform; var f = c.forward; f.y = 0; f = f.sqrMagnitude > .01f ? f.normalized : Vector3.forward;
            lampBar.transform.position = c.position + f * 1.2f + Vector3.up * -.32f;
            lampBar.transform.rotation = Quaternion.LookRotation((lampBar.transform.position - c.position).normalized);
        }

        void DrawLamps(MeshRenderer[] slots, bool visible, float dt, float w, Vector2 size)
        {
            lampMpb ??= new MaterialPropertyBlock();
            float x0 = -(lampGoal - 1) * w * .5f;
            for (int i = 0; i < slots.Length; i++)
            {
                var r = slots[i]; bool on = i < lampGoal; r.enabled = on && visible; if (!on) continue;
                float pop = lampPop != null && i < lampPop.Length ? lampPop[i] : 0;
                bool lit = i < lampGot;
                r.transform.localPosition = new Vector3(x0 + i * w, 0, 0); r.transform.localScale = new Vector3(size.x, size.y, 1) * (1 + pop * .6f);
                r.GetPropertyBlock(lampMpb);
                lampMpb.SetTexture(MapId, lit ? lampOn : lampOff);
                lampMpb.SetColor(TintId, lit ? new Color(1f, .78f, .42f, 1) : new Color(.72f, .69f, .6f, .55f));
                lampMpb.SetColor(GlowId, lit ? new Color(.5f, .28f, .08f, 0) * (.6f + pop * 2) : Color.clear);
                lampMpb.SetFloat(RevealU, 1);
                r.SetPropertyBlock(lampMpb);
            }
        }

        void TickLamps(float dt)
        {
            bool inPlay = state == "play" && root.game != null && (root.game.state == "play" || root.game.state == "falling") && !root.Paused && lampGoal > 0;
            lampShowT -= dt;
            bool show = inPlay && lampShowT > 0;
            if (!show && lampBar.Open) lampBar.Hide(); else if (show && !lampBar.Open && lampBar.Gone) lampBar.Show();
            if (lampPop != null) for (int i = 0; i < lampPop.Length; i++) lampPop[i] = Mathf.MoveTowards(lampPop[i], 0, dt * 1.6f);
            if (lampBar.gameObject.activeSelf) DrawLamps(lampSlots, lampBar.Open, dt, .085f, new Vector2(.04f, .066f));
            lampBar.Tick(dt);
            // 手背上的那份：跟着暂停按钮，画在它上方
            bool onWrist = wristOn && inPlay;
            if (onWrist && !wristLamps.Open) wristLamps.Show(-.3f); else if (!onWrist && wristLamps.Open) wristLamps.Hide();
            if (wristLamps.gameObject.activeSelf)
            {
                var t0 = pauseChip.transform; wristLamps.transform.SetPositionAndRotation(t0.position + t0.up * .042f, t0.rotation);
                DrawLamps(wristSlots, wristLamps.Open, dt, .021f, new Vector2(.011f, .018f));
            }
            wristLamps.Tick(dt);
        }

        void Relang() { foreach (var l in GetComponentsInChildren<UILang>(true)) l.Apply(); }

        /// <summary>从系统设置回来：边界可能变了，重新读一次</summary>
        void OnApplicationPause(bool paused)
        {
            if (paused || state != "menu" || !root.mr) return;
            root.mr.Realign(); FitAreas(); SetArea(area);
        }

        /// <summary>把界面摆到玩家正前方（只跟水平朝向，不跟低头抬头）</summary>
        void Face(Transform p, float dist, float dy)
        {
            var c = root.cam.transform; var f = Vector3.ProjectOnPlane(c.forward, Vector3.up);
            if (f.sqrMagnitude < .01f) f = Vector3.forward; f.Normalize();
            p.position = c.position + f * dist + Vector3.up * dy;
            p.rotation = Quaternion.LookRotation(f);
        }

        public void ShowMenu()
        {
            state = "menu"; t = 0; hint.HideNow(); queue.Clear(); hintT = -1; oob = false; page = 0; pendingPage = -1;
            if (root.mr) root.mr.Realign();
            unlocked = PlayerPrefs.GetInt("DLD_unlocked", 1);
            for (int i = 0; i < floors.Length; i++)
            {
                floors[i].active = i < unlocked;
                pips[i].zh = pipTex[Mathf.Clamp(PlayerPrefs.GetInt("DLD_star" + (i + 1), 0), 0, 3)]; pips[i].Apply();
            }
            floor = Mathf.Clamp(floor >= 0 ? floor : unlocked - 1, 0, unlocked - 1);
            FitAreas();
            // 第一次玩：标题页只有“开始”和语言；走过教学之后才多一个“玩法介绍”。没解锁第二层之前也不显示楼层
            bool veteran = PlayerPrefs.GetInt("DLD_lessonDone", 0) == 1;
            lessonBtn.gameObject.SetActive(veteran);
            // 第一排“开始”，第二排语言和退出（老玩家多一个教学），都居中
            int n2 = veteran ? 3 : 2; float x0 = -(n2 - 1) * .1565f;
            langBtn.transform.localPosition = new Vector3(x0, -.09f, 0); if (veteran) lessonBtn.transform.localPosition = new Vector3(x0 + .313f, -.09f, 0); quitBtn.transform.localPosition = new Vector3(x0 + (n2 - 1) * .313f, -.09f, 0);
            foreach (var g in floorRow) g.SetActive(unlocked > 1);
            rowB.localPosition = new Vector3(0, unlocked > 1 ? 0 : .36f, 0);      // 楼层现在是两行，藏起来时下面一排要往上补两行的高度
            Face(menuRoot, 1.6f, -.03f); menu.Show(); pageMain.Show(); pageOpts.HideNow();
            SetArea(area); SetFloor(floor); SelectMode(mode);
            lineHalf = Sides[area] / 2; lineK = 0;
        }

        /// <summary>只开放系统安全区放得下的范围；默认选放得下的最大一档。最小一档永远可选，不然没法玩</summary>
        void FitAreas()
        {
            float safe = SafeSide; int best = 0;
            for (int i = 0; i < areas.Length; i++) { areas[i].active = i == 0 || safe <= 0 || Sides[i] <= safe; if (areas[i].active) best = i; }
            if (safe <= 0) best = 1;
            if (area < 0 || !areas[Mathf.Min(area, areas.Length - 1)].active) area = best;
        }
        float SafeSide => root.mr ? root.mr.SafeSide : 0;

        void SelectMode(int k) { mode = k; for (int i = 0; i < modes.Length; i++) modes[i].lit = i == k; PlayerPrefs.SetInt("DLD_mode", k); }

        // 换难度时环境整个变了（透视 ↔ 医院），在一次短暂的暗场里换，不能硬切
        void SetMode(int k)
        {
            if (k == mode) return;
            SelectMode(k); pendingMode = k; modeSwap = .3f; root.fade.To(1, .28f, Color.black);
        }

        void SetArea(int k)
        {
            if (!areas[k].active) return;
            area = k; PlayerPrefs.SetInt("DLD_area", k);
            for (int i = 0; i < areas.Length; i++) areas[i].lit = i == k;
            root.tiles.SetArea(Sides[k] / 5);
        }

        void SetFloor(int k)
        {
            if (!floors[k].active) return;
            floor = k; for (int i = 0; i < floors.Length; i++) floors[i].lit = i == k;
        }

        bool pendingLesson;
        void Go(bool lesson)
        {
            pendingLesson = lesson; state = "going"; t = 0; pendingFloor = floor; PlayerPrefs.Save(); menu.Hide(); pageMain.Hide(); pageOpts.Hide();
            sound.Play("ui_begin", null, .5f, 0);
            root.fade.To(1, .9f, Color.black);
        }

        /// <summary>规则层的提示 → 视野下方一行字和一个图形。每种提示一辈子只教一次</summary>
        public void Tip(string key, int layerIdx)
        {
            if (key.StartsWith("tut:"))
            {
                // 教学的提示一直留着，做到了才换下一条
                int ti = System.Array.IndexOf(hintKeys, key.Substring(4)); if (ti < 0) { Debug.LogWarning("缺提示 " + key); return; }
                queue.Clear(); if (hintT > 0) hintT = .001f;
                queue.Enqueue(new TipCard { icon = tipIcons[ti], text = (UILang.Chinese ? tipZh : tipEn)[ti], dur = 1e5f });
                return;
            }
            string id = key switch
            {
                "start" => layerIdx == 0 ? "h_step" : layerIdx == 1 ? "h_move" : null,
                "t6" => layerIdx == 0 ? "h_gaze" : layerIdx == 1 ? "h_push" : null,
                "t16" => layerIdx == 0 ? "h_over" : null,
                "stay" => "h_move", "hand" => "h_swat", "eye" => "h_eye", "door" => "h_tear",
                _ => null,
            };
            if (id == null || PlayerPrefs.GetInt("DLD_hint_" + id, 0) == 1) return;
            PlayerPrefs.SetInt("DLD_hint_" + id, 1);
            int i = System.Array.IndexOf(hintKeys, id); if (i < 0) return;
            queue.Enqueue(new TipCard { icon = tipIcons[i], text = (UILang.Chinese ? tipZh : tipEn)[i], dur = 5f });
        }

        /// <summary>教学走完：教过的提示以后不再弹</summary>
        public void ClearHints() { queue.Clear(); hintT = -1; hint.HideNow(); }

        public void LessonDone()
        {
            PlayerPrefs.SetInt("DLD_lessonDone", 1);
            foreach (var k in new[] { "h_step", "h_gaze", "h_push", "h_eye", "h_tear" }) PlayerPrefs.SetInt("DLD_hint_" + k, 1);
            PlayerPrefs.Save(); queue.Clear(); if (hintT > 0) hintT = .001f;
        }

        public void ShowFloor(int idx) { queue.Clear(); oob = false; queue.Enqueue(new TipCard { text = (UILang.Chinese ? floorZh : floorEn)[idx], dur = 3f, big = true }); }

        public void ShowOob()
        {
            oob = true; int i = System.Array.IndexOf(hintKeys, "oob");
            if (i >= 0) queue.Enqueue(new TipCard { text = (UILang.Chinese ? tipZh : tipEn)[i], dur = 3f });
        }
        public void HideOob() => oob = false;

        /// <summary>
        /// 点按判定。防误触三条：手指要先出现在按钮前方才算“准备好”，从侧面或背面伸进去不算；
        /// 按下后要退出来才能再按；两次按下之间有间隔
        /// </summary>
        void Poke(in PlayerInput inp, bool picking, float dt)
        {
            over = null; overDepth = HoverFar; cool -= dt;
            for (int s = 0; s < 2; s++)
            {
                var h = inp.Hand(s);
                if (!picking || !h.tracked || !h.hasTip) { armed[s] = null; latched[s] = false; continue; }
                var tip = InputSource.ToU(h.tip);
                UIButton hit = null; float hd = HoverFar;
                foreach (var b in All())
                {
                    if (!b.Live) continue;
                    float d = b.Depth(tip, out bool inside);
                    if (inside && d > -.05f && d < hd) { hd = d; hit = b; }
                }
                if (latched[s] && (hit == null || hd > ReleaseAt)) latched[s] = false;
                if (hit != armed[s]) armed[s] = hit != null && hd > ArmAt ? hit : null;
                if (hit != null && hd < overDepth) { over = hit; overDepth = hd; overTip = tip; }
                if (hit != null && hit == armed[s] && hd <= 0 && !latched[s] && cool <= 0)
                {
                    latched[s] = true; armed[s] = null; cool = Cooldown; PressOn(hit);
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime; t += dt;
            var inp = root.LastInput;
            bool hands = (inp.left.tracked && inp.left.hasTip) || (inp.right.tracked && inp.right.hasTip);

            if (pendingLang && (state == "menu" ? pageMain.Gone && menu.Gone : safety.Gone))
            {
                pendingLang = false; UILang.Chinese = !UILang.Chinese; PlayerPrefs.SetInt("DLD_lang", UILang.Chinese ? 1 : 0);
                Relang();
                if (state == "menu") { menu.Show(-.9f); pageMain.Show(-1.2f); }
                else { safety.Show(-1); SafetyBtns(t > SafetyWait); }
            }
            if (pendingPage >= 0 && state == "menu")
            {
                var from = page == 0 ? pageMain : pageOpts;
                if (from.Open) from.Hide();
                if (from.Gone) { page = pendingPage; pendingPage = -1; (page == 0 ? pageMain : pageOpts).Show(-2f); }
            }
            if (modeSwap >= 0) { modeSwap -= dt; if (modeSwap < 0) { root.ApplyMode(pendingMode); root.fade.To(0, .5f, Color.black); } }
            if (state == "cover")
            {
                cover.transform.localScale = Vector3.one * (1 + t * .006f);        // 极慢地推近
                if (t > CoverTime) { cover.Hide(); state = "coverOut"; }
            }
            if (state == "coverOut" && cover.Gone)
            {
                backA = Mathf.MoveTowards(backA, 0, dt / .9f); SetBack(backA);
                if (backA <= 0) { state = "safety"; t = 0; Face(safety.transform, 1.6f, -.04f); safety.Show(); SafetyBtns(false); }
            }
            if (state == "safety")
            {
                countdown.revealMul = Mathf.Clamp01(t / SafetyWait);
                if (t > SafetyWait && !pendingLang) SafetyBtns(true);
            }
            if (state == "toMenu" && safety.Gone) ShowMenu();
            if (state == "going" && t > .95f) { state = "play"; menu.HideNow(); pageMain.HideNow(); pageOpts.HideNow(); root.StartRun(pendingLesson ? 0 : pendingFloor, AreaName, pendingLesson); }

            if (state == "safety") Follow(safety.transform, 1.6f, -.04f, dt);
            if (state == "menu" || state == "toMenu") Follow(menuRoot, 1.6f, -.03f, dt);
            TickPause(dt);
            TickLamps(dt);
            bool inPlay = state == "play" && root.game != null && root.game.state == "play";
            bool inEnding = root.ending && root.ending.Active;
            bool picking = ((state == "safety" || state == "menu") && !pendingLang && pendingPage < 0) || inPlay || inEnding;
            var prev = over;
            Poke(inp, picking, dt);
#if UNITY_EDITOR
            // 编辑器没有手：看着按钮按空格
            if (picking && !hands)
            {
                var ray = new Ray(InputSource.ToU(inp.gazeOrigin), InputSource.ToU(inp.gazeDir).normalized);
                float best = 6; foreach (var b in All()) { if (!b.Live) continue; float a = b.Angle(ray); if (a < best) { best = a; over = b; overDepth = .03f; overTip = b.transform.position; } }
                bool edge = false; for (int s = 0; s < 2; s++) { bool p = inp.Hand(s).tracked && inp.Hand(s).pinch; if (p && !was[s]) edge = true; was[s] = p; }
                if (edge && over) PressOn(over);
            }
#endif
            if (over != prev && over) sound.Play("ui_tick", null, .3f, .03f);
            foreach (var b in AllButtons())
            {
                bool on = b == over && b.Live;
                b.Tick(dt, on ? Mathf.Clamp01(1 - overDepth / HoverFar) : 0, on ? Mathf.Clamp(-overDepth, 0, .025f) / b.transform.parent.lossyScale.x : 0);
            }

            // 手指靠近时，按钮上有一个圆环跟着缩小，距离感一目了然
            reticle.Show(over != null && hands);
            if (over != null && hands)
            {
                var o = overTip - over.transform.position; var fw = over.transform.forward;
                reticleRoot.position = overTip - fw * Vector3.Dot(o, -fw) - fw * .003f; reticleRoot.rotation = over.transform.rotation;
                reticleRoot.localScale = Vector3.one * Mathf.Lerp(.16f, .5f, Mathf.Clamp01(overDepth / HoverFar));
            }
            reticle.Tick(dt);

            bool optsOpen = state == "menu" && page == 1 && pageOpts.Open && pendingPage < 0;
            bool noFit = over != null && !over.active && System.Array.IndexOf(areas, over) >= 0;
            bool small = SafeSide > 0 && SafeSide < Sides[0];
            boundMsgT -= dt;
            capBound.Show(optsOpen && boundMsgT > 0);
            capNofit.Show(optsOpen && noFit && boundMsgT <= 0);
            capSmall.Show(optsOpen && small && !noFit && boundMsgT <= 0);
            capArea.Show(optsOpen && !small && !noFit && boundMsgT <= 0);
            for (int i = 0; i < capModes.Length; i++) capModes[i].Show(optsOpen && i == mode);
            capSit.Show(state == "safety" && !pendingLang && root.cam.transform.localPosition.y < 1.25f && root.cam.transform.localPosition.y > .1f);
            handsNote.Show(picking && !inPlay && !inEnding && !hands && t > 1.5f);
            if (picking && !inPlay) { var near = state == "menu" ? nearMenu : nearSafety; handsNote.transform.parent.SetPositionAndRotation(near.position + near.up * (state == "menu" ? (page == 1 ? .245f : .215f) : .075f), near.rotation); }
            handsNote.Tick(dt);

            foreach (var b in AllButtons()) if (b.mark) b.mark.Show(b.lit && b.Live);
            if (state == "menu") titleRoot.localScale = Vector3.one * (1 + Mathf.Sin(t * .22f) * .012f);       // 片头字那种几乎察觉不到的呼吸

            TickRipple(dt);
            cover.Tick(dt); safety.Tick(dt); menu.Tick(dt); pageMain.Tick(dt); pageOpts.Tick(dt); TickHint(dt); TickLine(dt);
        }

        void PressOn(UIButton b)
        {
            if (!b.active) { sound.Play("ui_tick", null, .2f, 0, .7f); return; }
            sound.Play("ui_press", null, .45f, .02f);
            b.Press();
        }

#if UNITY_EDITOR
        // 自动截图脚本用：MCP 里不能反射
        public string DebugState => state + " page=" + page;
        public void DebugSkip() => t = 9;
        public void DebugForcePlay() { safety.HideNow(); menu.HideNow(); pageMain.HideNow(); pageOpts.HideNow(); backA = 0; SetBack(0); state = "play"; root.StartRun(3, "medium"); }
        public void DebugPause() => PressOn(pauseBtn);
        public void DebugUnlock(int n) { PlayerPrefs.SetInt("DLD_unlocked", n); }
        public void DebugCover() { safety.HideNow(); backA = 1; SetBack(1); state = "cover"; t = 0; Face(cover.transform, 3f, .05f); cover.Show(); }
        public void DebugLesson() => Go(true);
        public void DebugPress(UIButton b) => PressOn(b);
#endif

        void TickRipple(float dt)
        {
            if (rippleT < 0) { ripple.Tick(dt); return; }
            rippleT += dt / .5f; float e = 1 - (1 - rippleT) * (1 - rippleT);
            ripple.Show(rippleT < 1); ripple.Snap();
            rippleRoot.localScale = Vector3.one * Mathf.Lerp(.15f, .7f, e);
            var c = Ember; c.a = (1 - rippleT) * .8f; ripple.tint = c;
            ripple.Tick(dt);
            if (rippleT >= 1) rippleT = -1;
        }

        void TickLine(float dt)
        {
            bool on = (state == "menu" && page == 1) || oob;
            lineK = Mathf.MoveTowards(lineK, on ? 1 : 0, dt / (on ? 1.2f : .5f));
            areaLine.enabled = lineK > .001f;
            if (!areaLine.enabled) return;
            lineHalf = Mathf.Lerp(lineHalf, area >= 0 ? Sides[area] / 2 : 1.875f, 1 - Mathf.Exp(-dt * 8));
            lineMpb.SetFloat(Half, lineHalf); lineMpb.SetFloat(RevealId, lineK * lineK * (3 - 2 * lineK)); areaLine.SetPropertyBlock(lineMpb);
        }

        IEnumerable<UIButton> AllButtons()
        {
            yield return safetyGo; yield return safetyLang; yield return pauseBtn; yield return resumeBtn; yield return toTitleBtn; yield return quitBtn; yield return beginBtn; yield return enterBtn; yield return langBtn; yield return backBtn; yield return lessonBtn; yield return boundBtn;
            if (root.ending) { yield return root.ending.titleBtn; yield return root.ending.quitBtn; }
            foreach (var b in modes) yield return b;
            foreach (var b in areas) yield return b;
            foreach (var b in floors) yield return b;
        }

        IEnumerable<UIButton> All()
        {
            if (root.ending && root.ending.Active) { yield return root.ending.titleBtn; yield return root.ending.quitBtn; yield break; }
            if (root.Paused) { yield return resumeBtn; yield return toTitleBtn; yield break; }
            if (state == "play") { if (wristOn) yield return pauseBtn; yield break; }
            if (state == "safety") { yield return safetyGo; yield return safetyLang; yield break; }
            if (state != "menu") yield break;
            if (page == 0) { yield return beginBtn; if (lessonBtn.gameObject.activeSelf) yield return lessonBtn; yield return langBtn; yield return quitBtn; yield break; }
            foreach (var b in modes) yield return b;
            foreach (var b in areas) yield return b;
            foreach (var b in floors) yield return b;
            yield return backBtn; yield return boundBtn; yield return enterBtn;
        }

        void TickHint(float dt)
        {
            if (hintT <= 0 && queue.Count > 0 && hint.Gone)
            {
                var q = queue.Dequeue(); hintT = q.dur; hintBig = q.big;
                tipTextLang.zh = q.text; tipTextLang.en = null; tipTextLang.Apply();
                tipIcon.gameObject.SetActive(q.icon != null);
                if (q.icon) { tipIconLang.zh = q.icon; tipIconLang.Apply(); }
                var c0 = root.cam.transform; var f0 = Vector3.ProjectOnPlane(c0.forward, Vector3.up).normalized;
                hintPos = c0.position + f0 * HintDist + Vector3.up * HintLift;
                hint.Show();
            }
            if (hintT > 0)
            {
                hintT -= dt;
                // 懒跟随：左右跟着头转；上下最多压到水平线下约 15°。玩家几乎一直低头看地砖，
                // 提示不能跟下去盖在地板上，而是停在视野上方、远处墙面的高度，低头时也还看得到
                var c = root.cam.transform; var look = c.forward; float py = Mathf.Clamp(look.y, -.26f, .1f);
                var f = new Vector3(look.x, 0, look.z); f = f.sqrMagnitude > .01f ? f.normalized * Mathf.Sqrt(1 - py * py) : c.up * -1; f.y = py;
                var want = c.position + f * HintDist + Vector3.up * HintLift;
                hintPos = Vector3.Lerp(hintPos, want, 1 - Mathf.Exp(-dt * 1.8f));
                hint.transform.position = hintPos; hint.transform.rotation = Quaternion.LookRotation(hintPos - c.position);
                if (hintT <= 0) hint.Hide();
            }
            hint.Tick(dt);
        }
        float HintDist => hintBig ? 2.1f : 1.5f;
        float HintLift => hintBig ? .02f : root.game?.tut?.Target != null ? .2f : 0f;      // 教学有地上箭头时提示再抬高一点，不挡箭头
    }
}
