// T38 (the Arkham Knight look), switch 4: the planar reflection of the floor.
// A thin quad just over the floor adds the mirrored room (AKPlanarReflection renders it at half size into
// _AKPlanarTex) on top of the lit floor. The strength follows a Fresnel term and a puddle mask, so the reflection is
// strong at a grazing angle and in the wet patches, weak straight down. Additive: the floor's own light stays.
Shader "GB/AK/PlanarOverlay"
{
    Properties
    {
        _Strength ("Strength", Float) = 0.8
        _Fresnel ("Fresnel power", Float) = 2.5
        _Base ("Strength straight down", Float) = 0.08
        _MaskTex ("Puddle mask (R)", 2D) = "white" {}
        _MaskScale ("Mask scale (1 / metres)", Float) = 0.18
        _MaskLow ("Strength outside the puddles", Float) = 0.35
        _Distort ("Distortion by the mask", Float) = 0.006
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-60" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "PlanarOverlay"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_AKPlanarTex); SAMPLER(sampler_AKPlanarTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            float _AKPlanarOn;

            CBUFFER_START(UnityPerMaterial)
                float _Strength, _Fresnel, _Base, _MaskScale, _MaskLow, _Distort;
                float4 _MaskTex_ST;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float4 screen : TEXCOORD1; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.screen = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.screen.xy / i.screen.w;
                float3 view = normalize(GetCameraPositionWS() - i.positionWS);
                float fresnel = _Base + (1.0 - _Base) * pow(1.0 - saturate(view.y), _Fresnel);
                float mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.positionWS.xz * _MaskScale).r;
                float2 shift = (mask - 0.5) * _Distort;
                half3 reflection = SAMPLE_TEXTURE2D(_AKPlanarTex, sampler_AKPlanarTex, uv + shift).rgb;
                float k = _Strength * fresnel * lerp(_MaskLow, 1.0, mask) * _AKPlanarOn;
                return half4(reflection * k, 0);
            }
            ENDHLSL
        }
    }
}
