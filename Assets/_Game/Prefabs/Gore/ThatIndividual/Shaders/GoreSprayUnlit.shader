// Flat comic blood for the ThatIndividual spray particles (drops, splashes, mist, flipbooks).
// Unlit and saturated, so a spray reads as loud red in any light (CONTRACT section 8: gore is the loudest feedback).
// Mask = texture red * texture alpha (the vendor masks are grey with alpha, or opaque grey).
// _Procedural = 1 for vendor materials with no texture: a round drop from the particle UV.
// Particle vertex alpha multiplies alpha, so the vendor fades still work. Vertex RGB is ignored on purpose:
// the vendor start colours are dark reds that would undo the tint.
Shader "WarspiteP2/GoreSprayUnlit"
{
    Properties
    {
        _BaseMap ("Mask", 2D) = "white" {}
        _Color ("Color", Color) = (0.85, 0.02, 0.04, 1)
        _EdgeColor ("Edge Color", Color) = (0.45, 0, 0.02, 1)
        _Procedural ("Procedural Drop", Range(0, 1)) = 0
        _MaskLow ("Mask Low", Range(0, 1)) = 0.08
        _MaskHigh ("Mask High", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                half4 _EdgeColor;
                half _Procedural;
                half _MaskLow;
                half _MaskHigh;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half texMask = tex.r * tex.a;
                half drop = 1.0h - smoothstep(0.3h, 0.5h, length(input.uv - 0.5));
                half mask = lerp(texMask, drop, _Procedural);
                half alpha = smoothstep(_MaskLow, _MaskHigh, mask);
                half3 rgb = lerp(_EdgeColor.rgb, _Color.rgb, smoothstep(_MaskLow, _MaskHigh + 0.2h, mask));
                return half4(rgb, alpha * _Color.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
