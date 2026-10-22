// The 1999 sheen on the background (ART_LESSONS 1, CONCEPTS 7): the frame quantized to _Levels per channel
// in display space with a 4 x 4 Bayer dither. The characters keep their colours: the stencil bit 1 marks them
// (the CharacterStencil RenderObjects feature) and this pass skips those pixels. A full-screen pass of the
// GB_Full renderer, before post-processing, with the depth-stencil attachment bound.
Shader "Hidden/GB/Dither"
{
    Properties
    {
        _Levels ("Levels per channel", Float) = 32
        _DitherStrength ("Dither strength", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Stencil
        {
            Ref 1
            Comp NotEqual
            Pass Keep
        }
        Pass
        {
            Name "GB Dither"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // The same includes as URP's own CoreBlit.shader: Core.hlsl brings the XR texture macros Blit.hlsl uses.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            float _Levels;
            float _DitherStrength;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);
                // The Bayer 4 x 4 matrix, centred on zero: -0.5 .. +0.5 in steps of a 16th.
                static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
                uint2 p = uint2(input.positionCS.xy) & 3;
                float d = (bayer[p.y * 4 + p.x] + 0.5) / 16.0 - 0.5;
                float steps = max(_Levels, 2.0) - 1.0;
                float3 srgb = LinearToSRGB(saturate(c.rgb));
                srgb = floor(srgb * steps + d * _DitherStrength + 0.5) / steps;
                c.rgb = SRGBToLinear(srgb);
                return c;
            }
            ENDHLSL
        }
    }
}
