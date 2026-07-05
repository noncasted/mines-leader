Shader "Custom/UI/TiledBlurBackground"
{
    Properties
    {
        [MainTexture] _MainTex ("Tile Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.15, 0.12, 0.10, 1.0)
        _TileScale ("Tile Scale", Float) = 4.0
        _NoiseScale ("Noise Scale", Float) = 3.0
        _NoiseStrength ("UV Warp Strength", Range(0, 0.05)) = 0.015
        _ColorNoise ("Color Noise Strength", Range(0, 0.2)) = 0.05
        _PixelSize ("Pixel Snap Size", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _TileScale;
                float _NoiseScale;
                float _NoiseStrength;
                float _ColorNoise;
                float _PixelSize;
                float4 _MainTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
                float4 color : COLOR;
            };

            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float gnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = dot(hash2(i) - 0.5, f);
                float b = dot(hash2(i + float2(1, 0)) - 0.5, f - float2(1, 0));
                float c = dot(hash2(i + float2(0, 1)) - 0.5, f - float2(0, 1));
                float d = dot(hash2(i + float2(1, 1)) - 0.5, f - float2(1, 1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) + 0.5;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                float3 wp = TransformObjectToWorld(input.positionOS.xyz);
                output.worldPos = wp.xy;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 wp = input.worldPos;

                // Optional pixel snap (0 = off)
                if (_PixelSize > 0)
                    wp = (floor(wp * _PixelSize) + 0.5) / _PixelSize;

                float2 tiledUV = wp * _TileScale;

                // Procedural UV warp — 3 octaves for organic softness
                float2 warp = 0;
                warp += (gnoise(wp * _NoiseScale) - 0.5) * _NoiseStrength;
                warp += (gnoise(wp * _NoiseScale * 2.3 + float2(5.1, 3.7)) - 0.5) * _NoiseStrength * 0.5;
                warp += (gnoise(wp * _NoiseScale * 4.7 + float2(1.3, 8.9)) - 0.5) * _NoiseStrength * 0.25;

                // Multi-sample "blur" — 3 taps with warped UVs
                float2 uv1 = tiledUV + warp;
                float2 uv2 = tiledUV + warp * 0.7 + float2(0.03, -0.02);
                float2 uv3 = tiledUV + warp * 1.3 + float2(-0.02, 0.03);

                half4 c1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv1);
                half4 c2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv2);
                half4 c3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv3);

                half4 texColor = (c1 + c2 + c3) / 3.0;

                // Subtle color noise to break uniformity
                float cn = gnoise(wp * 8.0 + _Time.y * 0.1);
                texColor.rgb += (cn - 0.5) * _ColorNoise;

                half4 final = texColor * _Color * input.color;
                return saturate(final);
            }
            ENDHLSL
        }
    }
}