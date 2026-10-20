// Flat comic blood stain for the ThatIndividual stain particles (floor pools, wall splats).
// Unlit, so a stain reads the same on a lit floor and on a wall in shade (PS1 clarity).
// Mask = texture red * texture alpha: covers the vendor grey masks (blood N, splat N) and the
// alpha pools (pool N). Particle vertex colour multiplies colour and alpha, so the vendor fade still works.
Shader "WarspiteP2/GoreStainUnlit"
{
    Properties
    {
        _BaseMap ("Mask", 2D) = "white" {}
        _Color ("Color", Color) = (0.36, 0, 0.02, 1)
        _EdgeColor ("Edge Color", Color) = (0.6, 0.02, 0.03, 1)
        _MaskLow ("Mask Low", Range(0, 1)) = 0.25
        _MaskHigh ("Mask High", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

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
                half mask = tex.r * tex.a;
                half alpha = smoothstep(_MaskLow, _MaskHigh, mask);
                // A brighter rim where the mask is thin gives the stain a readable comic edge.
                half3 rgb = lerp(_EdgeColor.rgb, _Color.rgb, smoothstep(_MaskHigh, 1.0h, mask));
                return half4(rgb, alpha * _Color.a * input.color.a);
            }
            ENDHLSL
        }
    }
}
