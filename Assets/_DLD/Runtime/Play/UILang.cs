using UnityEngine;

namespace DLD.Play
{
    /// <summary>一张中文贴图 + 一张英文贴图。文字尺寸永远按贴图原始比例算，两种语言宽度不同也不会变形（size 只给线条、暗幕这类纯图形用）</summary>
    public class UILang : MonoBehaviour
    {
        public const float MetersPerPixel = 1 / 2000f;
        public Texture2D zh, en; public Vector3 anchor; public int align; public float scale = 1; public Vector2 size, fit;
        public static bool Chinese = true;
        static readonly int Map = Shader.PropertyToID("_BaseMap");

        public void Apply()
        {
            var tex = Chinese || !en ? zh : en;
            var r = GetComponent<MeshRenderer>(); var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb); mpb.SetTexture(Map, tex); r.SetPropertyBlock(mpb);
            float w = size.x > 0 ? size.x : tex.width * MetersPerPixel * scale, h = size.y > 0 ? size.y : tex.height * MetersPerPixel * scale;
            if (fit.x > 0) { float k = Mathf.Min(1, fit.x / w, fit.y / h); w *= k; h *= k; }      // 字放不下就整体缩小，比例不变，四周留白不变
            transform.localScale = new Vector3(w, h, 1);
            var item = GetComponent<UIItem>();
            var p = anchor + Vector3.right * (align * w / 2);
            if (item) item.home = p; else transform.localPosition = p;
        }
    }
}
