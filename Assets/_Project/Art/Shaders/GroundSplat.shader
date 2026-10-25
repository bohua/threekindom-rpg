// Open-country ground for large regions (Bohai plain, user 2026-10-25: the plain looked like one repeating carpet).
// Five ground layers chosen per texel from a 1 px/m control map: base grass, then the R/G/B/A layers where their
// weight beats a per-texel threshold, so two grounds meet in a ragged, dithered pixel border instead of a straight
// seam or a smooth blend. Texels are snapped to the 30 px/m grid, sampled point, and lit like URP Lit (main light,
// its shadows, ambient probe, fog).
Shader "ThreeKingdoms/GroundSplat"
{
    Properties
    {
        _Control ("Control (R,G,B,A weights over the base)", 2D) = "black" {}
        _Base ("Base layer", 2D) = "white" {}
        _LayerR ("Layer R", 2D) = "white" {}
        _LayerG ("Layer G", 2D) = "white" {}
        _LayerB ("Layer B", 2D) = "white" {}
        _LayerA ("Layer A", 2D) = "white" {}
        _TileMetres ("Metres per tile", Float) = 12
        _Bounds ("Control map bounds (x min, z min, x max, z max)", Vector) = (-560, -330, 560, 340)
        _PixelsPerMetre ("Pixels per metre", Float) = 30
        _Ragged ("Border raggedness", Range(0, 0.5)) = 0.22
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Control); SAMPLER(sampler_Control);
            TEXTURE2D(_Base); TEXTURE2D(_LayerR); TEXTURE2D(_LayerG); TEXTURE2D(_LayerB); TEXTURE2D(_LayerA);
            SAMPLER(sampler_point_repeat);
            CBUFFER_START(UnityPerMaterial)
                float4 _Control_ST;
                float _TileMetres;
                float4 _Bounds;
                float _PixelsPerMetre;
                float _Ragged;
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

            // Value noise on a coarser grid (clumps of a few texels), so borders break up in small pixel clusters.
            float Clumps(float2 texel)
            {
                float2 cell = floor(texel / 3.0), f = frac(texel / 3.0);
                float a = Hash(cell), b = Hash(cell + float2(1, 0)), c = Hash(cell + float2(0, 1)), d = Hash(cell + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 texel = floor(i.positionWS.xz * _PixelsPerMetre) + 0.5;
                float2 snapped = texel / _PixelsPerMetre;
                float2 cuv = (snapped - _Bounds.xy) / (_Bounds.zw - _Bounds.xy);
                float4 w = SAMPLE_TEXTURE2D(_Control, sampler_Control, cuv);
                float2 uv = snapped / _TileMetres;
                // Each layer gets its own threshold so their borders do not line up.
                float n = Clumps(texel) * 0.7 + Hash(texel) * 0.3;
                float t0 = 0.5 + (n - 0.5) * 2.0 * _Ragged;
                float t1 = 0.5 + (frac(n + 0.37) - 0.5) * 2.0 * _Ragged;
                float t2 = 0.5 + (frac(n + 0.61) - 0.5) * 2.0 * _Ragged;
                float t3 = 0.5 + (frac(n + 0.83) - 0.5) * 2.0 * _Ragged;
                half3 albedo = SAMPLE_TEXTURE2D(_Base, sampler_point_repeat, uv).rgb;
                if (w.r > t0) albedo = SAMPLE_TEXTURE2D(_LayerR, sampler_point_repeat, uv + 0.31).rgb;
                if (w.g > t1) albedo = SAMPLE_TEXTURE2D(_LayerG, sampler_point_repeat, uv + 0.57).rgb;
                if (w.b > t2) albedo = SAMPLE_TEXTURE2D(_LayerB, sampler_point_repeat, uv + 0.13).rgb;
                if (w.a > t3) albedo = SAMPLE_TEXTURE2D(_LayerA, sampler_point_repeat, uv + 0.79).rgb;

                float3 n3 = normalize(i.normalWS);
                Light main = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 lit = main.color * (main.shadowAttenuation * main.distanceAttenuation) * saturate(dot(n3, main.direction)) + SampleSH(n3);
                half3 color = albedo * lit;
                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Unlit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Unlit/DEPTHNORMALSONLY"
    }
}
