// 深渊里的雾层：一叠水平薄片，只透过洞可见。每层噪声方向不同，缓慢流动，立体视差是真的。
// 顶点色 rgb 是这层的颜色，a 是浓度；顶点上 uv.x 存流速系数
Shader "DLD/Mist"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-5" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha ZWrite Off Cull Off
            Stencil { Ref 1 Comp Equal }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_DLDNoise); SAMPLER(sampler_DLDNoise);
            float4 _DLDShaft; float _DLDEyeY; float _DLDLevel;
            struct A { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half4 color : COLOR; float2 uv : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.color = v.color; o.uv = v.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float t = _Time.y * i.uv.x; float2 p = i.positionWS.xz + i.positionWS.y * 0.37;
                half a1 = SAMPLE_TEXTURE2D(_DLDNoise, sampler_DLDNoise, p * 0.11 + float2(t * 0.013, t * 0.008)).r;
                half a2 = SAMPLE_TEXTURE2D(_DLDNoise, sampler_DLDNoise, p * 0.27 - float2(t * 0.009, -t * 0.017)).g;
                half n = a1 * 0.65 + a2 * 0.5;
                half a = smoothstep(0.32, 0.78, n) * i.color.a;
                half3 col = i.color.rgb * lerp(0.4, 1.0, _DLDLevel);
                // 眼睛升上来时，雾从下面被映红
                col += half3(0.2, 0.012, 0.005) * _DLDShaft.w * exp(-abs(i.positionWS.y - _DLDEyeY) * 0.2);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
