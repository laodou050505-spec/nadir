// 《别往下看》场景主着色器（URP，无后处理，移动端/双眼）
//  - 静态物体用烘焙光照贴图；动态物体用光照探针 + 全局两盏“虚拟灯”
//  - 反射探针 + 菲涅尔做地面湿润的反光
//  - _DLDLevel 全局亮度：灯光变暗、熄灯不用重烘焙
//  - _Grid：磨石地面的方格缝，和玩法地砖对齐
//  - 距离雾：全局 _DLDFogColor / _DLDFogDensity
Shader "DLD/Lit"
{
    Properties
    {
        _BaseMap ("贴图", 2D) = "white" {}
        _BaseColor ("颜色", Color) = (1,1,1,1)
        [Normal] _NormalMap ("法线", 2D) = "bump" {}
        _NormalScale ("法线强度", Range(0,2)) = 1
        _MaskMap ("遮罩 R 光泽 G 缝隙", 2D) = "white" {}
        _Cavity ("缝隙暗部", Range(0,1)) = 0
        _AntiTile ("防重复", Range(0,1)) = 0
        _UV2Scale ("第二层缩放", Float) = 0.73
        _UV2Off ("第二层偏移", Vector) = (0.37,0.61,0,0)
        _Grime ("大尺度老化", Range(0,1)) = 0
        _WearK ("投影磨损", Range(0,1)) = 0
        _DepthFade ("地面以下变暗", Range(0,1)) = 0
        _MinLight ("最低受光", Color) = (0,0,0,0)
        _Gloss ("反光强度", Range(0,1)) = 0.1
        _Rough ("粗糙度", Range(0,1)) = 0.5
        [HDR] _EmissionColor ("自发光", Color) = (0,0,0,1)
        _EmissionMap ("自发光贴图", 2D) = "white" {}
        _CeilRefl ("天花灯倒影", Range(0,4)) = 0
        _CrackMap ("裂纹图 R 线 G 到达时间", 2D) = "black" {}
        _CrackK ("裂纹进度", Range(0,1.2)) = 0
        _CrackRect ("裂纹区域 xz 中心 z 1/边长", Vector) = (0,0,1,0)
        [HDR] _CrackCol ("裂纹光", Color) = (0.7,0.9,0.85,1)
        _SeepK ("砖缝透光", Range(0,1)) = 0
        _EmissLevel ("自发光跟随亮度", Range(0,1)) = 0
        _Grid ("方格缝强度", Range(0,1.25)) = 0
        _GridSize ("方格边长(米)", Float) = 0.75
        _GridOff ("方格偏移", Float) = 0.5
        _LevelResponse ("跟随全局亮度", Range(0,1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("剔除", Float) = 2
        _RimK ("补光和轮廓光（触手）", Float) = 0
        _FogK ("受雾影响", Float) = 1
        [IntRange] _StencilRef ("Stencil 值", Range(0,255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil 比较", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        TEXTURE2D(_DLDNoise); SAMPLER(sampler_DLDNoise);
        TEXTURE2D(_DLDWear); SAMPLER(sampler_DLDWear);
        float _DLDLevel; float _DLDFogDensity; float4 _DLDFogColor;
        float4 _DLDLampPos[2]; float4 _DLDLampCol[2]; float4 _DLDSeep; float _DLDTile;
        float4 _DLDFlick;      // xyz 正在接触不良的那盏灯，w 它这一刻暗了多少（0 不暗）
        TEXTURE2D(_CrackMap); SAMPLER(sampler_CrackMap);
        TEXTURE2D(_DLDCeilMap); SAMPLER(sampler_DLDCeilMap); float4 _DLDCeil;   // x 天花高度 y 图覆盖的半边长
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST; half4 _BaseColor; half4 _EmissionColor; half _RimK, _FogK;
        half _Cavity, _AntiTile, _Grime, _WearK, _DepthFade; half4 _MinLight; float _UV2Scale; float4 _UV2Off; half _NormalScale, _Gloss, _Rough, _CeilRefl, _Grid, _LevelResponse, _SeepK, _EmissLevel, _CrackK; float _GridSize, _GridOff; float4 _CrackRect; half4 _CrackCol;
        CBUFFER_END
        ENDHLSL
        Pass
        {
            Name "DLDForward"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            Stencil { Ref [_StencilRef] Comp [_StencilComp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; float2 uv1 : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float2 lm : TEXCOORD1; float3 positionWS : TEXCOORD2; float3 normalWS : TEXCOORD3; float4 tangentWS : TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o = (V)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.lm = v.uv1 * unity_LightmapST.xy + unity_LightmapST.zw;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // 世界空间的大尺度噪声：同时用于防重复和老化，墙和地都能用
                float2 wp = i.positionWS.xz + i.positionWS.y * float2(0.71, 0.37);
                half macro = SAMPLE_TEXTURE2D(_DLDNoise, sampler_DLDNoise, wp * 0.047).r;
                half macro2 = SAMPLE_TEXTURE2D(_DLDNoise, sampler_DLDNoise, wp * 0.19).g;
                float2 uvB = i.uv * _UV2Scale + _UV2Off.xy;
                half mixB = _AntiTile * smoothstep(0.42, 0.58, macro + (macro2 - 0.5) * 0.4);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalScale);
                if (mixB > 0.002)
                {
                    albedo = lerp(albedo, SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvB).rgb, mixB);
                    nt = normalize(lerp(nt, UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uvB), _NormalScale), mixB));
                }
                half2 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv).rg;
                // 老化：一片旧一片更旧，略微褪色；同时打散光泽，整面墙不会一个质感
                half age = saturate((macro - 0.5) * 2.4 + (macro2 - 0.5) * 1.2 + 0.5);
                half lumA = dot(albedo, half3(0.3, 0.59, 0.11));
                albedo = lerp(albedo, lerp(albedo, lumA.xxx, age * 0.35) * (1.12 - age * 0.5), _Grime);
                albedo *= _BaseColor.rgb * lerp(1.0, mask.g, _Cavity);
                if (_WearK > 0)
                {
                    // 道具没有老化贴图：按朝向投影一层划痕掉漆，碰撞多的面和边都有
                    float3 an = abs(i.normalWS);
                    float2 wuv = an.y > 0.7 ? i.positionWS.xz : (an.x > an.z ? i.positionWS.zy : i.positionWS.xy);
                    half w = dot(SAMPLE_TEXTURE2D(_DLDWear, sampler_DLDWear, wuv * 0.9).rgb, half3(0.33, 0.34, 0.33));
                    albedo *= lerp(1.0, saturate(w / 0.34), _WearK);
                }
                float3 n = normalize(i.normalWS);
                float3 t = normalize(i.tangentWS.xyz);
                float3 b = cross(n, t) * i.tangentWS.w;
                n = normalize(nt.x * t + nt.y * b + nt.z * n);
                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                half line_grid = 0, seep = 0; half3 crackGlow = 0;
                if (_CrackK > 0)
                {
                    float2 cuv = (i.positionWS.xz - _CrackRect.xy) * _CrackRect.z + 0.5;
                    half2 c = SAMPLE_TEXTURE2D(_CrackMap, sampler_CrackMap, cuv).rg;
                    half show = c.r * smoothstep(c.g - 0.04, c.g + 0.02, _CrackK);   // 裂纹从一点爬开
                    albedo *= 1.0 - show * 0.92;
                    crackGlow = show * _CrackCol.rgb;
                }
                if (_Grid > 0 || _SeepK > 0)
                {
                    float2 g = i.positionWS.xz / _DLDTile + _GridOff;
                    float2 d = abs(frac(g) - 0.5);
                    float line_ = smoothstep(0.487, 0.492, max(d.x, d.y));
                    albedo *= 1 - line_ * _Grid * 0.8;
                    line_grid = line_ * _Grid;
                    seep = line_ * _SeepK;
                }
                half level = lerp(1, _DLDLevel, _LevelResponse);
                // 坏灯照亮的那一片跟着它一起暗：离得越近暗得越多，三四米外就不受影响
                float3 fo = i.positionWS - _DLDFlick.xyz; fo.y *= .6;
                level *= 1 - _DLDFlick.w * _LevelResponse * exp(-dot(fo, fo) * .2);
            #if defined(LIGHTMAP_ON)
                half3 diffuseLight = max(SampleLightmap(i.lm, 0, n), _MinLight.rgb);
            #else
                half3 diffuseLight = max(SampleSH(n), _MinLight.rgb);
            #endif
                half3 spec = 0;
                for (int k = 0; k < 2; k++)
                {
                    float3 L = _DLDLampPos[k].xyz - i.positionWS;
                    float d2 = dot(L, L); L /= sqrt(d2) + 1e-4;
                    half att = _DLDLampPos[k].w * 1.0 / (1.0 + d2 * 0.45);
                #if !defined(LIGHTMAP_ON)
                    diffuseLight += _DLDLampCol[k].rgb * saturate(dot(n, L)) * att;
                #endif
                    float3 h = normalize(L + viewDir);
                    spec += _DLDLampCol[k].rgb * pow(saturate(dot(n, h)), lerp(160.0, 12.0, _Rough)) * att * _Gloss * mask.r * 2.0;
                }
                half fres = pow(1.0 - saturate(dot(n, viewDir)), 4.0);
                half glossK = mask.r * lerp(1.0, 1.0 - age * 0.75, _Grime);
                half3 refl = GlossyEnvironmentReflection(reflect(-viewDir, n), i.positionWS, lerp(0.65, _Rough, glossK), 1.0) * (_Gloss * glossK * (0.16 + 0.84 * fres));
                if (_CeilRefl > 0)
                {
                    // 地面倒影：反射线打到天花平面，查亮灯分布图。比探针清楚，也有正确的视差
                    float3 ng = normalize(i.normalWS);
                    float3 r = reflect(-viewDir, normalize(lerp(ng, n, 0.5)));
                    float t = (_DLDCeil.x - i.positionWS.y) / max(r.y, 0.03);
                    float2 uv = (i.positionWS.xz + r.xz * t) / (2.0 * _DLDCeil.y) + 0.5;
                    // 磨损不均：一片亮一片哑，倒影才像真地面
                    float2 w = i.positionWS.xz;
                    half worn = 0.5 + 0.25 * (sin(w.x * 0.83 + sin(w.y * 0.61) * 1.7) + sin(w.y * 1.07 - sin(w.x * 0.47) * 2.1));
                    // 四次采样沿反射方向拉开：粗糙表面把灯拉成一道竖直的长光带
                    half m = 0;
                    float px = 0.17 * t * length(r.xz) * 512.0 / _DLDCeil.y;   // 相邻两次采样隔多少像素：模糊要盖过它，光带才连续
                    float lod = max(1.5 + worn * 1.2, log2(max(px, 1.0)));
                    m += SAMPLE_TEXTURE2D_LOD(_DLDCeilMap, sampler_DLDCeilMap, (i.positionWS.xz + r.xz * t * 0.58) / (2.0 * _DLDCeil.y) + 0.5, lod).r;
                    m += SAMPLE_TEXTURE2D_LOD(_DLDCeilMap, sampler_DLDCeilMap, (i.positionWS.xz + r.xz * t * 0.8) / (2.0 * _DLDCeil.y) + 0.5, lod).r;
                    m += SAMPLE_TEXTURE2D_LOD(_DLDCeilMap, sampler_DLDCeilMap, uv, lod).r * 1.5;
                    m += SAMPLE_TEXTURE2D_LOD(_DLDCeilMap, sampler_DLDCeilMap, (i.positionWS.xz + r.xz * t * 1.35) / (2.0 * _DLDCeil.y) + 0.5, lod).r;
                    m *= 0.3;
                    half f = 0.012 + 0.988 * pow(1.0 - saturate(dot(ng, viewDir)), 3.5);   // 俯视时几乎没有倒影，贴地看才出现
                    refl += m * f * _CeilRefl * glossK * glossK * half3(0.74, 0.93, 0.92);
                }
                if (_DepthFade > 0)
                {
                    // 探针只铺在大厅：伸进井里的部分要自己沉入黑暗，留一点冷光，苍白的轮廓才看得见
                    half down = saturate(1.0 + i.positionWS.y * 0.55);
                    diffuseLight = diffuseLight * down + half3(0.05, 0.075, 0.08) * (_RimK > 0 ? 0.06 : 1.0) * (1.0 - down) * exp(i.positionWS.y * 0.05);
                    refl *= down; spec *= down;
                }
                if (_RimK > 0)
                {
                    // 天光偏冷、井底偏暖，加一圈轮廓：深色皮肤俯视时才不是一块黑剪影
                    half upK = n.y * 0.5 + 0.5;
                    half keep = saturate(1.0 + i.positionWS.y * 0.2);
                    diffuseLight += lerp(half3(0.022, 0.014, 0.011), half3(0.03, 0.036, 0.038), upK) * _RimK * keep;
                    refl += pow(1.0 - saturate(dot(n, viewDir)), 5.0) * half3(0.03, 0.04, 0.044) * mask.g * _RimK * keep;      // 轮廓光只留很窄一圈，宽了就像蒙了层雾
                }
                half3 col = (albedo * diffuseLight + (spec + refl) * (1 - saturate(line_grid))) * level;
                col += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb * lerp(1.0, _DLDLevel, _EmissLevel);
                col += crackGlow + seep * _DLDSeep.rgb;
                float dist = distance(GetCameraPositionWS(), i.positionWS);
                col = lerp(col, lerp(_DLDFogColor.rgb, col, exp(-dist * _DLDFogDensity)), _FogK);
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/Meta"
    }
}
