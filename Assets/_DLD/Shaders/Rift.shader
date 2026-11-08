// 裂隙里面：走进传送门之后、到下一层之前停留的那一小段。
// 相机上的一个向内的球，但颜色只按视线方向算（和天空一样在无穷远）：走动、探头都没有视差，不会晕。
// 很暗的虚空，眼睛高度一圈微弱的余烬色雾，云一样的明暗慢慢变化（每秒转不到 1°），
// 前方远处一团很淡的暖光（出口的方向），零星的余烬光点慢慢往上飘（近的一层快一点、远的一层慢一点）、各自轻轻晃动、明灭。整体很暗，不刺眼，不闪。
// 不透明：透视模式下也完全盖住真实房间
Shader "DLD/Rift"
{
    Properties { _Fade ("亮起", Range(0,1)) = 0 _Dir ("出口方向", Vector) = (0,0,1,0) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay+35" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One Zero ZWrite Off ZTest Always Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_DLDNoise); SAMPLER(sampler_DLDNoise);
            CBUFFER_START(UnityPerMaterial) half _Fade; float4 _Dir; CBUFFER_END
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionWS = TransformObjectToWorld(v.positionOS.xyz); o.positionCS = TransformWorldToHClip(o.positionWS); return o; }

            float H(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            // 一层余烬：把方向切成小格，每格最多一个光点，慢慢往上飘，轻轻明灭
            half Embers(float2 sph, float scale, float rise, float keep, float t)
            {
                float2 g = sph * scale; g.y -= t * rise; g.x += t * rise * .25;   // 整层慢慢往上飘，略带一点斜
                float2 c = floor(g), f = frac(g) - .5;
                float h = H(c), h2 = H(c + 17.3);
                // 每个光点在自己的位置附近轻轻左右、上下晃（周期 6～13 秒），像在空气里浮着
                float2 sway = float2(sin(t * (.5 + h * .5) + h2 * 40), cos(t * (.35 + h2 * .4) + h * 30) * .6) * .11;
                float2 o = (float2(h2, H(c + 5.1)) - .5) * .4 + sway;
                float dd = length(f - o);
                half on = step(keep, h);
                half tw = .55 + .45 * sin(t * (.35 + h2 * .5) + h * 40);      // 很慢的明灭，周期 7～18 秒
                return on * tw * exp(-dd * dd / .01) * .8;                        // 软的光点，没有尖锐的亮核
            }

            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);                    // 双眼一次渲染时，按当前这只眼取相机位置（否则右眼也用左眼的位置，两眼画面对不上）
                float3 d = normalize(i.positionWS - GetCameraPositionWS());
                float a0 = _Time.y * .012; float s = sin(a0), c = cos(a0);
                float3 q = float3(c * d.x - s * d.z, d.y, s * d.x + c * d.z);
                half n1 = SAMPLE_TEXTURE2D_LOD(_DLDNoise, sampler_DLDNoise, q.xz * .42 + q.y * .21, 0).r;
                half n2 = SAMPLE_TEXTURE2D_LOD(_DLDNoise, sampler_DLDNoise, q.zx * 1.1 - q.y * .37 + .5, 0).r;
                half cloud = saturate((n1 * .65 + n2 * .35 - .35) * 1.6);
                half band = exp(-d.y * d.y / .06);                              // 眼睛高度一圈余烬色雾
                half ahead = pow(saturate(dot(d, normalize(_Dir.xyz))), 6);     // 出口方向一团很淡的暖光
                // 很暗，但不是死黑：眼睛高度一圈微弱的暖色雾，出口方向一团微弱的暖光，都不刺眼
                half3 col = half3(.003, .002, .0015)
                          + half3(.011, .005, .0025) * band * (.5 + cloud)
                          + half3(.006, .003, .0015) * cloud * (1 - abs(d.y))
                          + half3(.022, .012, .005) * ahead;
                float2 sph = float2(atan2(d.x, d.z), asin(clamp(d.y, -1, 1)));
                half em = Embers(sph, 16, .62, .88, _Time.y) + Embers(sph + 3.1, 10, .23, .92, _Time.y + 9) * .7;
                col += half3(.5, .24, .08) * em * .12 * (1 - smoothstep(.7, .95, abs(d.y)));
                return half4(col * _Fade, 1);
            }
            ENDHLSL
        }
    }
}
