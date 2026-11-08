using DLD.Play;
// 搭建主场景：菜单 DLD/1 搭建场景；DLD/2 烘焙光照。
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DLD.EditorTools
{
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/_DLD/Scenes/Main.unity";
        const string Tex = "Assets/_DLD/Art/Textures/";

        static Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + n + ".png");

        static Material Lit(string name, string tex, string nrm, float tile, Color tint, float gloss, float rough, float nscale = 1)
            => DLDArt.Mat("K_" + name, "DLD/Lit", m =>
            {
                m.SetTexture("_BaseMap", tex != null ? T(tex) : Texture2D.whiteTexture);
                m.SetTextureScale("_BaseMap", Vector2.one * tile);
                if (nrm != null) m.SetTexture("_NormalMap", T(nrm));
                m.SetColor("_BaseColor", tint); m.SetFloat("_Gloss", gloss); m.SetFloat("_Rough", rough); m.SetFloat("_NormalScale", nscale);
                var mask = tex != null ? T(tex + "_m") : null;
                m.SetTexture("_MaskMap", mask ? mask : Texture2D.whiteTexture);
                m.SetFloat("_Cavity", mask ? .45f : 0); m.SetFloat("_AntiTile", mask ? 1 : 0); m.SetFloat("_Grime", mask ? 1 : 0);
            });

        static Material Shaft(string name, string tex, float tile, Color tint, float spec = 0, Color? glow = null)
            => DLDArt.Mat("S_" + name, "DLD/Shaft", m =>
            {
                m.SetTexture("_BaseMap", tex != null ? T(tex) : Texture2D.whiteTexture);
                m.SetTextureScale("_BaseMap", Vector2.one * tile);
                m.SetColor("_BaseColor", tint); m.SetFloat("_Spec", spec);
                m.SetFloat("_LampGlass", glow.HasValue ? 1 : 0);
                if (glow.HasValue) m.SetColor("_EmissionColor", glow.Value);
                m.enableInstancing = true;
            });

        static Dictionary<string, Material> Mats()
        {
            var d = new Dictionary<string, Material>
            {
                ["terrazzo"] = Lit("terrazzo", "terrazzo", "terrazzo_n", .33f, new Color(.62f, .65f, .63f), .3f, .16f, .75f),
                ["paint"] = Lit("paint", "wall_paint", "wall_paint_n", .33f, new Color(.78f, .8f, .76f), .1f, .5f, 1f),
                ["tile"] = Lit("tile", "wall_tile", "wall_tile_n", 1 / 1.2f, new Color(.72f, .76f, .75f), .3f, .16f, .8f),
                ["skirt"] = Lit("skirt", "steel", "steel_n", .5f, new Color(.42f, .45f, .44f), .25f, .35f, 1f),
                ["trim"] = Lit("trim", "steel", "steel_n", 1.6f, new Color(.62f, .68f, .65f), .22f, .35f, .5f),
                ["chrome"] = Lit("chrome", "steel", "steel_n", .8f, new Color(.95f, 1f, .98f), .6f, .22f, .8f),
                ["wood"] = Lit("wood", "door_wood", "door_wood_n", .6f, new Color(.85f, .82f, .78f), .22f, .3f, 1f),
                ["glass"] = Lit("glass", "ceiling", null, .9f, new Color(.05f, .06f, .06f), .7f, .08f),
                ["ceiling"] = Lit("ceiling", "ceiling", "ceiling_n", .33f, new Color(.78f, .82f, .78f), .04f, .7f, 1f),
                ["s_concrete"] = Shaft("concrete", "concrete", .33f, new Color(.72f, .82f, .84f)),
                ["s_steel"] = Shaft("steel", "steel", .5f, new Color(1.1f, 1.2f, 1.2f), .6f),
                ["s_pipe"] = Shaft("pipe", "steel", .5f, new Color(1.4f, 1.5f, 1.45f), 1f),
                ["s_cable"] = Shaft("cable", null, 1, new Color(.05f, .05f, .06f)),
                ["s_lamp"] = Shaft("lamp", null, 1, Color.white, 0, new Color(1.5f, .72f, .26f)),
            };
            d["slabTop"] = Lit("slabTop", "terrazzo", "terrazzo_n", .33f, new Color(.62f, .65f, .63f), .3f, .16f, .75f);
            foreach (var k in new[] { "terrazzo", "slabTop" }) { d[k].SetFloat("_CeilRefl", 7f); }
            // 网格贴图只能错开整数格，不能缩放
            d["tile"].SetFloat("_UV2Scale", 1); d["tile"].SetVector("_UV2Off", new Vector4(.5f, .25f, 0, 0));
            d["slabTop"].SetFloat("_SeepK", 1); d["slabTop"].SetFloat("_GridSize", KitBuilder.HW * 2 / 5f); d["slabTop"].SetFloat("_GridOff", .5f);
            d["terrazzo"].SetFloat("_Grid", .95f); d["terrazzo"].SetFloat("_GridSize", KitBuilder.HW * 2 / 5f); d["terrazzo"].SetFloat("_GridOff", .5f);
            d["void"] = DLDArt.Mat("K_void", "DLD/Lit", m => { m.SetColor("_BaseColor", Color.black); m.SetFloat("_Gloss", 0); });
            foreach (var k in new[] { "terrazzo", "slabTop" }) { d[k].SetFloat("_Cavity", .12f); d[k].SetFloat("_NormalScale", .35f); }
            d["ceiling"].SetFloat("_NormalScale", .55f);
            d["chrome"].SetColor("_MinLight", new Color(.34f, .42f, .41f)); d["glass"].SetColor("_MinLight", new Color(.5f, .6f, .58f));
            d["slabSide"] = Lit("slabSide", "concrete", "concrete_n", .8f, new Color(.42f, .45f, .44f), .02f, .7f, 1f);
            d["tubeOn"] = DLDArt.Mat("K_tubeOn", "DLD/Lit", m => { m.SetColor("_BaseColor", Color.black); m.SetColor("_EmissionColor", new Color(3.2f, 4.2f, 3.9f)); m.SetFloat("_LevelResponse", 1); m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; });
            d["tubeOff"] = DLDArt.Mat("K_tubeOff", "DLD/Lit", m => { m.SetColor("_BaseColor", new Color(.22f, .24f, .23f)); m.SetColor("_EmissionColor", Color.black); });
            d["tubeOn"].SetFloat("_EmissLevel", 1);
            d["exit"] = DLDArt.Mat("K_exit", "DLD/Lit", m => { m.SetColor("_BaseColor", Color.black); m.SetColor("_EmissionColor", new Color(.2f, .85f, .66f)); });
            AssetDatabase.SaveAssets();
            return d;
        }

        static GameObject Put(string name, Mesh mesh, Dictionary<string, Material> mats, Transform parent, Vector3 pos, Quaternion rot, bool isStatic, Dictionary<string, Material> over = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false); go.transform.SetLocalPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = MeshKit.Orders[mesh.name].Select(k => over != null && over.TryGetValue(k, out var o) ? o : mats[k]).ToArray();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (isStatic) GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
            return go;
        }

        [MenuItem("DLD/1 搭建场景")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mats = Mats();
            var look = new GameObject("SceneLook").AddComponent<SceneLook>();
            look.lampPowerA = 0; look.shaftTop = -.12f;
            look.lampRadius = KitBuilder.ShaftH - .13f;

            look.noise = BakeNoise(); look.wear = T("steel");
            var root = new GameObject("World").transform;
            var hallRoot = new GameObject("Hospital").transform; hallRoot.SetParent(root, false);
            var shaftRoot = new GameObject("Shaft").transform; shaftRoot.SetParent(root, false); shaftRoot.localPosition = new Vector3(0, -.12f, 0);

            // 竖井：近处 8 层完整，往下用简化版
            var near = new[] { KitBuilder.Shaft(0, true), KitBuilder.Shaft(1, true), KitBuilder.Shaft(2, true) };
            var far = KitBuilder.Shaft(0, false);
            for (int i = 0; i < KitBuilder.Levels; i++)
            {
                int v = new[] { 0, 1, 0, 2, 0, 0, 1 }[i % 7];
                bool isNear = i < 8;
                var mesh = isNear ? near[v] : far;
                var q = Quaternion.identity;
                Put("L" + i, mesh, mats, shaftRoot, new Vector3(0, -i * KitBuilder.FloorH, 0), q, false);
            }
            var bottom = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(bottom.GetComponent<Collider>());
            bottom.name = "Bottom"; bottom.transform.SetParent(shaftRoot, false);
            bottom.transform.SetLocalPositionAndRotation(new Vector3(0, -KitBuilder.Levels * KitBuilder.FloorH, 0), Quaternion.Euler(90, 0, 0));
            bottom.transform.localScale = Vector3.one * 8;
            bottom.GetComponent<Renderer>().sharedMaterial = mats["s_cable"];

            BuildAbyss(shaftRoot);

            // 井口遮罩
            var mask = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(mask.GetComponent<Collider>());
            mask.name = "ShaftMask"; mask.transform.SetParent(root, false);
            mask.transform.SetLocalPositionAndRotation(new Vector3(0, .001f, 0), Quaternion.Euler(90, 0, 0));
            mask.transform.localScale = Vector3.one * (KitBuilder.HoleHalf * 2);
            mask.GetComponent<Renderer>().sharedMaterial = DLDArt.Mat("K_mask", "DLD/Mask");

            // 大厅和四条走廊
            var hall = KitBuilder.Hall();
            Put("Hall", hall, mats, hallRoot, Vector3.zero, Quaternion.identity, true);
            var arms = new[] { KitBuilder.Arm(0), KitBuilder.Arm(1) };
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0, 90 * i, 0);
                Put("Arm" + i, arms[i % 2], mats, hallRoot, rot * new Vector3(0, 0, KitBuilder.HallHalf), rot, true);
            }

            BuildFloor(hallRoot, mats);

            // 灯盘：多数已坏，少数还亮
            var troffer = KitBuilder.Troffer(); var exit = KitBuilder.ExitBox();
            var lightsRoot = new GameObject("Lights").transform; lightsRoot.SetParent(hallRoot, false);
            var hazeMesh = KitBuilder.HazeCone();
            var hazeMat = DLDArt.Mat("K_haze", "DLD/Haze", m => { m.SetColor("_Color", new Color(.55f, .78f, .74f)); m.SetFloat("_Power", .035f); });
            var litLamps = new List<(Vector3 p, float yaw, float power)>();
            void Lamp(Vector3 p, float yaw, bool on, float power = 3f)
            {
                if (on) litLamps.Add((p, yaw, power));
                var over = new Dictionary<string, Material> { ["tube"] = on ? mats["tubeOn"] : mats["tubeOff"] };
                Put("Troffer", troffer, mats, lightsRoot, p, Quaternion.Euler(0, yaw, 0), false, over);
                if (!on) return;
                var l = new GameObject("Bake").AddComponent<Light>();
                l.transform.SetParent(lightsRoot, false);
                l.transform.SetLocalPositionAndRotation(p + Vector3.down * .13f, Quaternion.Euler(90, yaw, 0));
                l.type = LightType.Rectangle; l.areaSize = new Vector2(.6f, 1.2f);
                l.lightmapBakeType = LightmapBakeType.Baked; l.color = new Color(.86f, .96f, 1f); l.intensity = power * 8f; l.range = 16;
                // 灯管本身也朝四周散光：照亮周围天花和墙顶，不然灯像贴上去的
                var g = new GameObject("BakeGlow").AddComponent<Light>(); g.transform.SetParent(lightsRoot, false); g.transform.localPosition = p + Vector3.down * .85f;
                g.type = LightType.Point; g.lightmapBakeType = LightmapBakeType.Baked; g.color = new Color(.8f, .95f, .92f); g.intensity = Mathf.Min(power, 6f) * .3f; g.range = 8f;
                var haze = new GameObject("Haze"); haze.transform.SetParent(lightsRoot, false);
                haze.transform.SetLocalPositionAndRotation(p + Vector3.down * .15f, Quaternion.Euler(0, yaw, 0));
                haze.AddComponent<MeshFilter>().sharedMesh = hazeMesh;
                var hr = haze.AddComponent<MeshRenderer>(); hr.sharedMaterial = hazeMat; hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            float S = KitBuilder.HallHalf, hy = KitBuilder.HallH - .02f;
            // 大厅只留两盏对角的灯，光池之间让它暗下去；中央的地砖两盏都照得到
            Lamp(new Vector3(-1.625f, hy, -1.625f), 0, false); Lamp(new Vector3(1.625f, hy, -1.625f), 0, true, 12f);
            Lamp(new Vector3(-1.625f, hy, 1.625f), 0, true, 9f); Lamp(new Vector3(1.625f, hy, 1.625f), 0, false);
            Lamp(new Vector3(-4.875f, hy, 1.625f), 90, true, 3f); Lamp(new Vector3(4.875f, hy, -1.625f), 90, false);
            Lamp(new Vector3(1.625f, hy, 4.875f), 0, false); Lamp(new Vector3(-1.625f, hy, -4.875f), 0, false);
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0, 90 * i, 0);
                for (int b = 0; b < KitBuilder.ArmBays; b++)
                {
                    bool on = (i, b) is (0, 1) or (1, 2) or (2, 0) or (2, 4) or (3, 3) or (0, 4);
                    Lamp(rot * new Vector3(0, KitBuilder.ArmH - .02f, S + (b + .5f) * KitBuilder.Bay), 90 * i, on, 3.2f);
                }
                // 出口灯
                var ex = Put("Exit", exit, mats, lightsRoot, rot * new Vector3(0, KitBuilder.ArmH - .4f, S + KitBuilder.ArmBays * KitBuilder.Bay - .01f), rot * Quaternion.Euler(0, 180, 0), false, new Dictionary<string, Material> { ["tube"] = mats["exit"] });
                var el = new GameObject("BakeExit").AddComponent<Light>(); el.transform.SetParent(lightsRoot, false);
                el.transform.localPosition = rot * new Vector3(0, KitBuilder.ArmH - .5f, S + KitBuilder.ArmBays * KitBuilder.Bay - .3f);
                el.type = LightType.Point; el.lightmapBakeType = LightmapBakeType.Baked; el.color = new Color(.45f, 1, .85f); el.intensity = .55f; el.range = 5;
            }

            look.ceilMap = BakeCeilMap(litLamps, look.ceilHalf);
            BuildProps(hallRoot);
            BuildWear(hallRoot, mats);
            BuildDust(hallRoot);
            BuildActors();
            BuildFigurePrefab();
            PlayBuilder.Build(root, look);

            // 光照探针：大厅网格 + 每条走廊一串
            var lp = new GameObject("LightProbes").AddComponent<LightProbeGroup>(); lp.transform.SetParent(hallRoot, false);
            var pts = new List<Vector3>();
            foreach (var y in new[] { .25f, 1.3f, 2.6f, 3.9f })
            {
                for (float x = -6f; x <= 6.01f; x += 2f) for (float z = -6f; z <= 6.01f; z += 2f) pts.Add(new Vector3(x, y, z));
                for (float d = 7.5f; d < 30f; d += 2.5f) { pts.Add(new Vector3(0, y, d)); pts.Add(new Vector3(0, y, -d)); pts.Add(new Vector3(d, y, 0)); pts.Add(new Vector3(-d, y, 0)); }
            }
            lp.probePositions = pts.ToArray();

            // 反射探针（大厅）
            var probe = new GameObject("Reflection").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(hallRoot, false); probe.transform.localPosition = new Vector3(0, 2.25f, 0);
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked; probe.size = new Vector3(13.2f, 4.8f, 13.2f); probe.boxProjection = true; probe.resolution = 256; probe.hdr = true;
            probe.center = new Vector3(0, .1f, 0);

            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0, 90 * i, 0);
                var ap = new GameObject("ReflectionArm" + i).AddComponent<ReflectionProbe>();
                ap.transform.SetParent(hallRoot, false);
                ap.transform.localPosition = rot * new Vector3(0, 1.75f, KitBuilder.HallHalf + KitBuilder.ArmBays * KitBuilder.Bay * .5f);
                bool ns = i % 2 == 0;
                ap.size = ns ? new Vector3(3.2f, 3.5f, 24f) : new Vector3(24f, 3.5f, 3.2f);
                ap.mode = UnityEngine.Rendering.ReflectionProbeMode.Baked; ap.boxProjection = true; ap.resolution = 256; ap.hdr = true;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.011f, .017f, .018f);
            RenderSettings.skybox = null; RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"场景搭建完成 大厅三角形 {hall.triangles.Length / 3} 走廊 {arms[0].triangles.Length / 3} 竖井 {near[0].triangles.Length / 3}/{far.triangles.Length / 3}");
        }

        /// <summary>可平铺的噪声：R 是云雾，G 是更碎的一层。雾和传送门共用</summary>
        static Texture2D BakeNoise()
        {
            const int N = 256; var px = new byte[N * N * 2];
            float Layer(int x, int y, int cells, int seed)
            {
                float Hash(int i, int j) { i = (i % cells + cells) % cells; j = (j % cells + cells) % cells; float h = Mathf.Sin(i * 127.1f + j * 311.7f + seed * 74.7f) * 43758.5453f; return h - Mathf.Floor(h); }
                float fx = x * (float)cells / N, fy = y * (float)cells / N; int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
                float tx = fx - ix, ty = fy - iy; tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), tx), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), tx), ty);
            }
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float r = 0, g = 0, amp = .5f;
                    for (int o = 0; o < 5; o++) { r += Layer(x, y, 4 << o, 1 + o) * amp; g += Layer(x, y, 7 << o, 11 + o) * amp; amp *= .5f; }
                    px[(y * N + x) * 2] = (byte)(Mathf.Clamp01(r * 1.03f) * 255); px[(y * N + x) * 2 + 1] = (byte)(Mathf.Clamp01(g * 1.03f) * 255);
                }
            var tex = new Texture2D(N, N, TextureFormat.RG16, true, true) { name = "Noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            tex.SetPixelData(px, 0); tex.Apply(true, false);
            var path = KitBuilder.Dir + "Noise.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        /// <summary>深渊的体积感：越往下雾越浓直到全黑，飞尘慢慢往上飘</summary>
        static void BuildAbyss(Transform shaft)
        {
            float R = KitBuilder.RW - .02f;
            var v = new List<Vector3>(); var c = new List<Color>(); var uv = new List<Vector2>(); var t = new List<int>();
            // 从最深的开始排，相机永远在上面，这样不用排序
            var layers = new[] { (-92f, .98f), (-66f, .94f), (-46f, .82f), (-31f, .62f), (-21f, .46f), (-13.5f, .34f), (-8f, .24f), (-4.2f, .14f) };
            for (int i = 0; i < layers.Length; i++)
            {
                var (y, a) = layers[i]; int b = v.Count;
                v.AddRange(new[] { new Vector3(-R, y, -R), new Vector3(R, y, -R), new Vector3(R, y, R), new Vector3(-R, y, R) });
                float warm = Mathf.Clamp01((-y - 12) / 40f) * (1 - Mathf.Clamp01((-y - 50) / 40f));
                var col = Color.Lerp(new Color(.018f, .034f, .038f), new Color(.05f, .028f, .016f), warm); col.a = a;
                for (int k = 0; k < 4; k++) { c.Add(col); uv.Add(new Vector2(1f + i * .19f, 0)); }
                t.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var m = new Mesh { name = "AbyssMist" }; m.SetVertices(v); m.SetColors(c); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.bounds = new Bounds(Vector3.down * 50, new Vector3(10, 110, 10));
            var path = KitBuilder.Dir + "AbyssMist.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path);
            var mist = new GameObject("Mist"); mist.transform.SetParent(shaft, false); mist.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = mist.AddComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.sharedMaterial = DLDArt.Mat("K_mist", "DLD/Mist");

            var go = new GameObject("Motes"); go.transform.SetParent(shaft, false); go.transform.localPosition = new Vector3(0, -16, 0);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = new ParticleSystem.MinMaxCurve(18, 30); main.startSpeed = 0; main.maxParticles = 260;
            main.startSize = new ParticleSystem.MinMaxCurve(.02f, .075f); main.startColor = new ParticleSystem.MinMaxGradient(new Color(.5f, .62f, .6f, .5f), new Color(.8f, .56f, .34f, .65f)); main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 12;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(R * 2 - .3f, 30, R * 2 - .3f);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(-.02f, .02f); vel.z = new ParticleSystem.MinMaxCurve(-.02f, .02f); vel.y = new ParticleSystem.MinMaxCurve(.05f, .18f);
            var nz = ps.noise; nz.enabled = true; nz.strength = .08f; nz.frequency = .18f; nz.scrollSpeed = .1f;
            var col2 = ps.colorOverLifetime; col2.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(1, .85f), new GradientAlphaKey(0, 1) });
            col2.color = g;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = DLDArt.Mat("K_mote", "DLD/Mote");
        }

        /// <summary>天花亮灯分布图：地面倒影用，mip 当模糊</summary>
        static Texture2D BakeCeilMap(List<(Vector3 p, float yaw, float power)> lamps, float half)
        {
            const int N = 1024; float ppm = N / (half * 2);
            var px = new byte[N * N];
            foreach (var (p, yaw, power) in lamps)
            {
                float hx = yaw % 180 == 0 ? .34f : .64f, hz = yaw % 180 == 0 ? .64f : .34f;
                int x0 = Mathf.FloorToInt((p.x - hx - .5f + half) * ppm), x1 = Mathf.CeilToInt((p.x + hx + .5f + half) * ppm);
                int z0 = Mathf.FloorToInt((p.z - hz - .5f + half) * ppm), z1 = Mathf.CeilToInt((p.z + hz + .5f + half) * ppm);
                for (int z = Mathf.Max(z0, 0); z <= Mathf.Min(z1, N - 1); z++)
                    for (int x = Mathf.Max(x0, 0); x <= Mathf.Min(x1, N - 1); x++)
                    {
                        float wx = (x + .5f) / ppm - half - p.x, wz = (z + .5f) / ppm - half - p.z;
                        float d = Mathf.Max(Mathf.Abs(wx) - hx, Mathf.Abs(wz) - hz);
                        float a = d <= 0 ? 1 : Mathf.Pow(1 - Mathf.Clamp01(d / .5f), 3) * .35f;
                        px[z * N + x] = (byte)Mathf.Max(px[z * N + x], Mathf.Clamp(a * Mathf.Min(power / 4f, 1) * 255, 0, 255));
                    }
            }
            var tex = new Texture2D(N, N, TextureFormat.R8, true, true) { name = "CeilMap", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixelData(px, 0); tex.Apply(true, false);
            var path = KitBuilder.Dir + "CeilMap.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        /// <summary>废弃感：掉下来的天花板、垂着的线、墙上的导向牌、满地的纸</summary>
        static void BuildWear(Transform parent, Dictionary<string, Material> mats)
        {
            var root = new GameObject("Wear").transform; root.SetParent(parent, false);
            float S = KitBuilder.HallHalf, hallH = KitBuilder.HallH, armH = KitBuilder.ArmH;
            var kit = new MeshKit();
            kit.Box("void", new Vector3(-.3f, -.012f, -.3f), new Vector3(.3f, 0, .3f));
            var hole = kit.Save(KitBuilder.Dir + "CeilHole.asset", false);
            var pk = new MeshKit();
            pk.Box("ceiling", new Vector3(-.295f, -.012f, 0), new Vector3(.295f, .012f, .59f), .002f);
            var panel = pk.Save(KitBuilder.Dir + "CeilPanel.asset", false);
            var R = new System.Random(11);
            void Drop(Vector3 p, float yaw, float tilt)
            {
                var y = Quaternion.Euler(0, yaw, 0);
                Put("CeilHole", hole, mats, root, p, y, false);
                var hinge = p + y * new Vector3(0, -.02f, -.3f);
                Put("CeilPanel", panel, mats, root, hinge, y * Quaternion.Euler(tilt, 0, 0), true);
                var ck = new MeshKit(); var c0 = p + y * new Vector3((float)R.NextDouble() * .3f - .15f, 0, .1f);
                ck.Tube("skirt", new[] { c0, c0 + new Vector3(.04f, -.25f, .03f), c0 + new Vector3(-.03f, -.5f, .08f), c0 + new Vector3(.02f, -.62f, .05f) }, .006f, 5);
                Put("Cable", ck.Save(KitBuilder.Dir + $"Cable{root.childCount}.asset", false), mats, root, Vector3.zero, Quaternion.identity, false);
            }
            Drop(new Vector3(-4.875f, hallH - .02f, -4.875f), 20, 38); Drop(new Vector3(4.875f, hallH - .02f, 4.875f), 110, 52); Drop(new Vector3(-1.625f, hallH - .02f, 4.875f), 200, 30); Drop(new Vector3(4.875f, hallH - .02f, -4.875f), 300, 62);
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0, 90 * i, 0);
                Drop(rot * new Vector3(i % 2 == 0 ? .95f : -.95f, armH - .02f, S + (1.5f + i) * KitBuilder.Bay), 90 * i + 30, 44);
                Drop(rot * new Vector3(i % 2 == 0 ? -.95f : .95f, armH - .02f, S + (4.5f - i) * KitBuilder.Bay), 90 * i + 150, 58);
            }

            // 导向牌：只有图形，没有文字，哪国玩家都看得懂
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad); var quad = tmp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(tmp);
            var signMats = new[] { "sign_a", "sign_b" }.Select(n => DLDArt.Mat("K_" + n, "DLD/Lit", m => { m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/_DLD/Art/Decals/{n}.png")); m.SetColor("_BaseColor", new Color(.72f, .74f, .7f)); m.SetFloat("_Gloss", .25f); m.SetFloat("_Rough", .3f); })).ToArray();
            var plate = new MeshKit(); plate.Box("trim", new Vector3(-.47f, -.32f, -.02f), new Vector3(.47f, .32f, 0), .004f);
            var plateMesh = plate.Save(KitBuilder.Dir + "SignPlate.asset", false);
            int sn = 0;
            void Sign(Vector3 p, Quaternion r)
            {
                Put("SignPlate", plateMesh, mats, root, p, r, false);
                var q = new GameObject("Sign"); q.transform.SetParent(root, false); q.transform.SetLocalPositionAndRotation(p + r * new Vector3(0, 0, -.036f), r); q.transform.localScale = new Vector3(.92f, .62f, 1);
                q.AddComponent<MeshFilter>().sharedMesh = quad; var mr = q.AddComponent<MeshRenderer>(); mr.sharedMaterial = signMats[sn++ % 2]; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            for (int i = 0; i < 4; i++)
            {
                var rot = Quaternion.Euler(0, 90 * i, 0);
                Sign(rot * new Vector3(2.45f, 1.72f, S), rot * Quaternion.identity);
                Sign(rot * new Vector3(-KitBuilder.ArmHalf, 1.75f, S + 8.9f), rot * Quaternion.Euler(0, -90, 0));
            }

            // 散落的纸：不进光照贴图，用探针，全部合成一个网格
            var pv = new List<Vector3>(); var pn = new List<Vector3>(); var puv = new List<Vector2>(); var pt = new List<int>();
            var prop = new List<Rect>();
            foreach (var l in parent.GetComponentsInChildren<LODGroup>())
            {
                var rs = l.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var bb = rs[0].bounds; foreach (var q in rs) bb.Encapsulate(q.bounds);
                prop.Add(Rect.MinMaxRect(bb.min.x - .2f, bb.min.z - .2f, bb.max.x + .2f, bb.max.z + .2f));
            }
            int paperN = 0;
            void Paper(Vector3 c)
            {
                foreach (var q in prop) if (q.Contains(new Vector2(c.x, c.z))) return;
                float y0 = .0045f + (paperN++ % 12) * .0012f;     // 一叠纸每张高度不同，重叠的地方才不闪
                float yaw = (float)R.NextDouble() * Mathf.PI * 2, s = Mathf.Sin(yaw), co = Mathf.Cos(yaw), lift = (float)R.NextDouble() * .012f;
                int b = pv.Count; const float a = .105f, h = .15f;
                var corners = new[] { new Vector2(-a, -h), new Vector2(a, -h), new Vector2(a, h), new Vector2(-a, h) };
                for (int k = 0; k < 4; k++) { var q = corners[k]; pv.Add(c + new Vector3(q.x * co - q.y * s, y0 + (k == 2 ? lift * 2 : 0), q.x * s + q.y * co)); pn.Add(Vector3.up); }
                puv.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                pt.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            float Rand(float a, float b) => a + (float)R.NextDouble() * (b - a);
            for (int cl = 0; cl < 14; cl++)
            {
                var c = new Vector3(Rand(-6, 6), 0, Rand(-6, 6));
                if (Mathf.Abs(c.x) < 2.6f && Mathf.Abs(c.z) < 2.6f) continue;   // 玩区里不放：地砖要塌
                for (int k = 0, n = R.Next(2, 6); k < n; k++) Paper(c + new Vector3(Rand(-.5f, .5f), 0, Rand(-.5f, .5f)));
            }
            for (int i = 0; i < 4; i++)
                for (int k = 0; k < 16; k++)
                {
                    var rot = Quaternion.Euler(0, 90 * i, 0);
                    Paper(rot * new Vector3(Rand(-1.3f, 1.3f), 0, S + Rand(.5f, 23)));
                }
            var papers = new Mesh { name = "Papers" }; papers.SetVertices(pv); papers.SetNormals(pn); papers.SetUVs(0, puv); papers.SetTriangles(pt, 0); papers.RecalculateTangents(); papers.RecalculateBounds();
            var path = KitBuilder.Dir + "Papers.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(papers, path);
            var pg = new GameObject("Papers"); pg.transform.SetParent(root, false); pg.AddComponent<MeshFilter>().sharedMesh = papers;
            var pr = pg.AddComponent<MeshRenderer>(); pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.sharedMaterial = DLDArt.Mat("K_paper", "DLD/Lit", m => { m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_DLD/Art/Decals/paper.png")); m.SetColor("_BaseColor", new Color(.26f, .29f, .28f)); m.SetFloat("_Gloss", 0); });
        }

        /// <summary>空气里缓慢漂浮的灰尘</summary>
        static void BuildDust(Transform parent)
        {
            var go = new GameObject("Dust"); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, 2.1f, 0);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.loop = true; main.startLifetime = 16; main.startSpeed = .03f; main.startSize = new ParticleSystem.MinMaxCurve(.012f, .03f); main.maxParticles = 220;
            main.startColor = new Color(.7f, .85f, .8f, .22f); main.simulationSpace = ParticleSystemSimulationSpace.World; main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 14;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(12, 4f, 12);
            var nz = ps.noise; nz.enabled = true; nz.strength = .04f; nz.frequency = .25f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = DLDArt.Mat("K_hallDust", "DLD/Mote_Open");
        }

        /// <summary>巨眼和碎片（发光安瓿瓶）做成预制体，玩法阶段按需生成</summary>
        static void BuildActors()
        {
            const string dir = "Assets/_DLD/Prefabs/";
            System.IO.Directory.CreateDirectory(dir);
            var iris = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_DLD/Art/Textures/iris.png");
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere); Object.DestroyImmediate(eye.GetComponent<Collider>()); eye.name = "Eye";
            eye.GetComponent<Renderer>().sharedMaterial = DLDArt.Mat("K_eye", "DLD/Eye", m => { m.SetTexture("_BaseMap", iris); m.SetColor("_Color", new Color(.42f, .44f, .46f)); m.SetFloat("_IrisR", .5f); m.SetFloat("_Open", 1); });
            eye.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            eye.transform.localScale = Vector3.one * (DLD.Play.EyeView.Radius * 2);
            PrefabUtility.SaveAsPrefabAsset(eye, dir + "Eye.prefab"); Object.DestroyImmediate(eye);

            float h = .17f;
            (float, float)[] P(params float[] a) { var o = new (float, float)[a.Length / 2]; for (int i = 0; i < o.Length; i++) o[i] = (a[i * 2] * h, a[i * 2 + 1] * h); return o; }
            var glass = KitBuilder.Lathe("Ampoule", P(0, 0, .13f, .01f, .22f, .05f, .26f, .12f, .26f, .46f, .23f, .56f, .12f, .64f, .075f, .69f, .075f, .73f, .12f, .78f, .13f, .84f, .09f, .94f, .03f, .995f, 0, 1));
            var liquid = KitBuilder.Lathe("AmpouleLiquid", P(0, .02f, .11f, .03f, .19f, .07f, .225f, .13f, .225f, .4f, .17f, .405f, 0, .41f));
            var shard = new GameObject("Shard");
            void Part(string n, Mesh m, Material mat) { var g = new GameObject(n); g.transform.SetParent(shard.transform, false); g.transform.localPosition = new Vector3(0, -h / 2, 0); g.AddComponent<MeshFilter>().sharedMesh = m; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            Part("Liquid", liquid, DLDArt.Mat("K_shardLiquid", "DLD/Glow", m => { m.SetColor("_Color", new Color(1.05f, .46f, .12f)); m.SetFloat("_Rim", 0); }));
            Part("Glass", glass, DLDArt.Mat("K_shardGlass", "DLD/Glow", m => { m.SetColor("_Color", new Color(.9f, .85f, .7f)); m.SetFloat("_Rim", 1); m.renderQueue = 3001; }));
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad); var quad = tmp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(tmp);
            var halo = new GameObject("Halo"); halo.transform.SetParent(shard.transform, false); halo.AddComponent<MeshFilter>().sharedMesh = quad;
            var hr = halo.AddComponent<MeshRenderer>(); hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            hr.sharedMaterial = DLDArt.Mat("K_shardHalo", "DLD/Halo", m => { m.SetColor("_Color", new Color(.6f, .28f, .08f)); m.SetFloat("_Size", .42f); m.renderQueue = 3002; });
            PrefabUtility.SaveAsPrefabAsset(shard, dir + "Shard.prefab"); Object.DestroyImmediate(shard);

            BuildTentaclePrefab(dir);
        }

        /// <summary>触手预制体：网格运行时生成，这里只配材质</summary>
        static void BuildTentaclePrefab(string dir)
        {
            var tg = new GameObject("Tentacle");
            tg.AddComponent<MeshFilter>(); var mr = tg.AddComponent<MeshRenderer>(); mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.sharedMaterial = DLDArt.Mat("M_tentacle", "DLD/Lit", m =>
            {
                m.SetTexture("_BaseMap", T("tentacle3")); m.SetTexture("_NormalMap", T("tentacle3_n")); m.SetTexture("_MaskMap", T("tentacle3_m"));
                m.SetColor("_BaseColor", new Color(.28f, .26f, .25f)); m.SetFloat("_NormalScale", 1.3f); m.SetFloat("_Cavity", .9f); m.SetFloat("_Gloss", .06f); m.SetFloat("_Rough", .52f); m.SetFloat("_FogK", 0);
                m.SetFloat("_DepthFade", 1); m.SetFloat("_RimK", 1); m.SetFloat("_FogK", .2f); m.renderQueue = 1985;
            });
            tg.AddComponent<Tentacle>();
            PrefabUtility.SaveAsPrefabAsset(tg, dir + "Tentacle.prefab"); Object.DestroyImmediate(tg);
        }

        /// <summary>无脸黑影：带一小片接触阴影的预制体，玩法里由规则层驱动</summary>
        static void BuildFigurePrefab()
        {
            var pf = DLDArt.ImportP2("figure", 2.35f, .08f);
            if (!pf) return;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad); var quad = tmp.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(tmp);
            var cm = DLDArt.Mat("K_contact", "DLD/Contact", m => m.SetFloat("_Strength", .75f));
            DLDArt.Mat("M_figure", "DLD/Lit", m => { m.SetColor("_BaseColor", new Color(.024f, .024f, .026f)); m.SetFloat("_Gloss", 0); m.SetFloat("_Rough", 1); m.SetFloat("_NormalScale", .35f); m.SetFloat("_FogK", 0); });   // 纯哑光：不反光、不进雾
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var c = new GameObject("Contact"); c.transform.SetParent(go.transform, false);
            c.transform.SetLocalPositionAndRotation(new Vector3(0, .004f, 0), Quaternion.Euler(90, 0, 0)); c.transform.localScale = Vector3.one * .9f;
            c.AddComponent<MeshFilter>().sharedMesh = quad; var cr = c.AddComponent<MeshRenderer>(); cr.sharedMaterial = cm; cr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/_DLD/Prefabs/Figure.prefab"); Object.DestroyImmediate(go);
        }

        /// <summary>摆放 P2 道具（还没做好的跳过）。道具投影进光照贴图，自身吃探针光。</summary>
        /// <summary>P2 修复时把输液架细长的立杆吃掉了，下半截用车削补回来：粗管套细管，底部接五爪座</summary>
        static void IvPole(Transform stand)
        {
            // Lathe 每次都会重建同名资产，两个输液架要共用一个，不然前一个的网格就没了
            poleMesh = poleMesh ? poleMesh : KitBuilder.Lathe("IvPole", new[] { (.024f, .1f), (.024f, .17f), (.0165f, .19f), (.0165f, 1.13f), (.0095f, 1.15f), (.0095f, 1.5f), (0f, 1.5f) }, 14);
            var mesh = poleMesh;
            var g = new GameObject("Pole"); g.transform.SetParent(stand, false); g.transform.localPosition = new Vector3(PoleOff.x, 0, PoleOff.y);
            g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = DLDArt.Mat("M_ivPole", "DLD/Lit", m => { m.SetColor("_BaseColor", new Color(.3f, .32f, .32f)); m.SetFloat("_Gloss", .3f); m.SetFloat("_Rough", .3f); });
        }
        static Mesh poleMesh;
        static readonly Vector2 PoleOff = new Vector2(-.017f, .017f);

        /// <summary>护士台：P2 的薄板经修复后只剩碎片，改用程序建模。正面朝大厅中央（+Z）</summary>
        static void BuildDesk(Transform parent, Vector3 pos)
        {
            var k = new MeshKit(); const float w = 1.8f, d = .4f, top = .98f;
            k.Box("skirt", new Vector3(-w + .05f, 0, -d + .05f), new Vector3(w - .05f, .1f, d - .05f));          // 踢脚
            k.Box("paint", new Vector3(-w, .1f, -d), new Vector3(w, top - .05f, d));                                // 柜身
            k.Box("ceiling", new Vector3(-w - .045f, top - .05f, -d - .03f), new Vector3(w + .045f, top, d + .06f));     // 台面
            for (int i = 0; i <= 6; i++) { float x = -w + i * (2 * w / 6); k.Box("trim", new Vector3(x - .012f, .13f, d), new Vector3(x + .012f, top - .08f, d + .012f)); }   // 正面分格压条
            k.Box("trim", new Vector3(-w, .115f, d), new Vector3(w, .135f, d + .012f)); k.Box("trim", new Vector3(-w, top - .085f, d), new Vector3(w, top - .065f, d + .012f));
            k.Box("wood", new Vector3(-w + .35f, top, -.2f), new Vector3(-w + .75f, top + .32f, .12f));         // 病历架
            k.Box("void", new Vector3(.25f, top, -.14f), new Vector3(.55f, top + .07f, .08f));                    // 老电话
            k.Box("ceiling", new Vector3(1f, top, .02f), new Vector3(1.32f, top + .025f, .24f));                 // 一摞纸
            var mats = Mats(); var go = Put("nursedesk", k.Save(KitBuilder.Dir + "NurseDesk.asset", true), mats, parent, pos, Quaternion.identity, true);
            var lod = go.AddComponent<LODGroup>(); lod.SetLODs(new[] { new LOD(.005f, go.GetComponentsInChildren<Renderer>()) });      // 黑影按 LODGroup 收集障碍
        }

        static void BuildProps(Transform parent)
        {
            var root = new GameObject("Props").transform; root.SetParent(parent, false); poleMesh = null;
            void P(string name, float height, float x, float z, float yaw, float gloss = .15f)
            {
                var pf = DLDArt.ImportP2(name, height, gloss);
                if (!pf) { Debug.LogWarning("道具未就绪 " + name); return; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root);
                go.transform.SetLocalPositionAndRotation(new Vector3(x, 0, z), Quaternion.Euler(0, yaw, 0));
                if (name == "ivstand") IvPole(go.transform);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.receiveGI = ReceiveGI.LightProbes;
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ContributeGI);
                }
            }
            // 大厅四角：贴墙摆放，中间留给玩家和黑影
            BuildDesk(root, new Vector3(4.1f, 0, -5.72f));
            P("medcart", 1.0f, -2.35f, -5.75f, 170);
            P("bench", .85f, -3.9f, -5.75f, 0);
            P("bench", .85f, -5.75f, -3.9f, 90);
            P("gurney", .95f, 4.7f, 4.4f, 118);
            P("ivstand", 1.9f, 5.4f, 3.0f, 30);
            P("screen", 1.75f, -4.9f, 4.9f, 45);
            P("cabinet", 1.95f, 5.9f, -3.75f, -90);
            P("cabinet", 1.95f, -5.9f, 3.65f, 90);
            P("wheelchair", 1.0f, -3.3f, 5.4f, 200);
            // 走廊里
            P("wheelchair", 1.0f, .85f, 21.5f, 195);
            P("gurney", .95f, -.98f, -16f, -51);
            P("ivstand", 1.9f, .98f, -13f, 60);
            P("medcart", 1.0f, 18f, .95f, -90);
            P("screen", 1.75f, -14f, -1.2f, 146);
            // 走廊尽头的双开门
            for (int i = 0; i < 4; i++)
            {
                var pf = DLDArt.ImportP2("warddoor", 2.5f, .12f); if (!pf) break;
                var rot = Quaternion.Euler(0, 90 * i, 0);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root);
                go.transform.SetLocalPositionAndRotation(rot * new Vector3(0, 0, KitBuilder.HallHalf + KitBuilder.ArmBays * KitBuilder.Bay - .225f), rot * Quaternion.Euler(0, 180, 0));
            }
        }

        /// <summary>5×5 块地板。参与烘焙（但不静态合批），移动时带着自己的光照贴图走。</summary>
        const int CrackVariants = 6;
        static void BuildFloor(Transform parent, Dictionary<string, Material> mats)
        {
            var floor = new GameObject("Floor").transform; floor.SetParent(parent, false);
            var slabTop = mats["slabTop"]; var slabSide = mats["slabSide"];
            float s = KitBuilder.HW * 2 / 5f;
            var R = new System.Random(7);
            System.IO.Directory.CreateDirectory(KitBuilder.Dir + "Slabs");
            var crackMaps = new Texture2D[CrackVariants];
            for (int v = 0; v < CrackVariants; v++)
            {
                crackMaps[v] = SlabMesh.CrackMap(s, .004f, 7, 100 + v); crackMaps[v].name = "Crack" + v;
                var cp = KitBuilder.Dir + $"Slabs/Crack{v}.asset"; AssetDatabase.DeleteAsset(cp); AssetDatabase.CreateAsset(crackMaps[v], cp);
            }
            for (int j = 0; j < 5; j++)
                for (int i = 0; i < 5; i++)
                {
                    var off = new Vector2((float)R.NextDouble() * 4, (float)R.NextDouble() * 4);
                    var m = SlabMesh.Whole(s, .004f, off);
                    AssetDatabase.DeleteAsset(KitBuilder.Dir + $"Slabs/W{i}{j}.asset");
                    AssetDatabase.CreateAsset(m, KitBuilder.Dir + $"Slabs/W{i}{j}.asset");
                    var go = new GameObject($"Slab{i}{j}");
                    go.transform.SetParent(floor, false);
                    go.transform.localPosition = new Vector3((i - 2) * s, 0, (j - 2) * s);
                    go.AddComponent<MeshFilter>().sharedMesh = m;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterials = new[] { slabTop, slabSide };
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
                    r.scaleInLightmap = 2.5f;
                    r.receiveGI = ReceiveGI.Lightmaps;

                    // 碎块：和这块砖同一套 UV，用同一张光照贴图的同一块区域，碎的瞬间看不出换了网格
                    int variant = (i * 7 + j * 3) % CrackVariants;
                    var view = go.AddComponent<SlabView>();
                    view.whole = r; view.crack = crackMaps[variant];
                    var frag = SlabMesh.Fracture(s, .004f, 7, 100 + variant, off);
                    view.pieces = new MeshRenderer[frag.Length]; view.centers = new Vector2[frag.Length];
                    for (int f = 0; f < frag.Length; f++)
                    {
                        var path = KitBuilder.Dir + $"Slabs/F{i}{j}_{f}.asset";
                        AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(frag[f].mesh, path);
                        var pg = new GameObject("P" + f); pg.transform.SetParent(go.transform, false); pg.SetActive(false);
                        pg.AddComponent<MeshFilter>().sharedMesh = frag[f].mesh;
                        var pr = pg.AddComponent<MeshRenderer>(); pr.sharedMaterials = new[] { slabTop, slabSide }; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        view.pieces[f] = pr; view.centers[f] = frag[f].center;
                    }
                }
            // 3.75 米地图和大厅地面之间的一圈固定地板（大厅地面洞口是 5 米，给最大的地图用）
            var k = new MeshKit();
            float a = KitBuilder.HW, b = KitBuilder.HoleHalf;
            k.Box("terrazzo", new Vector3(-b, -.1f, -b), new Vector3(b, 0, -a));
            k.Box("terrazzo", new Vector3(-b, -.1f, a), new Vector3(b, 0, b));
            k.Box("terrazzo", new Vector3(-b, -.1f, -a), new Vector3(-a, 0, a));
            k.Box("terrazzo", new Vector3(a, -.1f, -a), new Vector3(b, 0, a));
            Put("FloorRim", k.Save(KitBuilder.Dir + "FloorRim.asset", true), mats, floor, Vector3.zero, Quaternion.identity, true);
            // 小地图外圈补上的固定地面，分两圈互不重叠，一起参与烘焙
            Rim("FloorRimSmall", 1.5f, a, mats, floor); Rim("FloorRimTiny", 1.25f, 1.5f, mats, floor);
        }

        static void Rim(string name, float inner, float outer, System.Collections.Generic.Dictionary<string, Material> mats, Transform floor)
        {
            var k = new MeshKit();
            k.Box("terrazzo", new Vector3(-outer, -.1f, -outer), new Vector3(outer, 0, -inner)); k.Box("terrazzo", new Vector3(-outer, -.1f, inner), new Vector3(outer, 0, outer));
            k.Box("terrazzo", new Vector3(-outer, -.1f, -inner), new Vector3(-inner, 0, inner)); k.Box("terrazzo", new Vector3(inner, -.1f, -inner), new Vector3(outer, 0, inner));
            Put(name, k.Save(KitBuilder.Dir + name + ".asset", true), mats, floor, Vector3.zero, Quaternion.identity, true);
        }

        [MenuItem("DLD/2 烘焙光照")]
        public static void Bake()
        {
            var s = new LightingSettings { name = "DLDLighting" };
            s.bakedGI = true; s.realtimeGI = false; s.mixedBakeMode = MixedLightingMode.IndirectOnly;
            s.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            s.lightmapResolution = 14; s.lightmapMaxSize = 1024; s.directionalityMode = LightmapsMode.CombinedDirectional;
            s.directSampleCount = 32; s.indirectSampleCount = 256; s.environmentSampleCount = 64; s.maxBounces = 3; s.filteringMode = LightingSettings.FilterMode.Auto; s.ao = true; s.aoMaxDistance = 1.4f; s.aoExponentDirect = 1.2f; s.aoExponentIndirect = 1.8f;
            s.aoMaxDistance = .6f; s.ao = true; s.aoExponentDirect = 1; s.aoExponentIndirect = 1;
            System.IO.Directory.CreateDirectory("Assets/_DLD/Scenes/Lighting");
            AssetDatabase.CreateAsset(s, "Assets/_DLD/Scenes/Lighting/DLDLighting.lighting");
            Lightmapping.lightingSettings = s;
            Lightmapping.BakeAsync();
        }

        /// <summary>无界面：搭建场景 → 同步烘焙 → 保存。-batchmode -executeMethod DLD.EditorTools.SceneBuilder.BuildAndBakeBatch</summary>
        public static void BuildAndBakeBatch()
        {
            Build();
            Bake();
            Lightmapping.Cancel();
            var s = Lightmapping.lightingSettings; s.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;     // 无界面时没有 GPU 烘焙器
            bool ok = Lightmapping.Bake();
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            System.IO.File.WriteAllText("Logs/scene_build.txt", $"built bake={ok} lightmaps={LightmapSettings.lightmaps.Length}");
        }
    }
}
