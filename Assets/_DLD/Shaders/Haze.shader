// 灯下光雾：叠加的体积假象。uv.y=1 在灯口，往下淡出；边缘按视角淡出，离太近也淡出（VR 里不能糊脸）
Shader "DLD/Haze"
{
    Properties { _Color ("颜色", Color) = (0.5,0.7,0.65,1) _Power ("强度", Range(0,1)) = 0.12
        _Edge ("边缘柔度", Range(0,3)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil 比较", Float) = 8 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off Cull Off
            Stencil { Ref 1 Comp [_StencilComp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _DLDLevel;
            CBUFFER_START(UnityPerMaterial) half4 _Color; half _Power; half _Edge; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); o.uv = v.uv; o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);                    // 双眼一次渲染时，按当前这只眼取相机位置（否则右眼也用左眼的位置，两眼画面对不上）
                float3 view = normalize(GetWorldSpaceViewDir(i.positionWS));
                half edge = pow(abs(dot(normalize(i.normalWS), view)), 1.5 + _Edge);
                half d = saturate((distance(GetCameraPositionWS(), i.positionWS) - 0.4) / 1.2);
                half a = i.uv.y * i.uv.y * edge * d * _Power * _DLDLevel;
                half3 addRgb = _Color.rgb * a; return half4(addRgb, max(addRgb.r, max(addRgb.g, addRgb.b)));
            }
            ENDHLSL
        }
    }
}
