using UnityEngine;

namespace DLD.Play
{
    /// <summary>
    /// 触手：沿控制点的样条每帧重建。深色厚皮，肌肉般微微起伏，
    /// 由粗到细收成鞭梢，末端卷曲。波从根部往梢部走；穿洞那一段压住波幅，不碰地砖
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class Tentacle : MonoBehaviour
    {
        public float rootRadius = .3f, midRadius = .19f, tipRadius = .006f, twist = .9f, curl = 2.4f;
        public int rings = 130, sides = 24;
        const float UWraps = 2;
        Mesh mesh; Vector3[] v, n, c, off; Vector4[] tan; Vector2[] uv; float[] len, rad;
        const float WallIn = 2.2f;      // 井壁横梁的内缘（离井中心的距离）

        /// <summary>管子中心线，巡检用</summary>
        public Vector3[] Centers => c;
        public float[] Radii => rad;

        public void Init()
        {
            if (mesh) return;
            int vc = rings * (sides + 1);
            v = new Vector3[vc]; n = new Vector3[vc]; tan = new Vector4[vc]; uv = new Vector2[vc];
            c = new Vector3[rings]; off = new Vector3[rings]; len = new float[rings]; rad = new float[rings];
            var t = new int[(rings - 1) * sides * 6]; int q = 0;
            for (int i = 0; i < rings - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * (sides + 1) + s, b = a + sides + 1;
                    t[q++] = a; t[q++] = a + 1; t[q++] = b; t[q++] = a + 1; t[q++] = b + 1; t[q++] = b;   // 正面朝外
                }
            mesh = new Mesh { name = "Tentacle" }; mesh.MarkDynamic();
            mesh.vertices = v; mesh.uv = uv; mesh.triangles = t;
            mesh.bounds = new Bounds(Vector3.down * 20, Vector3.one * 120);
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        static Vector3 Catmull(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t) =>
            .5f * (2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (3 * b - a - 3 * c + d) * t * t * t);

        /// <summary>ctrl 从根到梢。holeT 是洞口在 0..1 的位置，sway 是波幅（米），curlK 控制末端卷曲</summary>
        public void Shape(Vector3[] ctrl, int count, float time, float sway, float seed, float holeT = -1, float curlK = 1, float taper = 1.7f, Vector3? avoid = null, float maxTop = 99)
        {
            Init();
            float segs = count - 1;
            for (int i = 0; i < rings; i++)
            {
                float u = (float)i / (rings - 1) * segs; int k = Mathf.Min((int)u, count - 2); float f = u - k;
                c[i] = Catmull(ctrl[Mathf.Max(k - 1, 0)], ctrl[k], ctrl[k + 1], ctrl[Mathf.Min(k + 2, count - 1)], f);
            }
            float total = 0; len[0] = 0;
            for (int i = 1; i < rings; i++) { total += (c[i] - c[i - 1]).magnitude; len[i] = total; }

            // 波：从根部往梢部走，越往梢部越灵活；洞口处收住
            for (int i = 1; i < rings - 1; i++)
            {
                float t = (float)i / (rings - 1);
                var d = (c[i + 1] - c[i - 1]).normalized;
                var a = Vector3.Cross(d, Vector3.right); if (a.sqrMagnitude < .05f) a = Vector3.Cross(d, Vector3.forward); a.Normalize();
                var b = Vector3.Cross(d, a);
                float env = Mathf.Sin(t * Mathf.PI); env = env * Mathf.Sqrt(env);
                if (holeT >= 0) env *= Mathf.Clamp01(Mathf.Abs(t - holeT) / .1f);
                float ph = len[i] * 2.6f - time * 1.7f + seed;
                float amp = sway * env * Mathf.Lerp(1, .55f, t);
                off[i] = (a * Mathf.Sin(ph) + b * Mathf.Sin(ph * .63f + 1.7f + seed)) * amp;
            }
            off[0] = off[rings - 1] = Vector3.zero;
            for (int i = 0; i < rings; i++) c[i] += off[i];

            // 梢部卷曲：最后一段沿圆弧弯回来（缠腿时梢部已经绕着腿，不再卷）
            int cs = curlK > .01f ? Mathf.RoundToInt(rings * .8f) : rings;
            var e = cs < rings ? (c[cs] - c[cs - 2]).normalized : Vector3.up;
            var axis = Vector3.Cross(e, Vector3.up); if (axis.sqrMagnitude < .05f) axis = Vector3.right; axis.Normalize();
            float ang = 0; float step = curl * curlK * (1 + Mathf.Sin(time * .9f + seed) * .25f) / Mathf.Max(rings - cs, 1);
            var p = c[Mathf.Min(cs, rings - 1)]; var dirNow = e;
            for (int i = cs + 1; i < rings; i++)
            {
                float seg = len[i] - len[i - 1];
                ang += step * (1 + (float)(i - cs) / (rings - cs));
                dirNow = Quaternion.AngleAxis(ang * Mathf.Rad2Deg, axis) * e;
                p += dirNow * seg; c[i] = p;
            }

            // 粗细按离梢部的距离算：不管伸出来多少，先露出来的永远是细鞭梢
            for (int i = 0; i < rings; i++)
            {
                float fromTip = len[rings - 1] - len[i];
                float r0 = fromTip < taper ? Mathf.Lerp(tipRadius, midRadius, Mathf.Pow(fromTip / taper, .85f)) : Mathf.Lerp(midRadius, rootRadius, Mathf.Clamp01((fromTip - taper) / 9f));
                rad[i] = r0 * (1 + .09f * Mathf.Sin(len[i] * 2.3f + seed));                       // 肌肉般微微起伏
                // 井下的部分不碰四壁：洞口附近放开，往下逐渐收紧，不会折一下
                float below = Mathf.Clamp01((-c[i].y - .1f) / .9f);
                if (below > 0)
                {
                    float lim = WallIn - rad[i] * 1.2f - .05f;
                    c[i].x = Mathf.Lerp(c[i].x, Mathf.Clamp(c[i].x, -lim, lim), below); c[i].z = Mathf.Lerp(c[i].z, Mathf.Clamp(c[i].z, -lim, lim), below);
                }
                if (c[i].y + rad[i] * 1.16f > maxTop) c[i].y = maxTop - rad[i] * 1.16f;      // 表面不超过这个高度
                if (avoid.HasValue)
                {
                    var o2 = new Vector2(c[i].x - avoid.Value.x, c[i].z - avoid.Value.z); float keep = .6f + rad[i];
                    if (o2.sqrMagnitude < keep * keep) { o2 = (o2.sqrMagnitude > 1e-4f ? o2.normalized : Vector2.right) * keep; c[i].x = avoid.Value.x + o2.x; c[i].z = avoid.Value.z + o2.y; }
                }
            }

            var dir = (c[1] - c[0]).normalized;
            var nor = Vector3.Cross(dir, Vector3.right); if (nor.sqrMagnitude < .05f) nor = Vector3.Cross(dir, Vector3.forward); nor.Normalize();
            var w = transform.worldToLocalMatrix; float vAcc = seed * .37f;
            for (int i = 0; i < rings; i++)
            {
                float t = (float)i / (rings - 1);
                var d = (c[Mathf.Min(i + 1, rings - 1)] - c[Mathf.Max(i - 1, 0)]).normalized;
                nor = Vector3.ProjectOnPlane(nor, d).normalized;        // 平行移动，管子不乱扭
                var bin = Vector3.Cross(d, nor);
                float r = rad[i];
                float tw = len[i] * twist + seed;
                // 纹理按周长铺：粗细变化时皮肤的纹路不被拉长。圈向必须是整数次，才没有接缝
                if (i > 0) vAcc += (len[i] - len[i - 1]) * UWraps / (2 * Mathf.PI * Mathf.Max(r, .05f));
                for (int s = 0; s <= sides; s++)
                {
                    float a = s * Mathf.PI * 2 / sides;
                    float ph3 = 3 * a + tw;
                    float lobe = 1 + .13f * Mathf.Sin(ph3) + .03f * Mathf.Sin(2 * ph3 + 1);          // 三股筋扭在一起：俯视时靠它读出体积
                    float dl = (.39f * Mathf.Cos(ph3) + .18f * Mathf.Cos(2 * ph3 + 1)) / lobe;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    var o = nor * ca + bin * sa; var around = -nor * sa + bin * ca;
                    int id = i * (sides + 1) + s;
                    v[id] = w.MultiplyPoint3x4(c[i] + o * (r * lobe));
                    n[id] = w.MultiplyVector((o - around * dl).normalized);
                    var tg = w.MultiplyVector(d); tan[id] = new Vector4(tg.x, tg.y, tg.z, 1);
                    uv[id] = new Vector2((float)s / sides * UWraps + tw / (Mathf.PI * 2), vAcc);
                }
            }
            mesh.vertices = v; mesh.normals = n; mesh.tangents = tan; mesh.uv = uv;
        }
    }
}
