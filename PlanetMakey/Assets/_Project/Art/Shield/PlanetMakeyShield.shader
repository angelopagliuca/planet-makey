// Makey's shield: a see-through bubble with a bright rim that glows (the rim is brighter than 1, so
// Bloom picks it up) and faint bands of light that drift over it. MakeyView sets _BaseColor (the
// ball's color, with its alpha) and _Glow (how hard it glows) through a property block; everything
// else is tuned on the material.
Shader "PlanetMakey/Shield"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 0.4)
        _Glow ("Glow", Range(0, 8)) = 2.5

        [Header(Fill and rim)]
        _FillAlpha ("Fill opacity", Range(0, 2)) = 0.5
        _RimAlpha ("Rim opacity", Range(0, 4)) = 2.0
        _RimPower ("Rim width (higher is thinner)", Range(0.5, 8)) = 3.0
        _FillBrightness ("Fill brightness", Range(0, 2)) = 0.7
        _RimBrightness ("Rim brightness", Range(0, 4)) = 1.0

        [Header(Bands of light)]
        _BandStrength ("Band strength", Range(0, 1)) = 0.25
        _BandScale ("Band scale", Range(0.5, 30)) = 6.0
        _BandSpeed ("Band speed", Range(-3, 3)) = 0.6
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Glow;
                half _FillAlpha;
                half _RimAlpha;
                half _RimPower;
                half _FillBrightness;
                half _RimBrightness;
                half _BandStrength;
                half _BandScale;
                half _BandSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));

                // Bright where the surface turns away from the viewer: the edge of the bubble.
                float rim = pow(1.0 - saturate(dot(normal, viewDirection)), _RimPower);

                // Bands of light that drift upwards over the bubble.
                float bands = 0.5 + 0.5 * sin(input.positionWS.y * _BandScale - _Time.y * _BandSpeed * 6.2831853);
                float shimmer = 1.0 + (bands - 0.5) * 2.0 * _BandStrength;

                float brightness = (_FillBrightness + rim * _RimBrightness) * shimmer;
                float3 color = _BaseColor.rgb * _Glow * brightness;
                float alpha = saturate(_BaseColor.a * (_FillAlpha + rim * _RimAlpha));

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
