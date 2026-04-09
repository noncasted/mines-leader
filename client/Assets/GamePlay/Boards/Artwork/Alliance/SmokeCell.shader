Shader "Custom/SmokeCell" {
    Properties {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        _ColorDark ("Smoke Dark", Color) = (0.3, 0.3, 0.33, 1.0)
        _ColorLight ("Smoke Light", Color) = (0.55, 0.55, 0.58, 1.0)
        _NoiseScale ("Noise Scale", Float) = 1.5
        _Speed ("Speed", Float) = 1.0
        _EdgeNoiseScale ("Edge Noise Scale", Float) = 8.0
        _EdgeNoiseStrength ("Edge Noise Strength", Range(0, 1.0)) = 0.4
        _CoreRadius ("Core Radius", Range(0.1, 1.0)) = 0.25
        _PixelSize ("Pixel Size", Float) = 12.0
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorDark;
                half4 _ColorLight;
                float _NoiseScale;
                float _Speed;
                float _EdgeNoiseScale;
                float _EdgeNoiseStrength;
                float _CoreRadius;
                float _PixelSize;
                float4 _MainTex_ST;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
                float4 color : COLOR;
            };

            float2 hash2(float2 p) {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float gnoise(float2 p) {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = dot(hash2(i) - 0.5, f);
                float b = dot(hash2(i + float2(1, 0)) - 0.5, f - float2(1, 0));
                float c = dot(hash2(i + float2(0, 1)) - 0.5, f - float2(0, 1));
                float d = dot(hash2(i + float2(1, 1)) - 0.5, f - float2(1, 1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) + 0.5;
            }

            // Multi-octave noise for soft smoke texture
            float smokeNoise(float2 p, float t) {
                // Organic wandering - faster and wider
                float2 drift1 = float2(sin(t * 0.7) * 0.8, cos(t * 0.5) * 0.6);
                float2 drift2 = float2(cos(t * 0.4) * 1.0, sin(t * 0.6) * 0.8);
                float2 drift3 = float2(sin(t * 0.9) * 0.6, cos(t * 0.8) * 1.0);

                float n = 0.0;
                n += gnoise(p * 1.0 + drift1) * 0.5;
                n += gnoise(p * 2.0 + drift2) * 0.3;
                n += gnoise(p * 4.0 + drift3) * 0.2;
                return n;
            }

            Varyings vert(Attributes input) {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                float3 wp = TransformObjectToWorld(input.positionOS.xyz);
                output.worldPos = wp.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                // Pixelate world position - snap to pixel center
                float2 wp = (floor(input.worldPos * _PixelSize) + 0.5) / _PixelSize;
                float t = _Time.y * _Speed;

                // Soft multi-octave noise for smoke texture
                float n1 = smokeNoise(wp * _NoiseScale, t);
                float n2 = smokeNoise(wp * _NoiseScale * 1.7 + float2(5.2, 1.3), t * 1.3);

                // Soft blend between colors
                float blend = smoothstep(0.3, 0.7, n1 * 0.6 + n2 * 0.4);
                half3 color = lerp(_ColorDark.rgb, _ColorLight.rgb, blend);

                // Snap UV to same pixel grid as world pos
                float2 pixUV = (floor(input.uv * _PixelSize) + 0.5) / _PixelSize;
                float2 centered = pixUV - 0.5;
                float edgeDist = max(abs(centered.x), abs(centered.y)) * 2.0;

                // Traveling wave along edge using angle
                float waveT = _Time.y * _Speed;
                float2 dir = pixUV - 0.5;
                float angle = atan2(dir.y, dir.x);

                // Phase offset from world position — each cell gets unique wave pattern
                float phase = dot(floor(input.worldPos), float2(127.1, 311.7));
                phase = frac(sin(phase) * 43758.5453) * 6.2831;

                // Low-frequency waves = wide lobes, randomized per cell
                float wave = sin(angle * 1.5 + waveT * 1.2 + phase) * 0.5
                           + sin(angle * 2.0 - waveT * 0.8 + phase * 1.7) * 0.3
                           + sin(angle * 3.0 + waveT * 1.5 + phase * 0.6) * 0.2;
                wave *= _EdgeNoiseStrength;

                // Small grain scatter
                float grainNoise = gnoise(wp * _EdgeNoiseScale + float2(sin(waveT * 1.1), cos(waveT * 0.7)));
                float grainRange = 0.8 / _PixelSize;

                // Wave radius, but NEVER less than core
                float waveRadius = _CoreRadius + wave + (grainNoise - 0.5) * grainRange;
                float edgeThreshold = max(_CoreRadius, waveRadius);
                float alpha = step(edgeDist, edgeThreshold);

                alpha *= input.color.a;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
