Shader "Custom/Sprites/PixelOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 1)
        [IntRange] _OutlinePixels ("Outline Pixels", Range(1, 3)) = 1
        _AlphaClip ("Alpha Clip", Range(0.01, 1)) = 0.1
        _PixelsPerUnit ("Pixels Per Unit", Float) = 12
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlinePixels;
                float _AlphaClip;
                float _PixelsPerUnit;
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            static const float2 kDirs[8] =
            {
                float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                float2(1, 1), float2(-1, 1), float2(1, -1), float2(-1, -1)
            };

            float SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            Varyings vert(Attributes input)
            {
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                float pixels = max(round(_OutlinePixels), 1);
                float ppu = max(_PixelsPerUnit, 1);
                float2 side = sign(input.positionOS.xy);
                input.positionOS.xy += side * (pixels / ppu);

                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv + side * pixels * _MainTex_TexelSize.xy;
                output.color = input.color * _Color * unity_SpriteColor * _OutlineColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float clipThreshold = _AlphaClip;
                if (SampleAlpha(uv) > clipThreshold)
                    return 0;

                int width = (int)max(round(_OutlinePixels), 1);
                float neighbor = 0;
                [unroll]
                for (int i = 1; i <= 3; i++)
                {
                    float use = i <= width ? 1 : 0;
                    [unroll]
                    for (int d = 0; d < 8; d++)
                    {
                        float2 sampleUv = uv + kDirs[d] * i * _MainTex_TexelSize.xy;
                        neighbor = max(neighbor, SampleAlpha(sampleUv) * use);
                    }
                }

                if (neighbor <= clipThreshold)
                    return 0;

                return input.color;
            }
            ENDHLSL
        }
    }
}
