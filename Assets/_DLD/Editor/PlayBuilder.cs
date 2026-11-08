using System.Linq;
using DLD.Play;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace DLD.EditorTools
{
    /// <summary>搭玩家和玩法部件：相机、手眼输入、各表现件</summary>
    public static class PlayBuilder
    {
        const string Dir = "Assets/_DLD/Prefabs/";
        static GameObject Load(string n) => AssetDatabase.LoadAssetAtPath<GameObject>(Dir + n);
        static T Child<T>(Transform p, string name) where T : Component { var g = new GameObject(name); g.transform.SetParent(p, false); return g.AddComponent<T>(); }

        public static void Build(Transform world, SceneLook look)
        {
            var origin = new GameObject("XR Origin").transform;
            var camGO = new GameObject("Main Camera"); camGO.transform.SetParent(origin, false);
            camGO.transform.localPosition = new Vector3(0, 1.6f, 0); camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.nearClipPlane = .05f; cam.farClipPlane = 120; cam.fieldOfView = 90;
            camGO.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
            camGO.AddComponent<AudioListener>();
            var tpd = camGO.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
            tpd.positionInput = new InputActionProperty(new InputAction("HeadPos", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction("HeadRot", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            var pxr = origin.gameObject.AddComponent<ByteDance.PICO.XR.PXR_Manager>(); pxr.eyeTracking = true;
            var mr = origin.gameObject.AddComponent<MRRig>(); mr.cam = cam;

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.name = "Fade"; sphere.transform.SetParent(camGO.transform, false); sphere.transform.localScale = Vector3.one * .5f;
            var fadeMat = DLDArt.Mat("K_fade", "DLD/Fade");
            var sr = sphere.GetComponent<MeshRenderer>(); sr.sharedMaterial = fadeMat; sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 醒来时的眼皮
            var lg = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(lg.GetComponent<Collider>()); lg.name = "Lids"; lg.transform.SetParent(camGO.transform, false); lg.transform.localScale = Vector3.one * .45f;
            var lmr = lg.GetComponent<MeshRenderer>(); lmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lmr.sharedMaterial = DLDArt.Mat("K_lids", "DLD/Lids");
            var lids = lg.AddComponent<Lids>(); lids.view = lmr;

            // 坠落时相机在井里，井的模板要铺满全屏
            var fm = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(fm.GetComponent<Collider>()); fm.name = "FallMask"; fm.transform.SetParent(camGO.transform, false); fm.transform.localScale = Vector3.one * .6f;
            var fmr = fm.GetComponent<MeshRenderer>(); fmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fmr.sharedMaterial = DLDArt.Mat("K_maskFull", "DLD/Mask", m => m.SetFloat("_Cull", 0)); fm.SetActive(false);
            // 裂隙里面：相机上的一个球，按视线方向算色
            var rs = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(rs.GetComponent<Collider>()); rs.name = "Rift"; rs.transform.SetParent(camGO.transform, false); rs.transform.localScale = Vector3.one * .5f;
            var rsr = rs.GetComponent<MeshRenderer>(); rsr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; rsr.receiveShadows = false;
            rsr.sharedMaterial = DLDArt.Mat("K_rift", "DLD/Rift", m => m.SetFloat("_Fade", 0)); rsr.enabled = false;

            var play = new GameObject("Play").transform; play.SetParent(world, false);
            var root = play.gameObject.AddComponent<GameRoot>(); root.lids = lids; root.cam = cam; root.rig = origin; root.fallMask = fm;
            root.rift = play.gameObject.AddComponent<RiftRoom>(); root.rift.view = rsr;
            var ei = play.gameObject.AddComponent<EditorInput>(); ei.cam = cam;
            var pi = play.gameObject.AddComponent<PicoInput>(); pi.cam = cam; pi.origin = origin;
            var bi = play.gameObject.AddComponent<BotInput>(); bi.cam = cam; bi.root = root; bi.enabled = false;
            var sw = play.gameObject.AddComponent<InputSwitch>(); sw.root = root; sw.editor = ei; sw.pico = pi; sw.bot = bi;

            root.tiles = GameObject.Find("Floor").AddComponent<TileField>(); root.tiles.rimMain = GameObject.Find("FloorRim"); root.tiles.rimSmall = GameObject.Find("FloorRimSmall"); root.tiles.rimTiny = GameObject.Find("FloorRimTiny"); root.tiles.mask = GameObject.Find("ShaftMask").transform;
            root.tiles.dust = BuildBreakDust(play);
            root.shards = Child<ShardField>(play, "Shards"); root.shards.prefab = Load("Shard.prefab");
            root.figures = Child<FigureField>(play, "Figures"); root.figures.prefab = Load("Figure.prefab");
            root.hands = Child<HandField>(play, "Hands"); root.hands.prefab = Load("Tentacle.prefab").GetComponent<Tentacle>();
            root.deepArms = Child<DeepArms>(play, "DeepArms"); root.deepArms.prefab = Load("Tentacle.prefab").GetComponent<Tentacle>();

            var ev = Child<EyeView>(play, "EyeView");
            var eye = (GameObject)PrefabUtility.InstantiatePrefab(Load("Eye.prefab"), ev.transform); eye.SetActive(false);
            ev.eye = eye.transform; ev.look = look; root.eye = ev;

            root.portal = BuildPortal(play, look);
            root.lights = Child<LightDirector>(play, "Lights"); root.lights.look = look;
            root.fade = Child<ScreenFade>(play, "Fade"); root.fade.view = sr;
            var hands = Child<PlayerHands>(play, "PlayerHands"); hands.rig = origin; hands.hole = DLDArt.Mat("K_handHole", "DLD/HandHole"); root.playerHands = hands;
            root.sound = Child<AudioDirector>(play, "Sound"); root.lights.sound = root.sound;
            var clips = new System.Collections.Generic.List<AudioClip>();
            foreach (var g in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/_DLD/Audio" })) clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g)));
            root.sound.clips = clips.ToArray();
            var hums = new System.Collections.Generic.List<Transform>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include)) if (l.name == "BakeGlow") hums.Add(l.transform);
            root.sound.humPoints = hums.ToArray();

            // 三个版本
            root.mr = mr;
            var hosp = new System.Collections.Generic.List<GameObject>();   // 透视模式藏掉建筑，地砖要留着
            foreach (Transform c in GameObject.Find("Hospital").transform) if (c.name != "Floor") hosp.Add(c.gameObject);
            root.hospital = hosp.ToArray();
            var dim = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(dim.GetComponent<Collider>()); dim.name = "Dim"; dim.transform.SetParent(camGO.transform, false); dim.transform.localScale = Vector3.one * 100;
            var dr = dim.GetComponent<MeshRenderer>(); dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; dr.sharedMaterial = DLDArt.Mat("K_dim", "DLD/Dim"); dim.SetActive(false);
            root.dim = dim; root.lights.dim = dr;
            root.cast = BuildCast(play);
            // 危险方向的小箭头：挂在相机上，总在视野边缘
            var ar = Child<DangerArrows>(camGO.transform, "DangerArrows"); ar.cam = camGO.transform; root.arrows = ar;
            var tq = GameObject.CreatePrimitive(PrimitiveType.Quad); var qm = tq.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(tq);
            var arMat = DLDArt.Mat("K_uiTop", "DLD/UI", mt => { mt.SetTexture("_BaseMap", Tex("arrow")); mt.SetFloat("_ZT", 8); mt.renderQueue = 3900; });
            ar.pool = Enumerable.Range(0, 3).Select(i => { var g = new GameObject("A" + i); g.transform.SetParent(ar.transform, false); g.transform.localScale = new Vector3(.062f, .046f, 1); g.AddComponent<MeshFilter>().sharedMesh = qm; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = arMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return r; }).ToArray();
            BuildUI(root, world);
        }

        const string UI = "Assets/_DLD/Art/UI/";
        static Texture2D Tex(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(UI + n + ".png");

        static readonly Color Bone = new Color(.97f, .93f, .78f, 1), Dim = new Color(.72f, .69f, .58f, 1), Ember = new Color(.9f, .34f, .22f, 1);

        /// <summary>一个界面元素。文字尺寸只由贴图决定（UILang 按统一比例换算），这里不存在“把字拉到多宽”这种参数</summary>
        static UIItem Q(Transform p, string name, string zh, string en, Vector3 pos, float delay, int align = 0, float scale = 1, Color? color = null, int sweep = 0, float dur = .9f, float shadow = .7f, Vector2 size = default, int order = 2)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(g.GetComponent<Collider>()); g.name = name;
            g.transform.SetParent(p, false);
            var r = g.GetComponent<MeshRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.sharedMaterial = DLDArt.Mat("K_ui", "DLD/UI"); r.sortingOrder = order;
            var it = g.AddComponent<UIItem>(); it.r = r; it.delay = delay; it.dur = dur; it.sweep = sweep; it.shadow = shadow; it.tint = color ?? Bone;
            var l = g.AddComponent<UILang>(); l.zh = Tex(zh); l.en = en != null ? Tex(en) : null; l.anchor = pos; l.align = align; l.scale = scale; l.size = size;
            l.Apply();
            return it;
        }
        static UIItem T(Transform p, string name, string key, Vector3 pos, float delay, int align = 0, float scale = 1, Color? color = null, float dur = .9f) => Q(p, name, key + "_zh", key + "_en", pos, delay, align, scale, color, 0, dur);
        static UIItem Shape(Transform p, string name, string tex, Vector3 pos, Vector2 size, float delay, int sweep = 1, Color? color = null, float dur = .9f, float shadow = 0, int order = 2) => Q(p, name, tex, null, pos, delay, 0, 1, color, sweep, dur, shadow, size, order);

        static UIButton Btn(Transform p, string name, Vector3 pos, Vector2 size) { var g = new GameObject(name); g.transform.SetParent(p, false); g.transform.localPosition = pos; var b = g.AddComponent<UIButton>(); b.size = size; return b; }
        static T1 Screen<T1>(Transform p, string name) where T1 : Component { var g = new GameObject(name); g.transform.SetParent(p, false); return g.AddComponent<T1>(); }

        /// <summary>手边的面板：离眼睛约 46 厘米，朝玩家斜 22°，手一抬就够得到。子物体用和远处一样的尺寸单位，整体缩小</summary>
        static Transform Near(Transform p, string name, float y)
        {
            var n = new GameObject(name).transform; n.SetParent(p, false);
            n.SetLocalPositionAndRotation(new Vector3(0, y, -1.14f), Quaternion.Euler(22, 0, 0)); n.localScale = Vector3.one * .48f;
            return n;
        }

        static int padSeed;
        /// <summary>按钮 = 背光胶片 + 蜡笔字 + 选中时手画的圈。每张都有一点歪，不是对齐排出来的</summary>
        static UIButton Pad(Transform p, string name, string zh, string en, string kind, Vector3 pos, Vector2 size, float delay, Color? col = null, bool variants = true)
        {
            int k = padSeed++; var b = Btn(p, name, pos, size); b.seed = k;
            var r = new System.Random(k * 31 + 7);
            b.transform.localRotation = Quaternion.Euler(0, 0, (float)(r.NextDouble() - .5) * 1.2f);
            b.back = Shape(b.transform, "Film", "f_" + kind + (variants ? (k % 3).ToString() : "0"), Vector3.zero, size, delay, 0, Color.white, 1f, 0, 1); b.back.rgb = true;
            var txt = Q(b.transform, "Text", zh, en, Vector3.zero, delay + .12f, 0, 1, col, 0, .9f, .55f, default, 3);
            var lang = txt.GetComponent<UILang>(); lang.fit = new Vector2(size.x * .78f, size.y * .58f); lang.Apply();      // 四周至少留 11% 和 21% 的白
            b.parts = new[] { txt };
            b.mark = Shape(b.transform, "Mark", "mk_" + kind, Vector3.zero, size, .1f, 2, Ember, .5f, 0, 4);
            return b;
        }
        static UIButton Pad(Transform p, string name, string key, string kind, Vector3 pos, Vector2 size, float delay, Color? col = null, bool variants = true) => Pad(p, name, key + "_zh", key + "_en", kind, pos, size, delay, col, variants);

        /// <summary>做旧的立体标题：前面一层有材质的字，后面叠几层深色的字，双眼看得出厚度</summary>
        static void Title(Transform p)
        {
            Shape(p, "Aura", "veil", new Vector3(0, .02f, .05f), new Vector2(1.35f, .85f), .1f, 0, new Color(.16f, .22f, .24f, .34f), 2f, 0, 1);
            for (int i = 6; i >= 1; i--)
            {
                float k = i / 6f;
                var d = Q(p, "Depth" + i, "title_zh", "title_en", new Vector3(0, -.0012f * i, .0055f * i), .15f + k * .12f, 0, 1, new Color(.2f - k * .15f, .205f - k * .15f, .2f - k * .15f, 1), 0, 1.7f, 0, default, 2);
                d.r.sortingOrder = 2 + 6 - i;
            }
            var f = Q(p, "Face", "title_tex_zh", "title_tex_en", Vector3.zero, .15f, 0, 1, Color.white, 0, 1.7f, .55f, default, 9); f.rgb = true;
        }

        static void BuildUI(GameRoot root, Transform world)
        {
            UISizes.Bake();      // 先记下界面贴图的原始尺寸，下面摆放文字要用
            var ui = new GameObject("UI").transform; ui.SetParent(world, false);
            var f = ui.gameObject.AddComponent<UIFlow>(); f.root = root; f.sound = root.sound; root.flowUI = f;
            var veilCol = new Color(0, 0, 0, .86f);

            // 开场封面：挡住场景的黑球 + 一张大图
            var bg = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(bg.GetComponent<Collider>()); bg.name = "CoverBack"; bg.transform.SetParent(root.cam.transform, false); bg.transform.localScale = Vector3.one * .55f;
            f.coverBack = bg.GetComponent<MeshRenderer>(); f.coverBack.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            f.coverBack.sharedMaterial = DLDArt.Mat("K_coverBack", "DLD/Fade", mt => { mt.SetColor("_Color", Color.black); mt.renderQueue = 4010; });
            f.cover = Screen<UIScreen>(ui, "Cover");
            var ci = Q(f.cover.transform, "Art", "cover_zh", "cover_en", Vector3.zero, .15f, 0, 1, Color.white, 0, 1.3f, 0, new Vector2(3.5f, 1.97f)); ci.rgb = true;
            ci.r.sharedMaterial = DLDArt.Mat("K_uiCover", "DLD/UI", mt => { mt.SetFloat("_ZT", 8); mt.renderQueue = 4020; });

            // 安全须知：一张大胶片，标题居中，四条上下排开，每条图形和文字各自对齐
            f.safety = Screen<UIScreen>(ui, "Safety"); var s = f.safety.transform;
            Shape(s, "Veil", "veil", new Vector3(0, 0, .55f), new Vector2(4.4f, 3f), 0, 0, veilCol, .8f, 0, 0);
            var sheet = Shape(s, "Sheet", "f_sheet", new Vector3(0, .09f, .02f), new Vector2(1.24f, .88f), .05f, 0, Color.white, 1.4f, 0, 1); sheet.rgb = true;
            T(s, "Title", "s_title", new Vector3(0, .42f, 0), .3f, 0, .86f, Bone, 1.3f);
            Shape(s, "Rule", "line", new Vector3(0, .335f, 0), new Vector2(.9f, .022f), .7f, 1, Dim, 1.1f);
            string[] ic = { "h_move", "s_space", "s_dizzy", "s_warn" };
            for (int i = 0; i < 4; i++)
            {
                float y = .24f - i * .135f, d = 1f + i * .28f;
                Q(s, "Icon" + i, "ic_" + ic[i], null, new Vector3(-.47f, y, 0), d, 0, .5f, i == 0 ? Ember : Bone);
                T(s, "Row" + i, "s_r" + i, new Vector3(-.39f, y, 0), d + .12f, 1, 1, i == 0 ? Ember : Bone);
            }
            f.capSit = T(s, "CapSit", "c_sit", new Vector3(0, -.42f, 0), 0, 0, .9f, Ember, .4f);
            f.nearSafety = Near(s, "Near", -.16f);
            f.safetyGo = Pad(f.nearSafety, "Go", "s_go", "go", new Vector3(.16f, 0, 0), new Vector2(.62f, .16f), 0, Bone, false);
            f.countdown = Shape(f.nearSafety, "Count", "line", new Vector3(.16f, -.115f, 0), new Vector2(.5f, .024f), 0, 1, Ember, .4f);
            f.safetyLang = Pad(f.nearSafety, "Lang", "lang_zh", "lang_en", "btn", new Vector3(-.325f, 0, 0), new Vector2(.293f, .13f), 1.6f);

            // 标题页：远处只有标题，手边只有三个按钮。关卡和设置收进下一页
            f.menuRoot = new GameObject("MenuRoot").transform; f.menuRoot.SetParent(ui, false);
            f.menu = Screen<UIScreen>(f.menuRoot, "Head"); var m = f.menu.transform;
            Shape(m, "Veil", "veil", new Vector3(0, 0, .55f), new Vector2(4.4f, 3f), 0, 0, veilCol, .9f, 0, 0);
            f.titleRoot = new GameObject("TitleRoot").transform; f.titleRoot.SetParent(m, false); f.titleRoot.localPosition = new Vector3(0, .32f, .24f);
            Title(f.titleRoot);
            T(m, "Sub", "sub", new Vector3(0, .12f, .08f), .9f, 0, .78f, new Color(.9f, .34f, .22f, 1), 1.2f);
            f.nearMenu = Near(f.menuRoot, "Near", -.17f);
            f.pageMain = Screen<UIScreen>(f.nearMenu, "Main"); var pm = f.pageMain.transform;
            // 标题读完才出现
            f.beginBtn = Pad(pm, "Begin", "begin", "start", new Vector3(0, .09f, 0), new Vector2(.36f, .16f), 2.9f, Bone, false);
            f.langBtn = Pad(pm, "Lang", "lang_zh", "lang_en", "btn", new Vector3(-.1565f, -.09f, 0), new Vector2(.293f, .13f), 3.1f);
            f.lessonBtn = Pad(pm, "Lesson", "b_lesson", "btn", new Vector3(0, -.09f, 0), new Vector2(.293f, .13f), 3.2f);
            f.quitBtn = Pad(pm, "Quit", "b_quit", "btn", new Vector3(.1565f, -.09f, 0), new Vector2(.293f, .13f), 3.3f);

            // 开始之后才选：难度、范围，走过的话还有楼层
            f.pageOpts = Screen<UIScreen>(f.nearMenu, "Opts"); var po = f.pageOpts.transform;
            // 所有行用同一个网格：左右边线对齐，间距统一
            const float L = -.42f, W = .92f, gap = .02f, lab = L - .01f; float rowM = .33f, rowA = .13f, rowF = -.06f, rowB = -.44f;
            float Cell(int i, int n) => L + (W - gap * (n - 1)) / n * (i + .5f) + gap * i;
            float wM = (W - gap * 2) / 3, wA = (W - gap * 3) / 4, wF = (W - gap * 7) / 8;
            T(po, "LMode", "l_mode", new Vector3(lab, rowM, 0), 2.05f, -1, .8f, Dim);
            T(po, "LArea", "l_area", new Vector3(lab, rowA, 0), 2.1f, -1, .8f, Dim);
            var lf = T(po, "LFloor", "l_floor", new Vector3(lab, rowF, 0), 2.15f, -1, .8f, Dim);
            f.modes = new UIButton[3];
            for (int i = 0; i < 3; i++) f.modes[i] = Pad(po, "Mode" + i, "mode" + i, "mode", new Vector3(Cell(i, 3), rowM, 0), new Vector2(wM, .13f), 2.05f + i * .05f);
            f.areas = new UIButton[4];
            for (int i = 0; i < 4; i++) f.areas[i] = Pad(po, "Area" + i, "area" + i, "area", new Vector3(Cell(i, 4), rowA, 0), new Vector2(wA, .13f), 2.1f + i * .05f);
            f.capModes = new UIItem[3]; for (int i = 0; i < 3; i++) f.capModes[i] = T(po, "CapM" + i, "c_m" + i, new Vector3(L - .02f, rowM - .098f, 0), 0, 1, .75f, Dim, .35f);
            var capPos = new Vector3(L - .02f, rowA - .098f, 0);
            f.capArea = T(po, "CapArea", "c_area", capPos, 2.3f, 1, .75f, Dim);
            f.capNofit = T(po, "CapNofit", "c_nofit", capPos, 0, 1, .75f, Ember, .35f);
            f.capSmall = T(po, "CapSmall", "c_small", capPos, 0, 1, .75f, Ember, .35f);
            f.capBound = T(po, "CapBound", "c_bound", capPos, 0, 1, .75f, Ember, .35f);
            f.floors = new UIButton[12]; f.pips = new UILang[12];
            int nF = 12, nPerRow = 6; float wFF = (W - gap * (nPerRow - 1)) / nPerRow;
            for (int i = 0; i < nF; i++)
            {
                int row = i / nPerRow, col = i % nPerRow, nThisRow = Mathf.Min(nPerRow, nF - row * nPerRow);
                float y = rowF - row * .18f;
                f.floors[i] = Pad(po, "Floor" + (i + 1), "n" + (i + 1), null, "floor", new Vector3(Cell(col, nThisRow), y, 0), new Vector2(wFF, .12f), 2.15f + i * .025f);
                f.pips[i] = Q(po, "Pip" + i, "pip0", null, new Vector3(Cell(col, nThisRow), y - .076f, 0), 2.25f + i * .025f, 0, .5f, Dim).GetComponent<UILang>();
            }
            f.pipTex = new[] { Tex("pip0"), Tex("pip1"), Tex("pip2"), Tex("pip3") };
            var fr = new System.Collections.Generic.List<GameObject> { lf.gameObject }; foreach (var b0 in f.floors) fr.Add(b0.gameObject); foreach (var p0 in f.pips) fr.Add(p0.gameObject); f.floorRow = fr.ToArray();
            f.rowB = new GameObject("RowB").transform; f.rowB.SetParent(po, false);
            f.backBtn = Pad(f.rowB, "Back", "b_back", "btn", new Vector3(Cell(0, 3), rowB, 0), new Vector2(wM, .13f), 2.3f);
            f.boundBtn = Pad(f.rowB, "Bound", "b_bound", "btn", new Vector3(Cell(1, 3), rowB, 0), new Vector2(wM, .13f), 2.33f);
            f.enterBtn = Pad(f.rowB, "Enter", "b_enter", "btn", new Vector3(Cell(2, 3), rowB, 0), new Vector2(wM, .13f), 2.36f, Bone);

            // 暂停：游戏中的小胶片和暂停面板
            f.pauseChip = Screen<UIScreen>(ui, "PauseChip");
            var pc = new GameObject("Scale").transform; pc.SetParent(f.pauseChip.transform, false); pc.localScale = Vector3.one * .26f;      // 贴在手背上，比手掌略小
            f.pauseBtn = Pad(pc, "Pause", "b_pause", "btn", Vector3.zero, new Vector2(.293f, .13f), .1f);
            f.pauseMenu = Screen<UIScreen>(ui, "PauseMenu"); var pm2 = f.pauseMenu.transform;
            Shape(pm2, "Veil", "veil", new Vector3(0, 0, .55f), new Vector2(4.4f, 3f), 0, 0, new Color(0, 0, 0, .9f), .5f, 0, 0);
            T(pm2, "Title", "pause_title", new Vector3(0, .22f, .08f), .1f, 0, .9f, Bone, .6f);
            var pn = Near(pm2, "Near", -.17f);
            f.resumeBtn = Pad(pn, "Resume", "b_resume", "start", new Vector3(-.2f, 0, 0), new Vector2(.36f, .16f), .25f, Bone, false);
            f.toTitleBtn = Pad(pn, "ToTitle", "b_title", "start", new Vector3(.2f, 0, 0), new Vector2(.36f, .16f), .3f, null, false);
            // 收集进度：一排手绘小灯，最多 8 格
            f.lampBar = Screen<UIScreen>(ui, "LampBar"); var lb = f.lampBar.transform;
            var lbBack = Shape(lb, "Back", "veil", new Vector3(0, 0, .01f), new Vector2(.82f, .15f), 0, 0, new Color(0, 0, 0, .55f), .4f, 0, 1);      // 现在摆在 1.2 米外，按距离放大
            var tq2 = GameObject.CreatePrimitive(PrimitiveType.Quad); var qm2 = tq2.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(tq2);
            var lampMat = DLDArt.Mat("K_uiTop", "DLD/UI");
            f.lampSlots = Enumerable.Range(0, 12).Select(i => { var g = new GameObject("Lamp" + i); g.transform.SetParent(lb, false); g.AddComponent<MeshFilter>().sharedMesh = qm2; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = lampMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.enabled = false; return r; }).ToArray();
            // 手背上的那份进度：很小，跟着暂停按钮
            f.wristLamps = Screen<UIScreen>(ui, "WristLamps"); var wl = f.wristLamps.transform;
            Shape(wl, "Back", "veil", new Vector3(0, 0, .002f), new Vector2(.2f, .036f), 0, 0, new Color(0, 0, 0, .55f), .3f, 0, 1);
            f.wristSlots = Enumerable.Range(0, 12).Select(i => { var g = new GameObject("Lamp" + i); g.transform.SetParent(wl, false); g.AddComponent<MeshFilter>().sharedMesh = qm2; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = lampMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.enabled = false; return r; }).ToArray();
            f.lampOff = Tex("lamp_off"); f.lampOn = Tex("lamp_on");
            f.goalZh = Enumerable.Range(1, 12).Select(n => Tex($"goal_n{n}_zh")).ToArray(); f.goalEn = Enumerable.Range(1, 12).Select(n => Tex($"goal_n{n}_en")).ToArray();
            f.goalDoneZh = Tex("goal_done_zh"); f.goalDoneEn = Tex("goal_done_en");

            // 结局：被阳光照醒回到真实世界。这里搭字卡、两个按钮、日晕面片和环境音
            var en = Child<Ending>(ui, "Ending"); root.ending = en;
            en.card = Screen<UIScreen>(en.transform, "Card"); var ec = en.card.transform;
            T(ec, "Line1", "end_l1", new Vector3(0, .2f, .04f), .2f, 0, .9f, Bone, 1.4f);
            T(ec, "Line2", "end_l2", new Vector3(0, .07f, .04f), 1.4f, 0, .78f, Dim, 1.4f);
            var en2 = Near(ec, "Near", -.17f);
            en.titleBtn = Pad(en2, "Title", "b_title", "start", new Vector3(-.2f, 0, 0), new Vector2(.36f, .16f), 2.6f, Bone, false);
            en.quitBtn = Pad(en2, "Quit", "b_quit", "start", new Vector3(.2f, 0, 0), new Vector2(.36f, .16f), 2.75f, null, false);
            // 太阳和日晕：套在相机外面的一个大球（半径 10 米），颜色按视线方向算，像天空一样在无穷远；手举起来能挡住它
            var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(halo.GetComponent<Collider>()); halo.name = "SunSky"; halo.transform.SetParent(root.cam.transform, false); halo.transform.localScale = Vector3.one * 20;
            var hr = halo.GetComponent<MeshRenderer>(); hr.sharedMaterial = DLDArt.Mat("K_sunHalo", "DLD/SunSky", m => { m.SetFloat("_Fade", 0); m.SetFloat("_Ring", 10 * Mathf.Deg2Rad); });
            hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; hr.receiveShadows = false; hr.enabled = false; en.halo = hr;
            // 自然环境音
            var endAmb = en.gameObject.AddComponent<AudioSource>(); endAmb.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_DLD/Audio/Sfx/end_dawn.wav");
            endAmb.loop = true; endAmb.playOnAwake = false; endAmb.spatialBlend = 0; endAmb.volume = 0; en.amb = endAmb;

            var noteRoot = new GameObject("HandsNote").transform; noteRoot.SetParent(ui, false);
            f.handsNote = T(noteRoot, "Text", "c_hands", Vector3.zero, 0, 0, .3f, Ember, .5f);

            // 游戏里的提示：一个白色图形和一行字
            f.hint = Screen<UIScreen>(ui, "Hint"); var h = f.hint.transform;
            f.tipIcon = Q(h, "Icon", "ic_h_step", null, new Vector3(0, .1f, 0), 0, 0, .68f); f.tipIconLang = f.tipIcon.GetComponent<UILang>();
            f.tipText = Q(h, "Text", "tip_h_step_zh", null, new Vector3(0, -.055f, 0), .22f); f.tipTextLang = f.tipText.GetComponent<UILang>();
            f.tipLine = Shape(h, "Rule", "line", new Vector3(0, -.125f, 0), new Vector2(.34f, .012f), .4f, 1, Ember, .8f);
            f.hintKeys = new[] { "h_step", "h_gaze", "h_over", "h_swat", "h_push", "h_eye", "h_tear", "h_move", "oob", "goal1", "goal2", "h_enter", "h_wrist" };
            f.tipIcons = f.hintKeys.Select(k => k == "oob" || k == "goal1" || k == "goal2" || k == "h_enter" || k == "h_wrist" ? null : Tex("ic_" + k)).ToArray();
            f.tipZh = f.hintKeys.Select(k => Tex($"tip_{k}_zh")).ToArray(); f.tipEn = f.hintKeys.Select(k => Tex($"tip_{k}_en")).ToArray();
            f.floorZh = Enumerable.Range(1, 12).Select(i => Tex($"floor{i}_zh")).ToArray(); f.floorEn = Enumerable.Range(1, 12).Select(i => Tex($"floor{i}_en")).ToArray();

            // 教学的地面标记
            var tm = Child<TutorialMarks>(ui, "Marks"); tm.cam = root.cam.transform; root.marks = tm;
            MeshRenderer RingQ(string n) { var g = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(g.GetComponent<Collider>()); g.name = n; g.transform.SetParent(tm.transform, false); g.transform.rotation = Quaternion.Euler(90, 0, 0); var r = g.GetComponent<MeshRenderer>(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.sharedMaterial = DLDArt.Mat("K_areaLine", "DLD/AreaLine"); return r; }
            tm.danger = RingQ("Danger"); tm.safe = RingQ("Safe");
            var aq = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(aq.GetComponent<Collider>()); aq.name = "Arrow"; aq.transform.SetParent(tm.transform, false); aq.transform.localScale = new Vector3(.3f, .23f, 1);
            tm.arrow = aq.GetComponent<MeshRenderer>(); tm.arrow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tm.arrow.sharedMaterial = DLDArt.Mat("K_uiTop", "DLD/UI");

            f.reticleRoot = new GameObject("Reticle").transform; f.reticleRoot.SetParent(ui, false); f.reticle = Shape(f.reticleRoot, "Ring", "ring", Vector3.zero, new Vector2(.11f, .11f), 0, 0, Ember, .12f);
            f.rippleRoot = new GameObject("Ripple").transform; f.rippleRoot.SetParent(ui, false); f.ripple = Shape(f.rippleRoot, "Ring", "ring", Vector3.zero, new Vector2(.2f, .2f), 0, 0, Ember, .01f);

            // 地上的活动范围线
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>()); q.name = "AreaLine"; q.transform.SetParent(ui, false);
            q.transform.SetLocalPositionAndRotation(new Vector3(0, .012f, 0), Quaternion.Euler(90, 0, 0)); q.transform.localScale = new Vector3(7, 7, 1);
            f.areaLine = q.GetComponent<MeshRenderer>(); f.areaLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            f.areaLine.sharedMaterial = DLDArt.Mat("K_areaLine", "DLD/AreaLine", mt => { mt.SetColor("_Color", new Color(1f, .78f, .52f)); mt.SetFloat("_Size", 7); });
        }

        static CastFx BuildCast(Transform play)
        {
            var c = Child<CastFx>(play, "Cast");
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad); var quad = tmp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(tmp);
            var mat = DLDArt.Mat("K_pulse", "DLD/Pulse", m => { m.SetColor("_Color", new Color(.5f, .62f, .76f)); m.SetFloat("_W", .035f); });
            MeshRenderer Part(string n) { var g = new GameObject(n); g.transform.SetParent(c.transform, false); g.AddComponent<MeshFilter>().sharedMesh = quad; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return r; }
            c.palm = Part("Palm");
            c.pool = Enumerable.Range(0, 8).Select(i => Part("Wave" + i)).ToArray();
            c.sparks = Bits(c.transform, "Sparks", "K_ember", "DLD/Mote_Open", new Color(.72f, .86f, 1f, .95f), 90, 0);
            c.ash = Bits(c.transform, "Ash", "K_ash", "DLD/Smoke", new Color(.05f, .05f, .06f, .75f), 90, .25f);
            c.dust = Bits(c.transform, "Dust", "K_castDust", "DLD/Smoke", new Color(.46f, .5f, .49f, .2f), 60, 0);
            return c;
        }

        static ParticleSystem Bits(Transform p, string name, string mat, string shader, Color col, int max, float gravity)
        {
            var g = new GameObject(name); g.transform.SetParent(p, false);
            var ps = g.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.startColor = col; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = max; main.gravityModifier = gravity;
            var em = ps.emission; em.enabled = false; var sh = ps.shape; sh.enabled = false;
            var drag = ps.limitVelocityOverLifetime; drag.enabled = true; drag.dampen = .08f; drag.limit = .1f;
            var cl = ps.colorOverLifetime; cl.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.8f, .4f), new GradientAlphaKey(0, 1) });
            cl.color = gr;
            g.GetComponent<ParticleSystemRenderer>().sharedMaterial = DLDArt.Mat(mat, shader);
            return ps;
        }

        static PortalView BuildPortal(Transform play, SceneLook look)
        {
            var dv = Child<PortalView>(play, "Portal"); dv.look = look;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad); var quad = tmp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(tmp);
            MeshRenderer Part(string n, Material m, Vector3 p, Quaternion r, Vector3 s)
            {
                var g = new GameObject(n); g.transform.SetParent(dv.transform, false); g.transform.SetLocalPositionAndRotation(p, r); g.transform.localScale = s;
                g.AddComponent<MeshFilter>().sharedMesh = quad; var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return mr;
            }
            dv.gate = Part("Gate", DLDArt.Mat("K_portal", "DLD/Portal", m => { m.SetTexture("_Pano", null); m.SetColor("_Rim", new Color(.82f, .42f, .17f)); m.SetColor("_Bleed", new Color(.62f, .3f, .11f)); m.SetFloat("_Open", 1); }), new Vector3(0, 1.22f, 0), Quaternion.Euler(0, 180, 0), new Vector3(1.5f, 2.4f, 1));
            dv.halo = Part("Halo", DLDArt.Mat("K_portalHalo", "DLD/Halo", m => { m.SetColor("_Color", new Color(.05f, .024f, .009f)); m.SetFloat("_Size", 1.5f); m.renderQueue = 3000; }), new Vector3(0, 1.22f, 0), Quaternion.identity, Vector3.one).transform;
            dv.pool = Part("Pool", DLDArt.Mat("K_portalPool", "DLD/Pool", m => m.SetColor("_Color", new Color(.075f, .036f, .013f))), new Vector3(0, .004f, 0), Quaternion.Euler(90, 0, 0), new Vector3(4.2f, 4.2f, 1)).transform;

            var eg = new GameObject("Embers"); eg.transform.SetParent(dv.transform, false); eg.transform.localPosition = new Vector3(0, 1.2f, 0);
            var ps = eg.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false; main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.5f); main.startSpeed = 0; main.maxParticles = 90;
            main.startSize = new ParticleSystem.MinMaxCurve(.008f, .026f); main.startColor = new Color(1f, .6f, .25f, .85f); main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 20;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(1.5f, 2.3f, .35f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(-.04f, .04f); vel.z = new ParticleSystem.MinMaxCurve(-.04f, .04f); vel.y = new ParticleSystem.MinMaxCurve(.05f, .26f);
            var nz = ps.noise; nz.enabled = true; nz.strength = .28f; nz.frequency = .9f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g2 = new Gradient(); g2.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, .45f, .15f), 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(0, 1) });
            col.color = g2;
            eg.GetComponent<ParticleSystemRenderer>().sharedMaterial = DLDArt.Mat("K_ember", "DLD/Mote_Open");
            dv.embers = ps;
            // 立体碎片：一块小的不规则四面体，暗面 + 被裂口烤亮的边
            var chipMesh = new Mesh { name = "RiftChip" };
            var cv = new[] { new Vector3(0, 1, .2f), new Vector3(.9f, -.5f, .4f), new Vector3(-.8f, -.6f, .5f), new Vector3(.1f, -.2f, -1) };
            int[] ct = { 0, 1, 2, 0, 3, 1, 1, 3, 2, 2, 3, 0 }; var v2 = new System.Collections.Generic.List<Vector3>(); var t2 = new System.Collections.Generic.List<int>();
            foreach (var ix in ct) { t2.Add(v2.Count); v2.Add(cv[ix]); }
            chipMesh.SetVertices(v2); chipMesh.SetTriangles(t2, 0); chipMesh.RecalculateNormals(); chipMesh.RecalculateTangents();
            var cp = "Assets/_DLD/Art/Models/Kit/RiftChip.asset"; AssetDatabase.DeleteAsset(cp); AssetDatabase.CreateAsset(chipMesh, cp);
            var chipMat = DLDArt.Mat("K_riftChip", "DLD/Lit", m => { m.SetColor("_BaseColor", new Color(.2f, .17f, .14f)); m.SetColor("_EmissionColor", new Color(.012f, .005f, .002f)); m.SetFloat("_MinLight", 0); m.SetFloat("_EmissLevel", 0); m.SetFloat("_FogK", 0); });
            dv.chips = Enumerable.Range(0, 26).Select(i => { var g = new GameObject("Chip" + i); g.transform.SetParent(dv.transform, false); g.AddComponent<MeshFilter>().sharedMesh = chipMesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = chipMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return g.transform; }).ToArray();
            dv.gameObject.SetActive(false);
            return dv;
        }

        static ParticleSystem BuildBreakDust(Transform parent)
        {
            var go = new GameObject("BreakDust"); go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.startLifetime = new ParticleSystem.MinMaxCurve(.5f, 1.3f); main.startSpeed = new ParticleSystem.MinMaxCurve(.15f, .5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.18f, .6f); main.startColor = new Color(.42f, .47f, .46f, .2f); main.gravityModifier = .1f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 200;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(.75f, .02f, .75f);
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = DLDArt.Mat("K_smoke", "DLD/Smoke");
            return ps;
        }
    }
}
