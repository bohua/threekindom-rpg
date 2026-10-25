// Ink-wash screen pass for the Bohai ink trial (user 2026-10-25: the whole picture should read as a real ink-and-wash
// painting). Runs as a URP Full Screen Pass on the trial renderer only: ink lines where depth or brightness breaks
// (silhouettes of trees, banks, walls), colours pulled toward a translucent wash, edge darkening like pooled pigment,
// and rice-paper fibre multiplied over everything.
Shader "ThreeKingdoms/InkWashScreen"
{
    Properties
    {
        _LineStrength ("Ink line strength", Range(0, 1)) = 0.6
        _LineWidth ("Ink line width (px)", Range(0.5, 3)) = 1.4
        _DepthEdge ("Depth edge threshold", Range(0.001, 0.2)) = 0.025
        _ColourEdge ("Brightness edge threshold", Range(0.02, 0.6)) = 0.16
        _Wash ("Wash (desaturate and lift)", Range(0, 1)) = 0.2
        _Paper ("Paper fibre", Range(0, 1)) = 0.35
        _PaperTint ("Paper tint", Color) = (0.95, 0.92, 0.85, 1)
        _InkColour ("Ink colour", Color) = (0.13, 0.13, 0.14, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off
        Pass
        {
            Name "InkWash"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float _LineStrength, _LineWidth, _DepthEdge, _ColourEdge, _Wash, _Paper;
            float4 _PaperTint, _InkColour;

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            float Lum(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }
            float Depth(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }
            float3 Col(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 px = _LineWidth / _ScreenParams.xy;
                // Brush wobble: the sampling offset drifts slowly so lines thicken and thin like a brush.
                float wobble = 0.6 + 0.8 * Noise(uv * _ScreenParams.xy / 23.0);
                px *= wobble;

                float3 c = Col(uv);
                float d = Depth(uv);
                // Sobel on depth (relative, so far edges are as clean as near ones) and on brightness.
                float dl = Depth(uv - float2(px.x, 0)), dr = Depth(uv + float2(px.x, 0));
                float dd = Depth(uv - float2(0, px.y)), du = Depth(uv + float2(0, px.y));
                float depthEdge = (abs(dr - dl) + abs(du - dd)) / max(d, 0.01);
                float ll = Lum(Col(uv - float2(px.x, 0))), lr = Lum(Col(uv + float2(px.x, 0)));
                float ld = Lum(Col(uv - float2(0, px.y))), lu = Lum(Col(uv + float2(0, px.y)));
                float lumEdge = abs(lr - ll) + abs(lu - ld);
                float ink = saturate(smoothstep(_DepthEdge, _DepthEdge * 3, depthEdge) + smoothstep(_ColourEdge, _ColourEdge * 2.5, lumEdge) * 0.6);
                // Dry-brush breaks in the line.
                ink *= smoothstep(0.15, 0.55, Noise(uv * _ScreenParams.xy / 6.0 + 17.0));
                ink *= _LineStrength;

                // Wash: a translucent watercolour keeps its colour (the reference is green and ochre, not sepia): only a
                // touch of desaturation and lifted shadows.
                float l = Lum(c);
                float3 wash = lerp(c, l.xxx, _Wash * 0.3);
                wash = lerp(wash, 1 - (1 - wash) * 0.88, _Wash);   // lift
                // Pooled pigment: slightly darker where the brightness changes.
                wash *= 1 - saturate(lumEdge * 1.2) * 0.12 * _Wash;

                // Rice paper: large soft blotches plus fine fibres.
                float2 sp = uv * _ScreenParams.xy;
                float fibre = Noise(sp / 2.5) * 0.5 + Noise(sp / 9.0) * 0.3 + Noise(sp / 60.0) * 0.2;
                float paper = lerp(1, 0.86 + 0.18 * fibre, _Paper);
                wash *= paper * lerp(float3(1, 1, 1), _PaperTint.rgb / max(Lum(_PaperTint.rgb), 0.01), 0.06 * _Paper);

                float3 o = lerp(wash, _InkColour.rgb, ink);
                return half4(o, 1);
            }
            ENDHLSL
        }
    }
}
