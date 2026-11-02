// T26 remake look, switch 5: the light in the air. A ray march from the camera to the depth of each pixel, at
// half size: at each step the chosen additional lights (their colour, their fall-off, their cone and their real-time
// shadow map) scatter into the view through a thin, slowly moving dust density. Characters and props block it like
// they block the light (the same shadow maps). Pass 0 marches, passes 1 and 2 blur, pass 3 adds the result over the frame.
Shader "Hidden/GB/Volumetric"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }
        ZWrite Off ZTest Always Cull Off

        HLSLINCLUDE
        #define _ADDITIONAL_LIGHT_SHADOWS 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _VolLightPos[4]; // xyz the world position of up to four marked lights, w their scatter scale (0 = none)
        float4 _VolCounts;      // x the number of additional lights this frame
        float4 _VolParams;    // x density, y max distance (m), z steps, w anisotropy g
        float4 _VolNoise;     // x scale, y amount, zw unused
        float4 _VolWind;      // xyz drift (m/s), w time
        float4 _VolTexel;     // xy the texel size of the half-size target

        float Hash13(float3 p)
        {
            p = frac(p * 0.1031);
            p += dot(p, p.zyx + 31.32);
            return frac((p.x + p.y) * p.z);
        }

        float ValueNoise(float3 p)
        {
            float3 i = floor(p);
            float3 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float n000 = Hash13(i), n100 = Hash13(i + float3(1, 0, 0)), n010 = Hash13(i + float3(0, 1, 0)), n110 = Hash13(i + float3(1, 1, 0));
            float n001 = Hash13(i + float3(0, 0, 1)), n101 = Hash13(i + float3(1, 0, 1)), n011 = Hash13(i + float3(0, 1, 1)), n111 = Hash13(i + float3(1, 1, 1));
            float a = lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y);
            float b = lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y);
            return lerp(a, b, f.z);
        }

        float Phase(float cosTheta, float g)
        {
            float g2 = g * g;
            return (1.0 - g2) / (12.566 * pow(max(1e-4, 1.0 + g2 - 2.0 * g * cosTheta), 1.5));
        }

        float3 Scatter(int index, float strength, float3 p, float3 rd)
        {
            if (index < 0) return 0;
            Light light = GetAdditionalPerObjectLight(index, p);
            half shadow = AdditionalLightRealtimeShadow(index, p, light.direction);
            float cosTheta = dot(light.direction, rd); // 1 when the eye looks into the light: the forward peak
            return light.color * (light.distanceAttenuation * shadow * Phase(cosTheta, _VolParams.w) * strength);
        }

        // The index of a marked light in URP's additional-light arrays, found by its position (no guess about the order).
        int FindLight(float3 position)
        {
            int count = (int)_VolCounts.x;
            for (int j = 0; j < count; j++)
            {
                float3 d = _AdditionalLightsPosition[j].xyz - position;
                if (dot(d, d) < 1e-4) return j;
            }
            return -1;
        }

        half4 FragMarch(Varyings input) : SV_Target
        {
            int i0 = _VolLightPos[0].w > 0 ? FindLight(_VolLightPos[0].xyz) : -1;
            int i1 = _VolLightPos[1].w > 0 ? FindLight(_VolLightPos[1].xyz) : -1;
            int i2 = _VolLightPos[2].w > 0 ? FindLight(_VolLightPos[2].xyz) : -1;
            int i3 = _VolLightPos[3].w > 0 ? FindLight(_VolLightPos[3].xyz) : -1;
            float2 uv = input.texcoord;
            #if UNITY_REVERSED_Z
                float depth = SampleSceneDepth(uv);
            #else
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
            #endif
            float3 positionWS = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);
            float3 ro = _WorldSpaceCameraPos;
            float3 toPixel = positionWS - ro;
            float dist = min(length(toPixel), _VolParams.y);
            float3 rd = toPixel / max(1e-4, length(toPixel));
            if (_VolCounts.y > 0.5 && _VolCounts.y < 1.5) return half4(dist / 10.0, i0 >= 0 ? 1 : 0, 0, 1); // check: the march length, the light found
            if (_VolCounts.y > 2.5 && i0 >= 0)
            {
                Light l3 = GetAdditionalPerObjectLight(i0, positionWS);
                if (_VolCounts.y < 3.5) return half4(l3.color * l3.distanceAttenuation * 0.1, 1); // check 3: no shadow
                return AdditionalLightRealtimeShadow(i0, positionWS, l3.direction).xxxx;          // check 4: the shadow only
            }
            if (_VolCounts.y > 1.5 && i0 >= 0)
            {
                // check: the light and its shadow at the surface of the pixel, no march
                Light l = GetAdditionalPerObjectLight(i0, positionWS);
                return half4(l.color * l.distanceAttenuation * 0.1, 1) * AdditionalLightRealtimeShadow(i0, positionWS, l.direction);
            }
            int steps = (int)_VolParams.z;
            float stepLength = dist / steps;
            // Interleaved gradient noise: a different start per pixel, the blur hides the pattern.
            float2 pixel = uv * _ScreenParams.xy;
            float jitter = frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            float t = stepLength * jitter;
            float3 sum = 0;
            [loop]
            for (int i = 0; i < steps; i++)
            {
                float3 p = ro + rd * t;
                float n = ValueNoise(p * _VolNoise.x + _VolWind.xyz * _VolWind.w) * 0.6 + ValueNoise(p * _VolNoise.x * 3.7 - _VolWind.xyz * _VolWind.w * 1.7) * 0.4;
                float density = _VolParams.x * (1.0 - _VolNoise.y + _VolNoise.y * 2.0 * n);
                float3 s = Scatter(i0, _VolLightPos[0].w, p, rd)
                         + Scatter(i1, _VolLightPos[1].w, p, rd)
                         + Scatter(i2, _VolLightPos[2].w, p, rd)
                         + Scatter(i3, _VolLightPos[3].w, p, rd);
                sum += s * density * stepLength;
                t += stepLength;
            }
            return half4(sum, 1);
        }

        half4 Blur(float2 uv, float2 d)
        {
            half3 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb * 0.227027;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d * 1.3846).rgb * 0.316216;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - d * 1.3846).rgb * 0.316216;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + d * 3.2308).rgb * 0.070270;
            c += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - d * 3.2308).rgb * 0.070270;
            return half4(c, 1);
        }

        half4 FragBlurH(Varyings input) : SV_Target { return Blur(input.texcoord, float2(_VolTexel.x, 0)); }
        half4 FragBlurV(Varyings input) : SV_Target { return Blur(input.texcoord, float2(0, _VolTexel.y)); }

        half4 FragAdd(Varyings input) : SV_Target
        {
            return half4(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord).rgb, 0);
        }
        ENDHLSL

        Pass
        {
            Name "March"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragMarch
            #pragma target 4.5
            ENDHLSL
        }
        Pass
        {
            Name "BlurH"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurH
            ENDHLSL
        }
        Pass
        {
            Name "BlurV"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragBlurV
            ENDHLSL
        }
        Pass
        {
            Name "Add"
            Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragAdd
            ENDHLSL
        }
    }
}
