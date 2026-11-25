// A three-stop vertical gradient sky with a soft sun disc.
//
// Unity's default skybox drew a brown horizon band past the edge of the ground,
// which reads as a rendering artefact. Rather than clearing to a flat colour and
// losing all depth, this gives the scene a real sky whose horizon can be matched
// to the ground, in one draw call and with no texture.
Shader "SlipperyZlope/Sky Gradient"
{
    Properties
    {
        [HDR] _TopColor ("Top Color", Color) = (0.09, 0.08, 0.22, 1)
        [HDR] _MiddleColor ("Middle Color", Color) = (0.46, 0.20, 0.42, 1)
        [HDR] _HorizonColor ("Horizon Color", Color) = (0.98, 0.52, 0.36, 1)
        _HorizonHeight ("Horizon Height", Range(0, 1)) = 0.34
        _GroundColor ("Below Horizon Color", Color) = (0.14, 0.10, 0.20, 1)
        [HDR] _SunColor ("Sun Color", Color) = (1, 0.86, 0.55, 1)
        _SunDirection ("Sun Direction", Vector) = (0.35, 0.30, 1, 0)
        _SunSize ("Sun Size", Range(0.001, 0.5)) = 0.06
        _SunHalo ("Sun Halo", Range(0, 1)) = 0.18
        _SkyCurve ("Sky Curve", Range(0.1, 2)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 directionWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _MiddleColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half4 _SunColor;
                float4 _SunDirection;
                float _HorizonHeight;
                float _SunSize;
                float _SunHalo;
                float _SkyCurve;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.directionWS = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.directionWS);

                // Height runs 0 at the horizon to 1 straight up, then is pushed through
                // a curve. In portrait the camera only sees about the first ten degrees
                // above the horizon, so a linear gradient would spend almost its whole
                // range off the top of the screen and the sky would read as one flat
                // wash. The curve pulls the interesting part down into the visible band.
                float height = pow(saturate(direction.y), max(_SkyCurve, 0.01));

                float horizon = max(_HorizonHeight, 0.001);
                half3 lower = lerp(_HorizonColor.rgb, _MiddleColor.rgb, saturate(height / horizon));
                half3 upper = lerp(_MiddleColor.rgb, _TopColor.rgb,
                                   saturate((height - horizon) / max(1.0 - horizon, 0.001)));
                half3 sky = direction.y < horizon ? lower : upper;

                // Below the true horizon the sky darkens into the ground colour, so the
                // edge of the playfield never shows a hard seam.
                half3 below = lerp(_GroundColor.rgb, _HorizonColor.rgb,
                                   saturate(direction.y * 6.0 + 1.0));
                sky = direction.y < 0.0 ? below : sky;

                float3 sunDirection = normalize(_SunDirection.xyz);
                float towardsSun = saturate(dot(direction, sunDirection));

                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.35, towardsSun);
                float halo = pow(towardsSun, 24.0) * _SunHalo;

                sky += _SunColor.rgb * (disc + halo);

                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
