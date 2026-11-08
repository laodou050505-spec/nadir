// 井口遮罩：最先画，只写 Stencil = 1；它覆盖的区域才看得到井里
Shader "DLD/Mask"
{
    Properties { [Enum(UnityEngine.Rendering.CullMode)] _Cull ("剔除", Float) = 2 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-100" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ColorMask 0 ZWrite Off Cull [_Cull]
            Stencil { Ref 1 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
