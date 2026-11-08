// 透视模式下的手：不画手，只在手的位置把虚拟画面抠掉，露出摄像头拍到的真手。
// _DLDHandAlpha 和压暗层一致，真手和周围的现实一样暗
Shader "DLD/HandHole"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+450" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On Blend Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _DLDHandAlpha;
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(V i) : SV_Target { return half4(0, 0, 0, _DLDHandAlpha); }
            ENDHLSL
        }
    }
}
