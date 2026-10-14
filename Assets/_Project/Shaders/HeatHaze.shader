// Bends the scene behind a soft disc, as air shimmers over a fire.
//
// Drawn by the heat rising off the corrupted knight in his later phases. It samples the opaque scene the pipeline
// copies after the opaque pass and offsets the lookup by a scrolling noise, so whatever stands behind him wavers.
// The copy is enabled only on the desktop High pipeline; the effect is never emitted anywhere else, because without
// the copy the lookup returns a flat grey.
Shader "AdaptiveBossArena/HeatHaze"
{
    Properties
    {
        _NoiseMap ("Noise (RG offsets)", 2D) = "gray" {}
        _Strength ("Strength (screen fraction)", Float) = 0.012
        _ScrollSpeed ("Rise speed", Float) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "HeatHaze"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseMap_ST;
                float _Strength;
                float _ScrollSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The particle's alpha carries its strength over its life; the disc fades it out toward its rim, so
                // no edge of the quad is ever visible as a seam in the bent image.
                float2 centred = input.uv * 2.0 - 1.0;
                half falloff = saturate(1.0 - dot(centred, centred));
                half weight = falloff * falloff * input.color.a;

                float2 noiseUv = TRANSFORM_TEX(input.uv, _NoiseMap) + float2(0.0, -_Time.y * _ScrollSpeed);
                float2 offset = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv).rg * 2.0 - 1.0;

                float2 screenUv = input.screenPos.xy / input.screenPos.w + offset * _Strength * weight;
                return half4(SampleSceneColor(screenUv), weight);
            }
            ENDHLSL
        }
    }
}
