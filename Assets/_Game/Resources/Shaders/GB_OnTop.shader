// The lock marker (Bruno, 2026-11-16: "the indicator is hidden behind walls and other obstacles. it needs to be seen no matter
// what"): a flat colour drawn over everything, after the transparent queue, with no depth test.
Shader "GB/OnTop"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.15, 0.15, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Overlay" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "OnTop"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                return o;
            }

            half4 frag (Varyings i) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
