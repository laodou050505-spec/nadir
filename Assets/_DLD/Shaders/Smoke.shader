// 柔软的烟尘：圆形软边，半透明。粒子颜色决定深浅
Shader "DLD/Smoke"
{
    Properties { [MainTexture] _BaseMap ("贴图", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_DLDNoise); SAMPLER(sampler_DLDNoise);
            float _DLDLevel;
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.color = v.color; o.uv = v.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                half n = SAMPLE_TEXTURE2D(_DLDNoise, sampler_DLDNoise, i.uv * 0.35 + i.color.rg * 3.0).r;
                half a = saturate(1.0 - length(i.uv - 0.5) * 2.0); a = a * a * (0.5 + n);
                return half4(i.color.rgb * _DLDLevel, i.color.a * a);
            }
            ENDHLSL
        }
    }
}
