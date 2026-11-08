// 光晕：始终面向相机的柔光片，离太近时淡出
Shader "DLD/Halo"
{
    Properties { [HDR] _Color ("颜色", Color) = (1,0.55,0.2,1) _Size ("半径", Float) = 0.3 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color; float _Size; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half fade : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 c = TransformObjectToWorld(float3(0, 0, 0));
                float3 cam = GetCameraPositionWS();
                float3 f = normalize(cam - c);
                float3 r = normalize(cross(float3(0, 1, 0), f));
                float3 u = cross(f, r);
                float2 q = (v.uv - 0.5) * 2.0 * _Size * length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                o.positionCS = TransformWorldToHClip(c + r * q.x + u * q.y);
                o.uv = v.uv;
                o.fade = saturate((distance(cam, c) - 0.25) / 0.5);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);                    // 双眼一次渲染时，按当前这只眼取相机位置（否则右眼也用左眼的位置，两眼画面对不上）
                half a = saturate(1.0 - length(i.uv - 0.5) * 2.0);
                half3 addRgb = _Color.rgb * a * a * a * i.fade; return half4(addRgb, max(addRgb.r, max(addRgb.g, addRgb.b)));
            }
            ENDHLSL
        }
    }
}
