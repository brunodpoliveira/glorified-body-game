// The frame recorder (bot clips only): puts the HUD over the scene after the post-processing, as the game's Screen
// Space Overlay canvas does. _MainTex is the scene, _HudBlack the HUD rendered on black, _HudWhite the same HUD on
// white. Per pixel: out = hud_on_black + scene * (hud_on_white - hud_on_black). The difference of the two HUD frames is
// the part of the scene that shows through the HUD (1 where there is no HUD, 0 under an opaque HUD pixel).
// Used through Graphics.Blit by FrameRecorder.SaveWithHud. Not used by the game.
Shader "Hidden/GB/HudComposite"
{
    Properties
    {
        _MainTex ("Scene", 2D) = "black" {}
        _HudBlack ("HUD on black", 2D) = "black" {}
        _HudWhite ("HUD on white", 2D) = "white" {}
    }
    SubShader
    {
        ZTest Always Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _HudBlack;
            sampler2D _HudWhite;

            float4 frag(v2f_img i) : SV_Target
            {
                float3 scene = tex2D(_MainTex, i.uv).rgb;
                float3 onBlack = tex2D(_HudBlack, i.uv).rgb;
                float3 onWhite = tex2D(_HudWhite, i.uv).rgb;
                float3 through = saturate(onWhite - onBlack);
                return float4(saturate(onBlack + scene * through), 1.0);
            }
            ENDCG
        }
    }
}
