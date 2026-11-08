// 醒来时的眼皮：相机上的一个球，上下两片软边的黑色弧线。_Open 0 全黑，1 完全睁开
Shader "DLD/Lids"
{
    Properties { _Open ("睁开", Range(0,1)) = 1 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay+60" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off ZTest Always Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) float _Open; CBUFFER_END
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 d : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.d = v.positionOS.xyz; return o; }
            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.d);
                if (d.z < .06) return half4(0, 0, 0, 1);
                float2 p = d.xy / d.z;
                float arch = saturate(1 - .2 * p.x * p.x);             // 眼缝两头收拢
                float top = (_Open * 2.7 - .1) * arch + .02, low = -(_Open * 2.0 - .08) * arch - .02;
                float soft = .08 + _Open * .4;                         // 刚睁开时边缘更清楚，睁大了就散开
                float lid = max(smoothstep(top - soft, top + soft * .4, p.y), 1 - smoothstep(low - soft * .4, low + soft, p.y));
                float a = max(lid, (1 - _Open) * (1 - _Open) * .55);   // 没睁开时整个世界也还是暗的
                return half4(0, 0, 0, a);
            }
            ENDHLSL
        }
    }
}
