// 地上的活动范围线：只画一条细亮线和很淡的余晖，线宽按米算，范围变大也不变粗
Shader "DLD/AreaLine"
{
    Properties { _Color ("颜色", Color) = (1,.8,.55,1) _Half ("半宽(米)", Float) = 1.875 _Reveal ("显影", Range(0,1)) = 1 _Size ("网格边长(米)", Float) = 7 _Line ("线宽(米)", Float) = .006 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off ZTest LEqual Cull Off Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color; float _Half, _Reveal, _Size, _Line; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 p : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.p = (v.uv - .5) * _Size; return o; }
            half4 frag(V i) : SV_Target
            {
                float2 q = abs(i.p);
                float d = abs(max(q.x, q.y) - _Half);
                float aa = max(fwidth(d), .001);
                float core = 1 - smoothstep(_Line, _Line + aa * 1.5, d);
                float haze = exp(-d * d / .007) * .07;
                // 从四个角同时向边中点画开，再有一道极慢的光沿线走
                float along = 1 - min(q.x, q.y) / max(_Half, .01);
                float show = smoothstep(0, .05, _Reveal * 1.05 - (1 - along));
                float flow = .72 + .28 * sin(atan2(i.p.y, i.p.x) * 4 - _Time.y * 1.1);
                float v = (core * .6 + haze) * show * flow;
                half3 addRgb = _Color.rgb * v; return half4(addRgb, max(addRgb.r, max(addRgb.g, addRgb.b)));
            }
            ENDHLSL
        }
    }
}
