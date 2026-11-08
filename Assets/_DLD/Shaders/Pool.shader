// 地面上的一片柔光（叠加），贴在地面上
Shader "DLD/Pool"
{
    Properties { [HDR] _Color ("颜色", Color) = (0.5,0.28,0.1,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+45" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }
            half4 frag(V i) : SV_Target { half a = saturate(1.0 - length(i.uv - 0.5) * 2.0); half3 addRgb = _Color.rgb * a * a; return half4(addRgb, max(addRgb.r, max(addRgb.g, addRgb.b))); }
            ENDHLSL
        }
    }
}
