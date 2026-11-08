using DLD.Logic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 把规则层接到场景上：每帧读输入 → 跑规则 → 把事件分发给各表现件。
    /// 规则层不知道场景存在，这里也不写规则
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        public InputSource input; public Camera cam;
        public TileField tiles; public ShardField shards; public FigureField figures; public HandField hands; public DeepArms deepArms;
        public EyeView eye; public PortalView portal; public LightDirector lights; public ScreenFade fade; public AudioDirector sound; public Transform rig; public GameObject fallMask;
        public int startLayer; public bool Paused; public Ending ending; public RiftRoom rift; public Lids lids; public TutorialMarks marks; public DangerArrows arrows; public UIFlow flowUI; public CastFx cast; public PlayerHands playerHands; public GameObject[] hospital; public GameObject dim; public MRRig mr;
        public PlayerInput LastInput;

        public Game game;
        float clock; string flow = "boot"; float flowT, fallT = -1, sink; int nextLayer; Vector3 portalPos;

        /// <summary>0 普通：透视；1 恐怖：透视压暗；2 恐怖 + 场景包围</summary>
        public void ApplyMode(int m)
        {
            mode = m;
            foreach (var g in hospital) if (g) g.SetActive(m == 2);
            tiles.hospitalOn = m == 2; tiles.Rims();
            if (dim) dim.SetActive(m == 1);
            if (mr) { mr.passthrough = m != 2; mr.Apply(); }
            if (playerHands) playerHands.SetPassthrough(m != 2, m == 1 && dim ? dim.GetComponent<Renderer>().sharedMaterial.GetFloat("_Alpha") : 0);
        }
        public int mode = 2; bool lessonNext;
        static readonly double[] FallSeconds = { 2.2, 3.2, 4.4 };   // 简单 / 普通 / 困难

        public void StartRun(int layer, string area = "medium", bool lesson = false)
        {
            lessonNext = lesson;
            if (lights && !lights.dim && dim) lights.dim = dim.GetComponent<Renderer>();      // 旧场景没接上压暗层时补上
            if (mr) mr.Align();
            game = new Game(System.Environment.TickCount & 0xFFFFFF, area, mode == 0 ? "normal" : "horror") { fallSeq = FallSeconds[mode], handsOn = mode == 2, safeAt = mr ? mr.InSafe : null };
            BeginLayer(layer);
        }

        /// <summary>游戏中从手掌菜单退回标题</summary>
        public void ExitToMenu() { Paused = false; AudioListener.pause = false; if (game == null || flow == "tomenu") return; flow = "tomenu"; flowT = .7f; portalPhase = -1; rift.Hide(); fade.To(1, .55f, Color.black); }

        /// <summary>暂停菜单里选了一层：短暂黑场，然后从那一层重新开始</summary>
        public void JumpTo(int idx)
        {
            Paused = false; AudioListener.pause = false;
            if (game == null) return;
            lights.StopFlicker();
            lessonNext = false; nextLayer = Mathf.Clamp(idx, 0, Layers.All.Length - 1); flow = "next"; flowT = .6f;
            fade.To(1, .45f, Color.black);
        }

        /// <summary>逃出来了：清掉医院里的一切，被阳光照醒，回到真实世界（具体流程在 Ending 里）</summary>
        void EnterEnding()
        {
            flow = "ending"; arrows.Clear(); marks.Tick(1, null); flowUI.LampsBegin(0, false); shards.Clear(); hands.Clear(); portal.Hide(); eye.Clear(); lights.StopFlicker();
            figures.Bind(new System.Collections.Generic.List<Effigy>()); deepArms.Bind(-1); fallT = -1; if (fallMask) fallMask.SetActive(false);
            flowUI.ClearHints();
            PlayerPrefs.SetInt("DLD_escaped", 1); PlayerPrefs.Save();
            ending.Begin(this);
            game = null;      // 医院里的一局到此结束：暂停、进度、提示都不再响应
        }

        // ───────── 进传送门 ─────────
        // 一走进去就在裂隙里面了（RiftRoom），不再有黑暗把人包起来的过程；里面很暗，停一会儿
        // 2.6 秒：慢慢黑下去；3.2 秒：下一层从黑里亮起来（最后一层则接结局：闭眼 → 被阳光照醒）
        const float PortalTotal = 3.2f, DarkAt = 2.6f;
        int portalPhase = -1; Vector3 portalAhead;

        void EnterPortal(Vector3 portalPos, bool last, int next)
        {
            var c = cam.transform.position;
            portalAhead = portalPos - c;
            portal.Hide(); rift.Show(portalAhead);
            flowUI.ClearHints(); flowUI.LampsBegin(0, false);      // 关卡里的提示和进度一起收掉，里面干干净净
            flow = last ? "toEnding" : "next"; flowT = PortalTotal; nextLayer = next; portalPhase = 1;
        }

        /// <summary>调试：直接走进传送门（和真的走进去完全同一条路）。toEnd=true 走最后一层的结局，false 重新进本层</summary>
        public void DebugEnterPortal(bool toEnd = true)
        {
            if (game == null || flow != "play") return;
            var c = cam.transform;
            EnterPortal(c.position + c.forward * 1.5f, toEnd, game.layerIdx);
        }

        /// <summary>结局里点了"回到标题"</summary>
        public void EndingToMenu() { flow = "tomenu"; flowT = .7f; fade.To(1, .5f, Color.black); }      // 先黑场；ToMenu 里再撤掉室外、恢复医院

        public void ToMenu()
        {
            if (ending && ending.Active) { ending.Stop(); ApplyMode(mode); }
            rift.Hide(); portalPhase = -1;
            flow = "menu"; game = null; arrows.Clear(); marks.Tick(1, null); flowUI.LampsBegin(0, false); shards.Clear(); hands.Clear(); portal.Hide(); eye.Clear(); lights.StopFlicker();
            fade.To(0, 1.2f, Color.black); flowUI.ShowMenu();
        }

        void BeginLayer(int idx)
        {
            if (lessonNext) game.StartTutorial(); else game.StartLayer(idx);
            lessonNext = false;
            fallT = -1; lids.Stop(); if (fallMask) fallMask.SetActive(false);
            tiles.Bind(game.floor); shards.Clear(); hands.Clear(); portal.Hide(); eye.Clear(); lights.StopFlicker();
            figures.Bind(game.scares.effigies); deepArms.Bind(game.handsOn ? idx : -1);
            fade.To(0, 1.4f, Color.black);
            flow = "play";
            sound?.Layer(game);
            if (game.tut == null) flowUI.ShowFloor(idx);
            flowUI.LampsBegin(game.tut == null ? game.L.shards : 0, game.tut == null);      // 玩法介绍只收一盏，不需要进度栏
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, .1f); clock += dt;
            LastInput = input.Poll(dt); var inp = LastInput;
            if (Paused) { fade.Tick(dt); return; }
            var head = cam.transform.position;
            if (flow == "ending") { ending.Tick(dt); fade.Tick(dt); lids.Tick(dt); sound?.TickEnding(dt); return; }
            // 结局里点了"回到标题"：这时已经没有对局，倒计时要在这里走，不然会卡在黑场里
            if (game == null && flow == "tomenu") { flowT -= dt; ending.Tick(dt); fade.Tick(dt); if (flowT <= 0) ToMenu(); return; }
            if (game == null) { fade.Tick(dt); cast.Tick(dt, null, head); sound?.TickMenu(dt); return; }
            if (flow == "play")
            {
                game.Update(dt, inp);
                foreach (var e in game.Drain()) Dispatch(e, head);
            }
            else
            {
                flowT -= dt;
                if (portalPhase >= 0)
                {
                    float e = PortalTotal - flowT;
                    if (portalPhase == 1 && e >= DarkAt) { fade.To(1, .6f, Color.black); portalPhase = 2; }
                }
                if (flowT <= 0 && flow == "next") { rift.Hide(); portalPhase = -1; BeginLayer(nextLayer); }
                else if (flowT <= 0 && flow == "tomenu") { rift.Hide(); portalPhase = -1; ToMenu(); return; }
                else if (flowT <= 0 && flow == "toEnding") { rift.Hide(); portalPhase = -1; EnterEnding(); return; }
            }
            tiles.Tick(dt, game, clock);
            shards.Tick(clock);
            figures.Tick(game.scares.effigies, head);
            hands.Tick(game, head, clock, eye.Visible);
            deepArms.Tick(clock, fallT >= 0 ? head : null, eye.Bottom);
            eye.Tick(dt, game, head);
            portal.Tick(game, head, clock);
            lights.Tick(dt, game, clock, 0);
            fade.Tick(dt);
            rift.Tick(dt);
            RigFx(dt);
            lids.Tick(dt);
            marks.Tick(dt, game);
            arrows.Tick(dt, game);
            cast.Tick(dt, game.scares.castFrom.HasValue ? InputSource.ToU(game.scares.castFrom.Value) : null, head);
            sound?.Tick(dt, game, head, cam.transform.forward);
        }

        /// <summary>被拖住时视线慢慢下沉；坠落时真的掉进井里，看着洞口在头顶远去，然后才淡出</summary>
        void RigFx(float dt)
        {
            // 被拖时视线跟着拖拽进度下沉，松开后慢慢回来
            float target = -(float)game.pull * .95f;
            sink = Mathf.Lerp(sink, target, 1 - Mathf.Exp(-dt * 5));
            float y = sink;
            if (fallT >= 0)
            {
                fallT += dt;
                // 不到全重力，够看清井壁，也不至于太晕；到 10 米每秒就不再加速
                float t = fallT, seq = (float)game.fallSeq, fadeAt = seq - .85f;
                y += t < 1.92f ? -.5f * 5.2f * t * t : -9.58f - 10f * (t - 1.92f);
                if (fallT > fadeAt && fallT - dt <= fadeAt) fade.To(1, .6f, Color.black);
            }
            if (rig) rig.position = (mr ? mr.basePos : Vector3.zero) + new Vector3(0, y, 0);
        }

        void Progress()
        {
            int id = game.L.id, un = PlayerPrefs.GetInt("DLD_unlocked", 1);
            PlayerPrefs.SetInt("DLD_unlocked", Mathf.Max(un, Mathf.Min(Layers.All.Length, id + 1)));
            PlayerPrefs.SetInt("DLD_star" + id, Mathf.Max(PlayerPrefs.GetInt("DLD_star" + id, 0), game.result.stars));
            PlayerPrefs.Save();
        }

        void Dispatch(GameEvent e, Vector3 head)
        {
            sound?.On(e, game);
            switch (e.type)
            {
                case "tip": flowUI.Tip(e.kind, game.layerIdx); break;
                case "oob": flowUI.ShowOob(); break;
                case "inbounds": flowUI.HideOob(); break;
                case "swat": cast.Swat(InputSource.ToU(e.hand.pos), head, e.killed); break;
                case "handSpawn": if (tiles.dust) { tiles.dust.transform.position = InputSource.ToU(e.hand.anchor) + Vector3.up * .05f; tiles.dust.Emit(14); } break;     // 预警：洞口先冒灰
                case "shardSpawn": shards.Spawn(e.shard, clock); break;
                case "shard": shards.Remove(e.shard); flowUI.LampGot(e.collected, e.goal); break;
                case "shardFade": shards.Remove(e.shard); break;
                case "doorSpawn": portal.Show(e.tile, head); break;
                case "eyeSwat": cast.Cast(InputSource.ToU(e.pos), InputSource.ToU(e.pos) + cam.transform.forward * 2.2f); eye.Slap(); break;
                case "scare":
                    if (e.kind == "push") cast.Cast(InputSource.ToU(e.from), InputSource.ToU(e.pos));
                    break;
                case "fall_player": fallT = 0; if (fallMask) fallMask.SetActive(true); break;
                case "wake": fallT = -1; sink = 0; if (fallMask) fallMask.SetActive(false); lids.Play(); fade.To(0, .05f); break;
                case "lost": fade.To(1, .6f, Color.black); flow = "tomenu"; flowT = 1.6f; break;
                case "clear":
                    if (game.tut != null) { EnterPortal(portal.transform.position + Vector3.up * 1.2f, false, 0); flowUI.LessonDone(); break; }
                    Progress();
                    EnterPortal(portal.transform.position + Vector3.up * 1.2f, game.layerIdx >= Layers.All.Length - 1, game.layerIdx + 1);      // 最后一层的裂隙那边是外面
                    break;
            }
        }
    }
}
