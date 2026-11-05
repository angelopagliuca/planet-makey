// A gradient sky that changes from day to night: zenith, horizon and ground colors, a warm glow at the
// horizon for dusk and dawn, procedural stars, a moon and a sun disc. No textures. DayNightView sets
// _Night (0 day, 1 night), _Warmth (the glow), _SunDirection and _MoonDirection on a copy of the
// material; the colors below are set on the material itself.
Shader "PlanetMakey/Gradient Sky"
{
    Properties
    {
        [Header(Day)]
        _DayZenith ("Day zenith", Color) = (0.20, 0.45, 0.85, 1)
        _DayHorizon ("Day horizon", Color) = (0.75, 0.88, 0.98, 1)
        _DayGround ("Day ground", Color) = (0.35, 0.38, 0.30, 1)

        [Header(Night)]
        _NightZenith ("Night zenith", Color) = (0.04, 0.03, 0.14, 1)
        _NightHorizon ("Night horizon", Color) = (0.27, 0.11, 0.27, 1)
        _NightGround ("Night ground", Color) = (0.05, 0.04, 0.10, 1)

        [Header(Dusk and dawn)]
        _WarmColor ("Glow color", Color) = (1.0, 0.55, 0.30, 1)
        _WarmStrength ("Glow strength", Range(0, 1)) = 0.85
        _WarmHeight ("Glow height", Range(0.05, 1)) = 0.35

        [Header(Shape)]
        _HorizonCurve ("Horizon curve", Range(0.1, 2)) = 0.6

        [Header(Stars)]
        _StarScale ("Star scale", Range(20, 300)) = 70
        _StarThreshold ("Star threshold", Range(0.9, 0.9999)) = 0.92
        _StarBrightness ("Star brightness", Range(0, 3)) = 1.5

        [Header(Moon and sun)]
        _MoonColor ("Moon color", Color) = (0.95, 0.95, 0.85, 1)
        _MoonSize ("Moon size (degrees)", Range(2, 10)) = 2.5
        _SunColor ("Sun color", Color) = (1.0, 0.95, 0.80, 1)
        _SunSize ("Sun size (degrees)", Range(2, 10)) = 2.8

        [Header(Set by DayNightView)]
        _Night ("Night amount", Range(0, 1)) = 0
        _Warmth ("Glow amount", Range(0, 1)) = 0
        _SunDirection ("Sun direction", Vector) = (0, 1, 0, 0)
        _MoonDirection ("Moon direction", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DayZenith;
                half4 _DayHorizon;
                half4 _DayGround;
                half4 _NightZenith;
                half4 _NightHorizon;
                half4 _NightGround;
                half4 _WarmColor;
                half _WarmStrength;
                half _WarmHeight;
                half _HorizonCurve;
                half _StarScale;
                half _StarThreshold;
                half _StarBrightness;
                half4 _MoonColor;
                half _MoonSize;
                half4 _SunColor;
                half _SunSize;
                half _Night;
                half _Warmth;
                float4 _SunDirection;
                float4 _MoonDirection;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            // A pseudo-random number from 0 to 1 for a point in space.
            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            // Little round stars: the sky is cut into cells, a few cells hold a star at a random spot,
            // and each star twinkles at its own pace.
            float Stars(float3 direction)
            {
                float3 scaled = direction * _StarScale;
                float3 cell = floor(scaled);
                float3 local = frac(scaled) - 0.5;

                float chance = Hash13(cell);
                float3 jitter = (float3(Hash13(cell + 17.1), Hash13(cell + 31.7), Hash13(cell + 47.3)) - 0.5) * 0.6;
                float distanceToStar = length(local - jitter);
                float size = lerp(0.10, 0.22, Hash13(cell + 5.5));
                float twinkle = 0.7 + 0.3 * sin(_Time.y * (1.0 + chance * 3.0) + chance * 40.0);

                float present = step(_StarThreshold, chance);
                return present * (1.0 - smoothstep(0.0, size, distanceToStar)) * twinkle;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float up = direction.y;
                float above = saturate(up);

                // Zenith to horizon, from day to night.
                float3 zenith = lerp(_DayZenith.rgb, _NightZenith.rgb, _Night);
                float3 horizon = lerp(_DayHorizon.rgb, _NightHorizon.rgb, _Night);
                float3 sky = lerp(horizon, zenith, pow(above, _HorizonCurve));

                // The glow of dusk and dawn, low in the sky.
                float band = 1.0 - smoothstep(0.0, _WarmHeight, above);
                sky = lerp(sky, _WarmColor.rgb, saturate(_Warmth * _WarmStrength * band));

                // Just below the horizon it turns to ground color, in case the terrain does not cover it.
                float3 ground = lerp(_DayGround.rgb, _NightGround.rgb, _Night);
                sky = lerp(sky, ground, smoothstep(0.0, 0.08, -up));

                // Stars come out with the night, and only in the sky.
                float starFade = _Night * smoothstep(0.0, 0.25, up);
                sky += Stars(direction) * _StarBrightness * starFade;

                // The moon: a disc with a faint halo, only at night and above the horizon.
                float moonAlignment = dot(direction, normalize(_MoonDirection.xyz));
                float moonCos = cos(radians(_MoonSize));
                float moonDisc = smoothstep(moonCos, moonCos + 0.0006, moonAlignment);
                float moonHalo = pow(saturate(moonAlignment), 220.0) * 0.35;
                float moonFade = _Night * smoothstep(-0.02, 0.05, up);
                sky += _MoonColor.rgb * (moonDisc + moonHalo) * moonFade;

                // The sun: the same, in the day.
                float sunAlignment = dot(direction, normalize(_SunDirection.xyz));
                float sunCos = cos(radians(_SunSize));
                float sunDisc = smoothstep(sunCos, sunCos + 0.0006, sunAlignment);
                float sunHalo = pow(saturate(sunAlignment), 160.0) * 0.4;
                float sunFade = (1.0 - _Night) * smoothstep(-0.02, 0.05, up);
                sky += _SunColor.rgb * (sunDisc + sunHalo) * sunFade;

                return half4(sky, 1.0);
            }
            ENDHLSL
        }
    }
}
