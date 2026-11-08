// 美术资源接入：FBX / 贴图导入设置、P2 模型 → 带 LOD 的预制体（改自 BANG 的 BangArt）。
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DLD.EditorTools
{
    public class DLDModelPostprocessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_DLD/Art/Models")) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;   // 材质统一由 DLDArt 分配
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;
            mi.globalScale = 1f;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_DLD/Art")) return;
            var ti = (TextureImporter)assetImporter;
            if (assetPath.EndsWith("_n.png")) ti.textureType = TextureImporterType.NormalMap;
            if (assetPath.EndsWith("_m.png")) ti.sRGBTexture = false;
            if (ti.maxTextureSize > 2048) ti.maxTextureSize = 2048;   // 用户要求：贴图不超过 2K
            ti.mipmapEnabled = true;
            ti.anisoLevel = 6;         // 地面和墙斜着看时不闪
            if (assetPath.StartsWith("Assets/_DLD/Art/UI")) { ti.wrapMode = TextureWrapMode.Clamp; ti.alphaIsTransparency = true; ti.npotScale = TextureImporterNPOTScale.ToLarger;      /* 放大到 2 的幂次：不是 2 的幂次又带 mipmap 的贴图在 Android 上不会被压缩，原始尺寸记在 UITexSizes */ ti.filterMode = FilterMode.Trilinear; ti.mipmapFilter = TextureImporterMipFilter.KaiserFilter; }
            var a = ti.GetPlatformTextureSettings("Android");
            bool ui = assetPath.StartsWith("Assets/_DLD/Art/UI");
            a.overridden = true; a.maxTextureSize = Mathf.Min(2048, ti.maxTextureSize); a.format = ui ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(a);
        }
    }

    public class DLDAudioPostprocessor : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/_DLD/Audio")) return;
            var ai = (AudioImporter)assetImporter;
            bool music = assetPath.Contains("/Music/");
            ai.forceToMono = !music;
            var st = ai.defaultSampleSettings;
            st.loadType = music || assetPath.Contains("/Amb/") || assetPath.EndsWith("_loop.wav") ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            st.compressionFormat = AudioCompressionFormat.Vorbis; st.quality = music ? .42f : .6f;
            ai.defaultSampleSettings = st;
        }
    }

    public static class DLDArt
    {
        public const string P2Src = "ArtSource/ModelsP2";
        public const string P2Dst = "Assets/_DLD/Art/Models/P2";
        public const string PrefabDir = "Assets/_DLD/Prefabs/P2";

        /// <summary>LOD 切换屏高。</summary>
        public static readonly float[] LodHeights = { 0.25f, 0.08f, 0.01f };

        public static Material Mat(string name, string shader, System.Action<Material> init = null)
        {
            Directory.CreateDirectory("Assets/_DLD/Art/Materials");
            var p = $"Assets/_DLD/Art/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (!m) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, p); }
            else if (m.shader.name != shader) m.shader = Shader.Find(shader);
            init?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static GameObject Model(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        /// <summary>
        /// 把 P2 管线产出（final/lod0..2.fbx + albedo.png，且 report 全部合格）做成预制体：底部落在 y=0、水平居中、缩放到目标高度（米）。
        /// 没有合格产出时返回 null。
        /// </summary>
        public static GameObject ImportP2(string name, float targetHeight, float gloss = 0.12f)
        {
            var src = Path.Combine(P2Src, name, "final");
            var rep = Path.Combine(src, "report.json");
            if (!File.Exists(Path.Combine(src, "lod0.fbx")) || !File.Exists(rep) || !File.ReadAllText(rep).Contains("\"pass\": true")) return null;
            var dst = $"{P2Dst}/{name}";
            Directory.CreateDirectory(dst);
            bool changed = false;
            foreach (var f in new[] { "lod0.fbx", "lod1.fbx", "lod2.fbx", "albedo.png", "albedo_n.png", "albedo_m.png" })
            {
                var s = Path.Combine(src, f); var d = Path.Combine(dst, f);
                if (File.Exists(s) && (!File.Exists(d) || File.GetLastWriteTimeUtc(s) > File.GetLastWriteTimeUtc(d))) { File.Copy(s, d, true); changed = true; }
            }
            if (changed) AssetDatabase.Refresh();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dst}/albedo.png");
            var ntex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dst}/albedo_n.png"); var mtex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dst}/albedo_m.png");
            var mat = Mat("M_" + name, "DLD/Lit", m => { m.SetTexture("_BaseMap", tex); if (ntex) { m.SetTexture("_NormalMap", ntex); m.SetFloat("_NormalScale", 1); } if (mtex) { m.SetTexture("_MaskMap", mtex); m.SetFloat("_Cavity", .5f); m.SetFloat("_Grime", .8f); m.SetFloat("_WearK", name == "figure" || name == "hand" || name == "climber" ? 0 : .75f); } m.SetFloat("_Gloss", gloss); m.SetFloat("_Rough", .35f); m.SetColor("_BaseColor", new Color(.62f, .66f, .64f)); });

            var root = new GameObject(name);
            var lods = new System.Collections.Generic.List<LOD>();
            Bounds b = default; bool hasB = false;
            for (int i = 0; i < LodHeights.Length; i++)
            {
                var m = Model($"{dst}/lod{i}.fbx");
                if (!m) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(m);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                inst.name = "LOD" + i;
                inst.transform.SetParent(root.transform, false);
                // P2 模型正面朝 −X：转 90° 让预制体正面朝 +Z（Unity 惯例）
                inst.transform.localRotation = Quaternion.Euler(0, 90, 0) * inst.transform.localRotation;
                inst.transform.localPosition = Quaternion.Euler(0, 90, 0) * inst.transform.localPosition;
                var rs = inst.GetComponentsInChildren<Renderer>();
                foreach (var r in rs) { r.sharedMaterial = mat; if (!hasB) { b = r.bounds; hasB = true; } else if (i == 0) b.Encapsulate(r.bounds); }
                lods.Add(new LOD(LodHeights[i], rs));
            }
            if (!hasB) { Object.DestroyImmediate(root); return null; }
            float s0 = targetHeight / Mathf.Max(b.size.y, 1e-4f);
            foreach (Transform c in root.transform) { c.localScale *= s0; c.localPosition = (c.localPosition - new Vector3(b.center.x, b.min.y, b.center.z)) * s0; }
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(lods.ToArray());
            lg.RecalculateBounds();
            Directory.CreateDirectory(PrefabDir);
            var pf = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return pf;
        }
    }
}
