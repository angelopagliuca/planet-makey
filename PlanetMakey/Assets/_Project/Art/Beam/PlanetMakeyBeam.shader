// The tractor beam: a glowing cone of light. It is drawn on Unity's cylinder, which this shader flares
// out at the bottom (narrow at the ship, a wide skirt at the ground), with soft edges, streaks and rings
// that drift upward, brighter near the ship and in a ring where it meets the ground. It is added to the
// picture, so it looks like light, and the intensity is brighter than white so Bloom makes it glow.
// BeamView sets the per-frame values (_Intensity, _Fade, _Urgency, _Phase, _Length, the flare) through a
// property block; the look is tuned on the material.
Shader "PlanetMakey/Beam"
{
    Properties
    {
        _BaseColor ("Color", Color) = (0.71, 0.95, 0.23, 1)
        _Intensity ("Intensity", Range(0, 12)) = 3
        _Fade ("Fade", Range(0, 1)) = 1

        [Header(Shape set by BeamView)]
        _TopFlare ("Width at the ship", Range(0.05, 4)) = 0.8
        _BottomFlare ("Width at the ground", Range(0.5, 12)) = 5
        _FlarePower ("Flare power", Range(0.5, 6)) = 2.2
        _Length ("Length in world units", Range(0.1, 40)) = 5
        _Phase ("Rise phase", Float) = 0
        _Urgency ("Urgency", Range(0, 1)) = 0

        [Header(Look)]
        _CoreAlpha ("Core opacity", Range(0, 1)) = 0.35
        _EdgeSoftness ("Edge softness", Range(0.2, 6)) = 1.6
        _StreakCount ("Streaks around", Range(2, 60)) = 18
        _StreakStrength ("Streak strength", Range(0, 2)) = 0.8
        _RingFrequency ("Rings per world unit", Range(0.1, 6)) = 1.1
        _RingStrength ("Ring strength", Range(0, 2)) = 0.7
        _RingSharpness ("Ring sharpness", Range(1, 16)) = 5
        _TopGlow ("Brightness at the ship", Range(0, 4)) = 1.6
        _BottomGlow ("Brightness at the ground", Range(0, 4)) = 0.5
        _GroundRing ("Ring where it lands", Range(0, 4)) = 1.8
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
                half _TopFlare;
                half _BottomFlare;
                half _FlarePower;
                half _Length;
                float _Phase;
                half _Urgency;
                half _CoreAlpha;
                half _EdgeSoftness;
                half _StreakCount;
                half _StreakStrength;
                half _RingFrequency;
                half _RingStrength;
                half _RingSharpness;
                half _TopGlow;
                half _BottomGlow;
                half _GroundRing;
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
                float2 radial : TEXCOORD2;   // the direction around the beam (the cylinder's x and z)
                float along : TEXCOORD3;     // 0 at the ship to 1 at the ground
                float capMask : TEXCOORD4;   // 1 on the flat end caps of the cylinder
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Unity's cylinder is 2 units tall with +Y towards the ship: 0 at the ship, 1 at the ground.
                float along = saturate(1.0 - (input.positionOS.y * 0.5 + 0.5));

                // Flare: narrow at the ship, a skirt at the ground.
                float flare = lerp(_TopFlare, _BottomFlare, pow(along, _FlarePower));
                float3 flared = input.positionOS.xyz;
                flared.xz *= flare;

                output.positionWS = TransformObjectToWorld(flared);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.radial = input.positionOS.xz;
                output.along = along;
                output.capMask = step(0.5, abs(input.normalOS.y));
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Smooth random noise from 0 to 1 that repeats every "period" cells along x, so the streaks
            // meet without a seam where the angle wraps around the beam.
            float ValueNoise(float2 p, float period)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                float x0 = cell.x - period * floor(cell.x / period);
                float x1 = (x0 + 1.0 >= period) ? 0.0 : x0 + 1.0;
                float a = Hash21(float2(x0, cell.y));
                float b = Hash21(float2(x1, cell.y));
                float c = Hash21(float2(x0, cell.y + 1.0));
                float d = Hash21(float2(x1, cell.y + 1.0));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // The flat ends are not part of the beam.
                clip(0.5 - input.capMask);

                float angle = atan2(input.radial.y, input.radial.x);
                float along = input.along;
                float distanceFromShip = along * _Length;

                // Soft edges: the beam is most solid where the wall faces the viewer and melts away at its silhouette.
                float3 normal = normalize(input.normalWS);
                float3 viewDirection = normalize(GetWorldSpaceViewDir(input.positionWS));
                float facing = abs(dot(normal, viewDirection));
                float edge = pow(saturate(facing), _EdgeSoftness);

                // Streaks of light that run up the beam and drift as it pulls.
                float streakPeriod = max(1.0, round(_StreakCount));
                float streakCoordinate = (angle / 6.2831853 + 0.5) * streakPeriod;
                float streaks = ValueNoise(float2(streakCoordinate, distanceFromShip * 0.6 - _Phase * 0.7), streakPeriod);
                streaks = smoothstep(0.35, 1.0, streaks);

                // Rings that travel up the beam.
                float ringWave = 0.5 + 0.5 * sin((distanceFromShip - _Phase) * _RingFrequency * 6.2831853);
                float rings = pow(saturate(ringWave), _RingSharpness);

                // Brighter at the ship, dimmer further down, and a bright ring where it lands.
                float vertical = lerp(_TopGlow, _BottomGlow, along);
                float landing = smoothstep(0.86, 0.99, along) * _GroundRing;

                float brightness = _CoreAlpha + streaks * _StreakStrength + rings * _RingStrength;
                brightness = brightness * vertical + landing;

                // A late, urgent lift burns whiter.
                float3 color = _BaseColor.rgb * _Intensity;
                color = lerp(color, color + float3(0.6, 0.6, 0.6) * _Intensity * 0.5, _Urgency);

                float alpha = saturate(brightness * edge) * _Fade;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
