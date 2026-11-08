// 施法的气浪：一圈极淡、断断续续的细波纹，能看见的主要是它掀起的灰尘和灰烬（粒子在 CastFx 里）。纯加色，不抓屏
Shader "DLD/Pulse"
{
    Properties { [HDR] _Color ("颜色", Color) = (.7,.85,1,1) _R ("环半径", Range(0,1)) = .5 _W ("环宽", Range(0,.5)) = .08 _RingK ("环", Float) = 1 _GlowK ("中心", Float) = 0 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color; float _R, _W, _RingK, _GlowK; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 p : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.p = (v.uv - .5) * 2; return o; }
            float Hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            half4 frag(V i) : SV_Target
            {
                float r = length(i.p);
                // 噪声按平面坐标取，不按角度取：按角度取会出放射状的线
                float n = Noise(i.p * 3.1 + _R * 6) * .6 + Noise(i.p * 7.3 - _R * 9) * .4;
                float w = _W * (.55 + .9 * n);
                float x = (r - _R) / max(w, .001);
                float ring = exp(-x * x) * n * n * 1.6 * _RingK;
                float g = saturate(1 - r); g = g * g * g * _GlowK;
                float v = (ring + g) * smoothstep(1, .82, r);
                half3 addRgb = _Color.rgb * v; return half4(addRgb, max(addRgb.r, max(addRgb.g, addRgb.b)));
            }
            ENDHLSL
        }
    }
}
