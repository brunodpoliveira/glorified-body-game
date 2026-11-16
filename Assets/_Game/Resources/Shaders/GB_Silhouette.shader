// The locked target behind a wall (Bruno, 2026-11-16: "if the enemy is not visible by the camera the enemy silhouette must be
// highlighted"): the body drawn again in a flat colour, only where something nearer hides it (ZTest Greater). Opaque: where
// two limbs overlap behind the wall, the colour stays one flat colour.
Shader "GB/Silhouette"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 0.25, 0.2, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Silhouette"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite Off
            ZTest Greater
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
