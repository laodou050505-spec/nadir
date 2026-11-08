using System.Collections.Generic;
using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 界面贴图的原始像素尺寸。界面贴图导入时会被放大到 2 的幂次——尺寸不是 2 的幂次又带 mipmap 的贴图，
    /// Unity 在 Android 上不会压缩，每个像素占 4 字节。文字的实际大小和比例按这里记下的原始尺寸算，所以不会变形。
    /// 这张表由 Editor/UISizes.Bake 在搭建场景和打包前自动生成，放在 Resources 里
    /// </summary>
    public class UITexSizes : ScriptableObject
    {
        public string[] names = new string[0];
        public Vector2Int[] px = new Vector2Int[0];
        static Dictionary<string, Vector2Int> map;

        public static Vector2 Of(Texture t)
        {
            if (map == null)
            {
                map = new Dictionary<string, Vector2Int>();
                var s = Resources.Load<UITexSizes>("UITexSizes");
                if (s) for (int i = 0; i < s.names.Length && i < s.px.Length; i++) map[s.names[i]] = s.px[i];
            }
            return map.TryGetValue(t.name, out var v) ? (Vector2)v : new Vector2(t.width, t.height);
        }

        public static void Reset() => map = null;
    }
}
