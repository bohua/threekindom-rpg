// Region ground with many layers (chapter 1 refinement, user 2026-10-28 "先精修"): every painted ground of a region
// (wheat, reeds, sand bars, loess road, alkali flats ...) sits in one texture array; a 2 px/m index map stores, per
// texel, the two strongest grounds there (R, G = layer index / 255) and how much of the second shows (B). The index
// map is baked from the Codex class mask with blurred, noise-broken weights (tools/regions/bake_ground.py), and the
// shader blends the four nearest texels by hand, so grounds meet in wandering transitions, never square patches
// (user 2026-10-25). Lit like the other ground shaders: main light and its shadows, ambient probe, fog.
Shader "ThreeKingdoms/GroundArraySplat"
{
    Properties
    {
        _Index ("Index map (R, G layer / 255, B weight of G)", 2D) = "black" {}
        _Layers ("Ground layers", 2DArray) = "" {}
        _TileMetres ("Metres per tile", Float) = 4
        _Bounds ("Index map bounds (x min, z min, x max, z max)", Vector) = (0, 0, 560, 360)
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _RockLayer ("Layer drawn on steep slopes (-1 = none)", Float) = -1
        _Rock ("Rock share (bake_rock.py, 1 px per metre)", 2D) = "black" {}
        _OuterLayer ("Layer past the region's edge", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma require 2darray
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Index); float4 _Index_TexelSize;
            TEXTURE2D(_Rock); SAMPLER(sampler_Rock);
            TEXTURE2D_ARRAY(_Layers);
            SAMPLER(sampler_point_clamp);
            SAMPLER(sampler_trilinear_repeat);
            CBUFFER_START(UnityPerMaterial)
                float4 _Index_ST;
                float _TileMetres;
                float4 _Bounds;
                half4 _Tint;
                float _RockLayer;
                float _OuterLayer;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float fog : TEXCOORD2; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 c = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(c), Hash(c + float2(1, 0)), u.x), lerp(Hash(c + float2(0, 1)), Hash(c + float2(1, 1)), u.x), u.y);
            }

            // One layer without visible repeats (user 2026-10-28: "地面……不够精细"): the tile is sampled twice, the second
            // time larger, turned and shifted, and the two are mixed by a slow noise, so no 8 m square reads twice.
            half3 Layer(float2 uv, float idx, float mixN)
            {
                half3 a = SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_trilinear_repeat, uv, idx).rgb;
                float2 uv2 = float2(uv.x * 0.62 - uv.y * 0.39, uv.x * 0.39 + uv.y * 0.62) + 0.43;
                half3 b = SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_trilinear_repeat, uv2, idx).rgb;
                return lerp(a, b, smoothstep(0.3, 0.7, mixN));
            }

            // One index texel's ground: its two layers mixed by its weight.
            half3 Texel(float2 tuv, float2 uv, float mixN)
            {
                float4 s = SAMPLE_TEXTURE2D(_Index, sampler_point_clamp, tuv);
                float a = round(s.r * 255.0), b = round(s.g * 255.0);
                return lerp(Layer(uv, a, mixN), Layer(uv + 0.37, b, mixN), s.b);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 xz = i.positionWS.xz;
                float2 cuv = (xz - _Bounds.xy) / (_Bounds.zw - _Bounds.xy);
                float2 uv = xz / _TileMetres;
                // Hand-made bilinear over the four nearest index texels (indices must not be filtered).
                float2 t = cuv * _Index_TexelSize.zw - 0.5;
                float2 f = frac(t), base = (floor(t) + 0.5) * _Index_TexelSize.xy;
                float2 dx = float2(_Index_TexelSize.x, 0), dy = float2(0, _Index_TexelSize.y);
                float mixN = Noise(xz * 0.045);
                half3 c00 = Texel(base, uv, mixN), c10 = Texel(base + dx, uv, mixN), c01 = Texel(base + dy, uv, mixN), c11 = Texel(base + dx + dy, uv, mixN);
                half3 albedo = lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y) * _Tint.rgb;
                // Large, soft light and dark drifts across the open country (sun-bleached and damp patches).
                albedo *= 0.9 + 0.2 * (Noise(xz * 0.012 + 7.0) * 0.7 + Noise(xz * 0.05) * 0.3);
                float3 n = normalize(i.normalWS);
                // Past the region's edge the clamped index would smear its last row outward: the ground there turns into
                // the region's common grass over 40 m instead (user 2026-10-30: the edges showed streaks and nothing).
                float2 outside = max(_Bounds.xy - xz, xz - _Bounds.zw);
                float beyond = saturate(max(outside.x, outside.y) / 40.0);
                if (beyond > 0) albedo = lerp(albedo, Layer(uv, _OuterLayer, mixN) * _Tint.rgb * (0.9 + 0.2 * Noise(xz * 0.012 + 7.0)), beyond);
                // Steep slopes show the region's rock (user 2026-10-30: mountains were one sheet of grass). The share is
                // baked from the smoothed heightmap with noise, so it fades in over metres, never along a terrace edge;
                // projected from the side so a cliff face is not one smeared texel.
                if (_RockLayer >= 0)
                {
                    float steep = SAMPLE_TEXTURE2D(_Rock, sampler_Rock, cuv).r;
                    steep = max(steep, beyond * smoothstep(0.93, 0.7, n.y));
                    if (steep > 0.003)
                    {
                        float3 w = pow(abs(n), 4); w /= max(dot(w, 1), 1e-4);
                        float3 q = i.positionWS / _TileMetres;
                        half3 rock = SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_trilinear_repeat, q.zy, _RockLayer).rgb * w.x
                                   + SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_trilinear_repeat, q.xz, _RockLayer).rgb * w.y
                                   + SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_trilinear_repeat, q.xy, _RockLayer).rgb * w.z;
                        albedo = lerp(albedo, rock * _Tint.rgb, steep);
                    }
                }
                Light ml = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 c = albedo * (ml.color * (ml.shadowAttenuation * ml.distanceAttenuation) * saturate(dot(n, ml.direction)) + SampleSH(n));
                return half4(MixFog(c, i.fog), 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Unlit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Unlit/DEPTHNORMALSONLY"
    }
}
