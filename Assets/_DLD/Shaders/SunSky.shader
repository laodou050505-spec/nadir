// 结局的太阳和日晕：画在相机外面一个大球上，但颜色只按视线方向算（和天空一样在无穷远）。
// 所以它像真的太阳：转头时在视野里移动，走动、探头没有视差，不是空中的一块平面；手举起来能挡住它。
// 由内到外：太阳圆盘（软边，中心暖白、边缘偏橙略暗）+ 长短不一的光束 + 一道横向长光条 → 热气一样轻轻流动的日冕光
//           → 两圈很淡的小光环（华）→ 主晕环（白色为主，只有内缘淡红琥珀，上面有冰晶细碎的闪光慢慢明灭）
// 光束会慢慢转、长短亮度各自涨落，细芒轻轻闪烁；薄云缓慢飘过，经过晕环时那一段变亮或几乎消失，经过太阳时太阳稍暗→ 晕环顶上一小段上切弧 → 左右两个幻日
//           → 外晕（只留上半段）→ 最外一层淡淡暖光。
// 晕环各处的明暗像有薄云飘过一样缓慢起伏。全部很慢，不闪；太阳最亮也不到纯白，不刺眼。
// 叠加混合，alpha 跟着亮度走（透视模式下才看得见）
Shader "DLD/SunSky"
{
    Properties
    {
        _Fade ("显现", Range(0,1)) = 0
        _Glare ("正对着看时的晃眼", Range(0,1)) = 0
        _SunDir ("太阳方向", Vector) = (0,.28,1,0)
        _Ring ("主晕环半径(弧度)", Float) = .1745
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off ZTest LEqual Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_DLDNoise); SAMPLER(sampler_DLDNoise);
            CBUFFER_START(UnityPerMaterial) half _Fade, _Glare; float4 _SunDir; float _Ring; CBUFFER_END
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); return o; }

            half N(float2 uv) { return SAMPLE_TEXTURE2D_LOD(_DLDNoise, sampler_DLDNoise, uv, 0).r; }

            // 晕环的颜色：x 是到环中心的距离（以环宽为单位）。真实的日晕主体是白的，
            // 只有内缘带一点淡红琥珀色，外侧稍微偏冷，不是一整圈彩虹
            half3 RingCol(float x, half pastel)
            {
                half3 c = lerp(half3(1, .58, .34), half3(1, .95, .87), smoothstep(-1.3, -.15, x));
                c = lerp(c, half3(.88, .93, 1), smoothstep(.4, 1.8, x) * .55);
                return lerp(c, half3(1, .96, .9), pastel);
            }

            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);                    // 双眼一次渲染时，按当前这只眼取相机位置（否则右眼也用左眼的位置，两眼画面对不上）
                float3 d = normalize(i.positionWS - GetCameraPositionWS());
                float3 s = normalize(_SunDir.xyz);
                float cosT = dot(d, s);
                if (cosT < .25) return 0;                                         // 太阳背后什么都没有
                float r = acos(clamp(cosT, -1, 1)) / _Ring;                       // 到太阳的角距离，以主晕环半径为 1
                float3 rt = normalize(cross(float3(0, 1, 0), s)), up = cross(s, rt);
                float2 p = float2(dot(d, rt), dot(d, up)) / cosT / tan(_Ring);    // 以太阳为中心、上方向是天的平面坐标
                float ang = atan2(p.y, p.x);
                float t = _Time.y;

                // ── 薄云：两层拉长的卷云纹理，往一个方向慢慢飘过（约 1 分钟飘过一个晕环）──
                //   它从太阳前面经过时太阳和光束会稍微暗一点；它经过晕环时，那一段晕环变亮或几乎消失，
                //   所以晕环每一段的明暗都在慢慢变，不是一张贴纸
                float2 wind = float2(t * .016, t * .004);
                half cir = N(float2(p.x * .2, p.y * .55) + wind);
                half cir2 = N(float2(p.x * .55 + p.y * .2, p.y * 1.4) * 1.3 + wind * 1.7 + .37);
                half cloud = saturate(cir * .65 + cir2 * .45 - .15);
                half live = lerp(.18, 1.25, smoothstep(.2, .85, cloud));
                half veil = lerp(1, .82, smoothstep(.45, .8, N(float2(.13, .29) + wind)));   // 太阳前面的云

                // ── 太阳 ──
                float sr = r / .085;
                half disc = smoothstep(1.1, .7, sr);                               // 边缘是软的，不是一个硬边正圆
                half limb = sqrt(saturate(1 - sr * sr));
                half3 sun = lerp(half3(1, .74, .46), half3(1, .94, .82), limb) * (.4 + .2 * limb) * disc * lerp(1, veil, .5);      // 临边昏暗：中心暖白，边缘偏橙略暗；和外面几层叠起来最亮约 .85，不到纯白
                half heat = .82 + .18 * N(float2(ang * .9 + t * .05, r * 2 - t * .07));                           // 日冕光像热气一样轻轻流动
                half bloom = (exp(-sr * sr * .3) * .12 + exp(-r * 6) * .07) * heat * (1 + .15 * _Glare);
                // 光束：像真的阳光穿过睫毛那样，是活的——
                //   六条主光束（极慢地转，每条的长短、亮度各自慢慢涨落）+ 六条错开的短光束
                //   + 一圈很细很密的细芒（沿着角度取噪声，噪声慢慢流动，所以细芒在轻轻闪烁、变换）
                float rot = ang + t * .012;
                float i1 = floor((rot + 3.1416) / 1.0472), i2 = floor((rot + 3.1416 + .5236) / 1.0472);
                half len1 = .45 + 1.0 * N(float2(i1 * .37, .21 + t * .035));
                half len2 = .4 + .8 * N(float2(i2 * .41, .63 + t * .045));
                half amp1 = .6 + .55 * N(float2(i1 * .53 + .5, t * .09));
                half amp2 = .5 + .6 * N(float2(i2 * .29 + .2, t * .11 + .4));
                half beams = pow(abs(cos(rot * 3 + .35)), 260) * exp(-r * 1.9 / len1) * .15 * amp1
                           + pow(abs(cos(rot * 3 + .35 + 1.5708)), 320) * exp(-r * 3.4 / len2) * .09 * amp2;
                float2 cs = float2(cos(ang), sin(ang));
                half fine = pow(N(cs * 2.6 + float2(t * .021, -t * .017)), 5) * pow(N(cs * 1.1 - float2(t * .013, t * .009) + .5), 2);
                beams += fine * exp(-r * 3.8) * .5;
                beams *= smoothstep(.06, .16, r) * (1 + .3 * _Glare) * veil;
                // 一道横向的长光条（照片里太阳左边那一道），左边长、右边短
                half streak = exp(-p.y * p.y / .0007) * exp(-abs(p.x) * (p.x < 0 ? .9 : 2.2)) * .075 * smoothstep(.05, .16, abs(p.x));

                // ── 冰晶的细碎闪光：沿着晕环一粒一粒慢慢明灭 ──
                half glint = smoothstep(.68, .95, N(float2(ang * 3.3, t * .06 + r * .4)));

                // 华：贴着太阳两圈很淡的彩色小环
                float c1 = (r - .2) / .028, c2 = (r - .33) / .035;
                half3 aureole = (RingCol(c1, .2) * exp(-c1 * c1) * .06 + RingCol(c2, .35) * exp(-c2 * c2) * .04) * live;
                // 主晕环 + 外侧拖出的一圈淡光
                // 真实的晕环内缘清楚、外侧慢慢散开
                float x1 = (r - 1) / .05;
                half prof = x1 < 0 ? exp(-x1 * x1 * 1.4) : exp(-x1 * .75) * .85 + exp(-x1 * x1) * .15;
                half3 halo1 = RingCol(x1, 0) * prof * .2 * live * (.85 + .4 * glint);
                half skirt = smoothstep(.98, 1.05, r) * exp(-max(r - 1.05, 0) * 6) * .016 * live;
                // 上切弧：晕环顶上一小段向上翘的亮弧
                float yt = (p.y - 1.02 - .3 * p.x * p.x) / .035;
                half3 tangent = RingCol(yt, .2) * exp(-yt * yt) * exp(-p.x * p.x / .12) * .07 * live;
                // 幻日：晕环左右和太阳同高的小亮斑，往外拖一点尾巴
                float sx = abs(p.x) - 1.04;
                half dog = exp(-(sx * sx) / .0035 - p.y * p.y / .0016) + exp(-max(sx, 0) * 9 - p.y * p.y / .0009) * .25 * step(0, sx);
                half3 dogs = RingCol(sx / .05, .1) * dog * .26 * (.8 + .35 * glint) * lerp(.45, 1.15, cloud);
                // 外晕：更大更淡，只留上半段
                float x2 = (r - 2.05) / .11;
                half arc = smoothstep(-.85, .15, p.y / max(r, 1e-3));
                half3 halo2 = RingCol(x2, .5) * exp(-x2 * x2) * .04 * arc * live;
                // 最外一层淡淡暖光；正对着看时多一点、散得更开（像被晃到），转开就回落
                // 真实的日晕：环里面的天空比环外面暗一点（冰晶不往环内折光），所以环内的暖光压低
                half inner = lerp(.45, 1, smoothstep(.55, 1.0, r)) ;
                half glow = (exp(-r * 2.7) * (.06 + .04 * _Glare) * veil + exp(-r * 1.3) * .02 * _Glare) * inner;
                // 被太阳照亮的薄云本身：离太阳越近越亮，慢慢飘过，给晕环一个“在天上”的纵深
                half wisps = smoothstep(.35, .9, cloud) * exp(-r * 1.1) * .035 * inner;

                half3 e = sun + half3(1, .86, .64) * (bloom + glow + beams + streak * veil + skirt + wisps) + aureole + halo1 + tangent + dogs + halo2;
                e *= _Fade * smoothstep(2.9, 2.4, r);
                half a = saturate(max(e.r, max(e.g, e.b)));
                return half4(e, a);
            }
            ENDHLSL
        }
    }
}
