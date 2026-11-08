// 井里的巨眼：一颗完整的球，比竖井略小，有球面明暗，只透过洞可见。+Z 是视线方向。
// 每个像素都真的追踪视线：先打到凸出的角膜，折射后落到凹进去的虹膜上，两只眼看到的位置不同，所以有真实的厚度。
// 瞳孔程序绘制，能收缩；开合用干净的黑色裂隙，不做皮肉
Shader "DLD/Eye"
{
    Properties
    {
        _BaseMap ("虹膜", 2D) = "white" {}
        [HDR] _Color ("虹膜颜色", Color) = (0.42,0.44,0.46,1)
        _Open ("睁开", Range(0,1)) = 1
        _Pupil ("瞳孔大小", Range(1,2)) = 1
        _IrisR ("虹膜半径", Range(0.1,0.95)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-15" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Stencil { Ref 1 Comp Equal }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) half4 _Color; half _Open, _Pupil, _IrisR; CBUFFER_END
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 p : TEXCOORD0; float3 cam : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            float Hash3(float3 p) { p = frac(p * .3183 + .1); p *= 17; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
            float Noise3(float3 x)
            {
                float3 i = floor(x), f = frac(x); f = f * f * (3 - 2 * f);
                return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x), lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x), lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + 1), f.x), f.y), f.z);
            }
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.p = v.positionOS.xyz;
                o.cam = TransformWorldToObject(GetCameraPositionWS());
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);                    // 双眼一次渲染时，按当前这只眼取相机位置（否则右眼也用左眼的位置，两眼画面对不上）
                float3 P = normalize(i.p), ro = i.cam * 2;                       // 单位球
                float3 rd = normalize(P - ro);
                if (P.z < 0) return half4(0, 0, 0, 1);
                const float CZ = .52, CR = .58, Limb = .8, IrisZ = .73;          // 角膜是一个凸出来的小球，虹膜是它后面凹进去的一个平面
                float3 C = float3(0, 0, CZ);
                float3 oc = ro - C; float bq = dot(oc, rd), cq = dot(oc, oc) - CR * CR, hq = bq * bq - cq;
                half3 col; float irisMask = 0; float3 n = P; float lidR = length(P.xy);
                float3 L = normalize(ro + float3(0, 0, 1) + float3(.35, .9, 0));  // 光从玩家身后的洞口落下来
                bool cornea = false; float3 H = P;
                if (hq > 0) { float t = -bq - sqrt(hq); H = ro + rd * t; cornea = H.z > Limb; }
                // 眼白：几乎沉在黑暗里，只有迎光的一面有层次
                float ndl = saturate(dot(P, L)), wrap = pow(saturate(ndl * .8 + .2), 2.4);
                col = half3(.03, .028, .026) * wrap;
                col *= lerp(.55, 1.0, Noise3(P * 5.0));
                if (cornea)
                {
                    n = normalize(H - C);
                    float3 r = refract(rd, n, 1.0 / 1.376);
                    float tI = (IrisZ - H.z) / min(r.z, -.05);
                    float3 Q = H + r * tI;
                    // 虹膜随瞳孔伸缩：纹理被径向压缩，不是一张固定的图
                    float2 q = Q.xy / _IrisR; float rq = length(q);
                    float rp = .285 * _Pupil, rT = .285 + .715 * saturate((rq - rp) / (1 - rp));
                    float2 dirq = q / max(rq, 1e-4);
                    float2 uv = dirq * rT * .485 + .5;
                    half lum0 = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb, half3(.3, .59, .11));
                    // 浮雕：纤维有高低，视线斜了会错开
                    uv += r.xy / max(-r.z, .2) * (lum0 - .5) * .035;
                    half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                    half lumR = dot(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + dirq * .012).rgb, half3(.3, .59, .11));
                    half lum = dot(tex, half3(.3, .59, .11));
                    half relief = saturate(.6 + (lum - lumR) * 3.2);
                    // 颜色不用照片的灰：里圈发暖，外圈沉进冷暗里
                    half inner = saturate(1 - (rq - rp) / (1 - rp));
                    half3 hue = lerp(half3(.2, .24, .26), half3(.95, .56, .22), inner * inner);
                    half3 iris = lum * lum * 1.7 * hue * _Color.rgb * 2.4;
                    // 光穿过角膜打在虹膜上：迎光一侧亮，对面暗
                    float lit = saturate(dot(normalize(float3(Q.xy * -1.1, .5)), L) * .75 + .3);
                    iris *= relief * lit;
                    iris *= smoothstep(1.03, .62, rq) * .85 + .15;                 // 角膜缘的暗环：虹膜是沉在里面的
                    // 瞳孔：真的黑洞，边上一圈被遮挡
                    float d = rq / rp;
                    iris *= lerp(.25, 1.0, smoothstep(1.0, 1.55, d));
                    iris *= smoothstep(.97, 1.03, d);
                    float limb = smoothstep(1.06, .96, rq);
                    col = lerp(col * .4, iris, limb);
                    // 角膜反射：头顶破洞的方形亮光，弯在球面上，只有它会随头动
                    float3 R = reflect(rd, n); float2 w = R.xy / max(R.z, .05) - float2(.1, .34);
                    float win = smoothstep(.4, .14, abs(w.x)) * smoothstep(.3, .07, abs(w.y));
                    float fr = .025 + .975 * pow(1 - saturate(dot(n, -rd)), 5);
                    col += half3(.5, .6, .62) * win * (.13 + fr * 1.2) * step(0, R.z);
                    col += half3(.05, .06, .065) * pow(saturate(R.z), 3) * fr * 3;
                }
                else
                {
                    // 边缘沉黑，轮廓才像球
                    col *= saturate(dot(P, normalize(ro)) * 1.2);
                    float3 R = reflect(rd, P); col += half3(.02, .024, .025) * pow(saturate(dot(R, L)), 24);
                }
                // 开合：两片干净的黑，沿球面的弧线
                float2 q0 = P.xy / _IrisR;
                float slit = _Open * 1.9 * (1.0 - pow(saturate(abs(q0.x) / 2.3), 2.0));
                half lid = 1.0 - smoothstep(slit - 0.1, slit + 0.02, abs(q0.y));
                col *= lid;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
