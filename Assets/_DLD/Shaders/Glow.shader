// 发光物：_Rim=0 实心光芯；_Rim=1 玻璃壳（只有边缘亮），都不受场景亮度影响，熄灯时也看得见
Shader "DLD/Glow"
{
    Properties { [HDR] _Color ("颜色", Color) = (1,0.6,0.25,1) _Rim ("玻璃", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color; half _Rim; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);                    // 双眼一次渲染时按当前这只眼取视角
                half nv = saturate(dot(normalize(i.normalWS), normalize(GetWorldSpaceViewDir(i.positionWS))));
                half core = 0.65 + 0.6 * nv * nv;
                half rim = pow(1.0 - nv, 2.5) * 0.7 + 0.02;
                half3 addRgb = _Color.rgb * lerp(core, rim, _Rim); return half4(addRgb, max(addRgb.r, max(addRgb.g, addRgb.b)));
            }
            ENDHLSL
        }
    }
}
