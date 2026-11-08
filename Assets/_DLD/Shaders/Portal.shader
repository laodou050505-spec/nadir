// 传送门：空间被撕开的一道竖直裂隙，是一个有内壁、向里收的洞。走穿了才是另一个世界：
// 按真实视线方向采样全景图，走动时景物跟着变，两只眼看到的也不同。
// 裂口是一截一截崩缺的折线，没有内壁（内壁让人眩晕）；边缘只有零星几段在发红。稳稳地定在空中，不摇不晃
Shader "DLD/Portal"
{
    Properties
    {
        _Open ("张开", Range(0,1)) = 1
        _Pano ("另一边的全景", 2D) = "black" {}
        _Yaw ("全景朝向", Range(0,1)) = 0
        [HDR] _Rim ("边缘", Color) = (1.0,0.72,0.42,1)
        [HDR] _Bleed ("裂口溢光", Color) = (1.0,0.6,0.26,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_DLDNoise); SAMPLER(sampler_DLDNoise);
            TEXTURE2D(_Pano); SAMPLER(sampler_Pano);
            CBUFFER_START(UnityPerMaterial) half _Open; half _Yaw; half4 _Rim, _Bleed; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.uv = v.uv;
                return o;
            }
            half N(float2 p) { return SAMPLE_TEXTURE2D_LOD(_DLDNoise, sampler_DLDNoise, p, 0).r; }
            // 裂口外形：<0 在里面。一条锯齿状的折线，像玻璃被撕开：中线是折的，两边各自一段一段地崩缺，不是光滑的叶子。
            // k 让它越往里越窄，是一个真的向内收的洞
            float Dist(float2 q, float k)
            {
                float y = q.y / 0.98, yy = saturate(1.0 - y * y);
                float seg = q.y * 5.2, fi = floor(seg), ff = frac(seg);
                // 中线：每段一个折点，连成折线
                float c0 = (N(float2(fi * .137, .21)) - .5) * .34, c1 = (N(float2((fi + 1) * .137, .21)) - .5) * .34;
                float bend = lerp(c0, c1, ff) * yy;
                float width = _Open * .42 * pow(yy, .55) * k;
                // 两边的崩口：方波一样一截一截缺进去，再叠细碎的毛刺
                float l = (N(float2(fi * .173, .52)) - .5) * .55 + (N(float2(q.y * 9.0, .35)) - .5) * .1;
                float r = (N(float2(fi * .191 + .3, .74)) - .5) * .55 + (N(float2(q.y * 9.3 + .5, .65)) - .5) * .1;
                return max(bend - width * (1.0 + l) - q.x, q.x - bend - width * (1.0 + r));
            }
            half4 frag(V i) : SV_Target
            {
                float2 q = (i.uv - 0.5) * 2.0;
                float dist = Dist(q, 1);
                half inside = 1.0 - smoothstep(-0.008, 0.004, dist);
                half core = exp(-abs(dist) * 90.0);
                half rim = exp(-abs(dist) * 34.0);
                
                // 裂口里只有近乎全黑的虚空
                half3 col = half3(0.015, 0.008, 0.005) * inside;
                // 边缘：像纸烧过的边，只有零星几段在发红
                half ember = smoothstep(.42, .72, N(float2(q.y * 2.1 + q.x * .6, _Time.y * .012))) * .9 + .1;
                col += _Rim.rgb * core * ember * .45 * saturate(_Open * 6.0 + 0.15);
                // 裂口外面完全透明：不能有任何 alpha 泄漏，否则 quad 的矩形就暴露了
                half alpha = saturate(inside + core * .35 * ember);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}
