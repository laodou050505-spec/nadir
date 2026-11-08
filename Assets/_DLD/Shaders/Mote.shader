// 深渊里的飞尘：柔软的亮点，只透过洞可见
Shader "DLD/Mote"
{
    Properties { [MainTexture] _BaseMap ("贴图", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-4" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One ZWrite Off
            Stencil { Ref 1 Comp Equal }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.color = v.color; o.uv = v.uv; return o; }
            half4 frag(V i) : SV_Target { half a = saturate(1.0 - length(i.uv - 0.5) * 2.0); half3 addRgb = i.color.rgb * i.color.a * a * a; return half4(addRgb, max(addRgb.r, max(addRgb.g, addRgb.b))); }
            ENDHLSL
        }
    }
}
