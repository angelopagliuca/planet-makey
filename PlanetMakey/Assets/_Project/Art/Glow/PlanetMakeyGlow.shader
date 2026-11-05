// A soft glow of light, drawn on a flat Unity Quad that always faces the camera: a small bright core inside
// a wider, softer halo. The quad's size sets the size of the glow, so a quad wider than it is tall gives an
// oval glow, which suits a flat ship. It is added to the picture, so it looks like light, and the intensity
// is brighter than white so Bloom makes it glow. MakeyGlowView sets _BaseColor, _Intensity and _Fade through
// a property block; the look is tuned on the material.
Shader "PlanetMakey/Glow"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.71, 0.95, 0.23, 1)
        _Intensity ("Intensity", Range(0, 12)) = 2.5
        _Fade ("Fade", Range(0, 1)) = 1

        [Header(Look)]
        _CoreSize ("Core size (share of the quad)", Range(0.05, 1)) = 0.4
        _CorePower ("Core falloff", Range(0.5, 8)) = 2
        _CoreStrength ("Core strength", Range(0, 4)) = 1.2
        _HaloPower ("Halo falloff", Range(0.5, 8)) = 2.5
        _HaloStrength ("Halo strength", Range(0, 4)) = 0.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Intensity;
                half _Fade;
                half _CoreSize;
                half _CorePower;
                half _CoreStrength;
                half _HaloPower;
                half _HaloStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;   // -1 to 1 across the quad, 0 in the middle
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Always face the camera: the quad is laid out along the camera's right and up
                // directions around the object's position, at the object's width and height.
                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float3 centerWS = float3(objectToWorld._m03, objectToWorld._m13, objectToWorld._m23);
                float width = length(float3(objectToWorld._m00, objectToWorld._m10, objectToWorld._m20));
                float height = length(float3(objectToWorld._m01, objectToWorld._m11, objectToWorld._m21));
                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;

                // Unity's Quad runs from -0.5 to 0.5 on x and y.
                float3 positionWS = centerWS
                    + cameraRight * (input.positionOS.x * width)
                    + cameraUp * (input.positionOS.y * height);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.positionOS.xy * 2.0;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float distanceFromMiddle = length(input.uv);

                float core = pow(saturate(1.0 - distanceFromMiddle / max(0.01, _CoreSize)), _CorePower) * _CoreStrength;
                float halo = pow(saturate(1.0 - distanceFromMiddle), _HaloPower) * _HaloStrength;

                // The core burns a little whiter than the halo.
                float3 color = _BaseColor.rgb * _Intensity;
                color = lerp(color, float3(1.0, 1.0, 1.0) * _Intensity, saturate(core * 0.5));

                float alpha = saturate(core + halo) * _Fade;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
