// An unlit disc that fades towards its rim, with no texture.
//
// Used for the ring's ground shadow, the glow pooled under a peg, and the halo
// around a light. All three need the same thing: a soft round falloff that reads
// correctly on a flat-shaded scene and costs one small transparent draw.
Shader "SlipperyZlope/Soft Disc"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0, 0, 0, 0.4)
        _Softness ("Softness", Range(0.01, 1)) = 0.55
        _Fill ("Fill", Range(0, 1)) = 0.35
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _Softness)
                UNITY_DEFINE_INSTANCED_PROP(float, _Fill)
            UNITY_INSTANCING_BUFFER_END(Props)

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 color = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);
                float softness = UNITY_ACCESS_INSTANCED_PROP(Props, _Softness);
                float fill = UNITY_ACCESS_INSTANCED_PROP(Props, _Fill);

                // The disc mesh's UVs put the centre at 0.5, 0.5 and the rim at radius 0.5.
                float distance = length(input.uv - 0.5) * 2.0;

                float inner = saturate(fill);
                float alpha = 1.0 - smoothstep(inner, inner + max(softness, 0.001), distance);

                return half4(color.rgb, color.a * alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
