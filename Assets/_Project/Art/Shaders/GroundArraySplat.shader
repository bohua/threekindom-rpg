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
            TEXTURE2D_ARRAY(_Layers);
            SAMPLER(sampler_point_clamp);
            SAMPLER(sampler_trilinear_repeat);
            CBUFFER_START(UnityPerMaterial)
                float4 _Index_ST;
                float _TileMetres;
                float4 _Bounds;
                half4 _Tint;
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

            // One index texel's ground: its two layers mixed by its weight.
            half3 Texel(float2 tuv, float2 uv)
            {
                float4 s = SAMPLE_TEXTURE2D(_Index, sampler_point_clamp, tuv);
                float a = round(s.r * 255.0), b = round(s.g * 255.0);
                half3 ca = SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_trilinear_repeat, uv, a).rgb;
                half3 cb = SAMPLE_TEXTURE2D_ARRAY(_Layers, sampler_trilinear_repeat, uv + 0.37, b).rgb;
                return lerp(ca, cb, s.b);
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
                half3 c00 = Texel(base, uv), c10 = Texel(base + dx, uv), c01 = Texel(base + dy, uv), c11 = Texel(base + dx + dy, uv);
                half3 albedo = lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y) * _Tint.rgb;
                float3 n = normalize(i.normalWS);
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
