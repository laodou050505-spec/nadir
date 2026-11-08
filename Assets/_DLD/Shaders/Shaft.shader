// 竖井：只透过地上的洞可见（Stencil = 1）。不烘焙，灯光按世界坐标现算：
// 每层四面各一盏暖灯（部分已坏），井壁越深越暗，融进黑暗。那只眼上来时井壁被映红。
Shader "DLD/Shaft"
{
    Properties
    {
        _BaseMap ("贴图", 2D) = "white" {}
        _BaseColor ("颜色", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("灯罩自发光", Color) = (0,0,0,1)
        _Spec ("金属反光", Range(0,1)) = 0
        _LampGlass ("是灯罩(1)", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil 比较", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-20" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Stencil { Ref 1 Comp [_StencilComp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float _DLDLevel; float4 _DLDShaft;   // x 井口世界 y，y 层高，z 灯到井心的距离，w 井壁红光强度
            float _DLDEyeY;
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST; half4 _BaseColor, _EmissionColor; half _Spec, _LampGlass;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            float hash(float x) { return frac(sin(x * 12.9898) * 43758.5453); }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);                    // 双眼一次渲染时按当前这只眼取视角
                UNITY_SETUP_INSTANCE_ID(i);
                float H = _DLDShaft.y; float R = _DLDShaft.z;
                float d = _DLDShaft.x - i.positionWS.y;                 // 深度
                float lv = floor((d + 0.5 * H) / H);                    // 最近的灯层
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half3 warm = 0;
                static const float2 dirs[4] = { float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1) };
                for (int k = 0; k < 4; k++)
                {
                    float alive = step(0.26, hash(lv * 4.0 + k)) * step(1.5, lv);   // 约四分之一的灯已经坏了；最上面两层全灭，洞口下先是黑的
                    float3 lamp = float3(dirs[k].x * R, _DLDShaft.x - (lv * H + 0.25 * H / 3.0), dirs[k].y * R);
                    float3 L = lamp - i.positionWS;
                    float dd = dot(L, L); L /= sqrt(dd) + 1e-4;
                    warm += alive * (saturate(dot(n, L)) * 0.85 + 0.15) / (0.5 + dd * 1.9);
                }
                float fade = exp(-d * 0.026);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                half3 cool = half3(0.036, 0.075, 0.085) * (0.75 - saturate(n.y) * 0.45);   // 冷色环境光：平面（圈梁顶）反而更暗，别看成近处的地面
                half topFade = saturate((d - 0.3) / 7.0); topFade *= topFade;
                half3 spill = half3(0.17, 0.25, 0.25) * exp(-d * 0.62) * (1.0 - saturate(n.y) * 0.85);   // 大厅的光从破洞渗下去：井口下几米看得见，然后沉入黑暗
                half level = lerp(0.15, 1, _DLDLevel);
                half3 col = albedo * (spill + cool * exp(-d * 0.035) * (0.25 + topFade) + warm * half3(1.0, 0.5, 0.2) * 0.3 * fade) * level;
                float3 h = normalize(viewDir + float3(0, 1, 0));
                col += _Spec * pow(saturate(dot(n, h)), 30.0) * half3(0.5, 0.4, 0.3) * saturate(dot(warm, half3(1, 1, 1)) * 0.6) * fade * level * 0.4;   // 只有灯照到的地方才有高光
                float alive0 = 1;
                if (_LampGlass > 0.5)
                {
                    float ang = atan2(i.positionWS.z, i.positionWS.x);
                    float k = abs(i.positionWS.x) > abs(i.positionWS.z) ? (i.positionWS.x > 0 ? 0 : 1) : (i.positionWS.z > 0 ? 2 : 3);
                    alive0 = step(0.26, hash(floor((d + 0.5 * H) / H) * 4.0 + k)) * step(1.5, floor((d + 0.5 * H) / H));
                    col = lerp(half3(0.015, 0.012, 0.01), _EmissionColor.rgb * pow(fade, 0.5) * level, alive0);
                }
                // 那只眼沿井升上来：它附近的井壁被映红
                col += half3(0.34, 0.018, 0.006) * albedo * _DLDShaft.w * exp(-max(i.positionWS.y - _DLDEyeY, 0.0) * 0.34 - max(_DLDEyeY - i.positionWS.y, 0.0) * 0.5) * saturate((d - 2.0) / 6.0);
                col = lerp(half3(0.002, 0.0035, 0.004), col, exp(-d * 0.016));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
