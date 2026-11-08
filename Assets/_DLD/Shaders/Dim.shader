// 恐怖版压暗真实房间：透视模式下相机清屏是全透明，这里写入一层半透明的黑，真实画面就暗下去
Shader "DLD/Dim"
{
    Properties { _Alpha ("压暗", Range(0,1)) = 0.62 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero ZWrite Off Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half _Alpha; CBUFFER_END
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(V i) : SV_Target { return half4(0, 0, 0, _Alpha); }
            ENDHLSL
        }
    }
}
