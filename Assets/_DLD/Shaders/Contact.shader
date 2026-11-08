// 接触阴影：脚下一小片乘法暗影，让不进光照贴图的动态物体贴住地面
Shader "DLD/Contact"
{
    Properties { _Strength ("强度", Range(0,1)) = 0.8 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+40" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend DstColor Zero, Zero One ZWrite Off Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half _Strength; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }
            half4 frag(V i) : SV_Target
            {
                float r = length(i.uv - 0.5) * 2.0;
                half a = saturate(1.0 - r); a = a * a * (3.0 - 2.0 * a);
                return half4((1.0 - a * _Strength).xxx, 1);
            }
            ENDHLSL
        }
    }
}
