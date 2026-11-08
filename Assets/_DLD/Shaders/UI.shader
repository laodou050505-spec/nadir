// 世界空间界面：贴图只提供形状（白色 + 透明），颜色、显影、光晕都在这里算。
// _Reveal 从 0 到 1：字从模糊里聚焦出来，同时沿 _Sweep 方向显影；退场倒放
Shader "DLD/UI"
{
    Properties
    {
        _BaseMap ("形状", 2D) = "white" {}
        _Tint ("颜色", Color) = (1,1,1,1)
        [HDR] _Glow ("光晕", Color) = (0,0,0,0)
        _Reveal ("显影", Range(0,1)) = 1
        _Sweep ("显影方向 0横向 1中心 2绕圈 3向下", Float) = 0
        _Shadow ("字后暗影", Range(0,1)) = 0
        _UseRgb ("用贴图本身的颜色", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZT ("深度比较", Float) = 4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest [_ZT] Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) half4 _Tint, _Glow; float _Reveal, _Sweep, _Shadow, _UseRgb; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float aspect : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv;
                o.aspect = length(UNITY_MATRIX_M[0].xyz) / max(length(UNITY_MATRIX_M[1].xyz), 1e-4);
                return o;
            }
            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            half4 frag(V i) : SV_Target
            {
                float m;
                if (_Sweep < .5)
                {
                    float2 q = i.uv * float2(i.aspect, 1) * 7;
                    float s = i.uv.x * .68 + (Noise(q) * .65 + Noise(q * 2.3) * .35) * .32;
                    m = smoothstep(0, .35, _Reveal * 1.35 - s);
                }
                else if (_Sweep < 1.5) m = smoothstep(0, .06, _Reveal * 1.06 - abs(i.uv.x - .5) * 2);
                else if (_Sweep < 2.5) m = smoothstep(0, .012, _Reveal * 1.012 - frac(atan2(i.uv.x - .5, i.uv.y - .5) / 6.28318 + 1));
                else m = smoothstep(0, .06, _Reveal * 1.06 - (1 - i.uv.y));
                half4 tex = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, i.uv, (1 - m) * 4.5);
                float a = tex.a * m;
                float halo = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, i.uv, 3.6).a * m;
                float shade = saturate(halo * 2.4) * _Shadow;
                half3 c = _Tint.rgb * a + _Glow.rgb * halo;
                if (_UseRgb > .5)
                {
                    // 做旧的金属字：一道很慢的冷光扫过，只在字面的亮部起作用
                    float s = i.uv.x - i.uv.y * .28 - frac(_Time.y * .055) * 2.2 + .6;
                    half lum = dot(tex.rgb, half3(.3, .59, .11));
                    c = (tex.rgb * _Tint.rgb + exp(-s * s * 90) * lum * lum * half3(.62, .78, .84)) * a + _Glow.rgb * halo;
                }
                float o = a + (1 - a) * shade;
                return half4(c * _Tint.a, o * _Tint.a);
            }
            ENDHLSL
        }
    }
}
