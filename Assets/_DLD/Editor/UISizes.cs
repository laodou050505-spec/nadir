using System.Linq;
using DLD.Play;
using UnityEditor;
using UnityEngine;

namespace DLD.EditorTools
{
    /// <summary>记下每张界面贴图的原始像素尺寸（导入后它们被放大到 2 的幂次），写到 Resources/UITexSizes.asset</summary>
    public static class UISizes
    {
        static string Base => DLDArt.P2Dst.Substring(0, DLDArt.P2Dst.IndexOf("/Art/"));

        public static void Bake()
        {
            string b = Base, dir = b + "/Art/UI", res = b + "/Resources", path = res + "/UITexSizes.asset";
            if (!AssetDatabase.IsValidFolder(res)) AssetDatabase.CreateFolder(b, "Resources");
            var so = AssetDatabase.LoadAssetAtPath<UITexSizes>(path);
            if (!so) { so = ScriptableObject.CreateInstance<UITexSizes>(); AssetDatabase.CreateAsset(so, path); }
            var list = AssetDatabase.FindAssets("t:Texture2D", new[] { dir }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
            so.names = new string[list.Count]; so.px = new Vector2Int[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(list[i]);
                ti.GetSourceTextureWidthAndHeight(out int w, out int h);
                so.names[i] = System.IO.Path.GetFileNameWithoutExtension(list[i]); so.px[i] = new Vector2Int(w, h);
            }
            EditorUtility.SetDirty(so); AssetDatabase.SaveAssets(); UITexSizes.Reset();
        }
    }
}
