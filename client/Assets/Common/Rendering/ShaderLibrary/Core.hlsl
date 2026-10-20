#ifndef GAME_CORE_INCLUDED
#define GAME_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

// Встроенные переменные движка. Раскладка UnityPerDraw повторяет URP: SRP Batcher
// сверяет её по смещениям, и спрайты ждут unity_SpriteColor / unity_SpriteProps в конце.
CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;

    float4 unity_RenderingLayer;

    float4 unity_PackedLightIndices;
    half4 unity_LightData;

    float4 unity_ProbesOcclusion;

    real4 unity_SpecCube0_HDR;
    real4 unity_SpecCube1_HDR;

    float4 unity_SpecCube0_BoxMax;
    float4 unity_SpecCube0_BoxMin;
    float4 unity_SpecCube0_ProbePosition;
    float4 unity_SpecCube0_Rotation;
    float4 unity_SpecCube0_Exposure;
    float4 unity_SpecCube1_BoxMax;
    float4 unity_SpecCube1_BoxMin;
    float4 unity_SpecCube1_ProbePosition;
    float4 unity_SpecCube1_Rotation;
    float4 unity_SpecCube1_Exposure;

    float4 unity_LightmapST;
    float4 unity_DynamicLightmapST;

    real4 unity_SHAr;
    real4 unity_SHAg;
    real4 unity_SHAb;
    real4 unity_SHBr;
    real4 unity_SHBg;
    real4 unity_SHBb;
    real4 unity_SHC;

    float4 unity_RendererBounds_Min;
    float4 unity_RendererBounds_Max;

    float4x4 unity_MatrixPreviousM;
    float4x4 unity_MatrixPreviousMI;
    float4 unity_MotionVectorsParams;

    // x, y - flip, z - смещение костей скиннинга, w - смещение unified-деформации.
    float4 unity_SpriteColor;
    float4 unity_SpriteProps;
CBUFFER_END

float4 _Time;
float4 _SinTime;
float4 _CosTime;
float4 unity_DeltaTime;

float3 _WorldSpaceCameraPos;
float4 _ProjectionParams;
float4 _ScreenParams;
float4 _ZBufferParams;
float4 unity_OrthoParams;

float4x4 glstate_matrix_projection;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_MatrixInvP;
float4x4 unity_MatrixVP;
float4x4 unity_MatrixInvVP;

#define UNITY_MATRIX_M     unity_ObjectToWorld
#define UNITY_MATRIX_I_M   unity_WorldToObject
#define UNITY_MATRIX_V     unity_MatrixV
#define UNITY_MATRIX_I_V   unity_MatrixInvV
#define UNITY_MATRIX_P     glstate_matrix_projection
#define UNITY_MATRIX_I_P   unity_MatrixInvP
#define UNITY_MATRIX_VP    unity_MatrixVP
#define UNITY_MATRIX_I_VP  unity_MatrixInvVP
#define UNITY_MATRIX_MV    mul(UNITY_MATRIX_V, UNITY_MATRIX_M)
#define UNITY_MATRIX_T_MV  transpose(UNITY_MATRIX_MV)
#define UNITY_MATRIX_IT_MV transpose(mul(UNITY_MATRIX_I_M, UNITY_MATRIX_I_V))
#define UNITY_MATRIX_MVP   mul(UNITY_MATRIX_VP, UNITY_MATRIX_M)
#define UNITY_PREV_MATRIX_M   unity_MatrixPreviousM
#define UNITY_PREV_MATRIX_I_M unity_MatrixPreviousMI

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

// Спрайты. При инстансинге цвет и flip SpriteRenderer приходят через инстансные буферы.
#ifdef UNITY_INSTANCING_ENABLED
    UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
        UNITY_DEFINE_INSTANCED_PROP(float4, unity_SpritePropArray)
        UNITY_DEFINE_INSTANCED_PROP(float4, unity_SpriteRendererColorArray)
        UNITY_DEFINE_INSTANCED_PROP(float, unity_RendererUserValueArray)
    UNITY_INSTANCING_BUFFER_END(PerDrawSprite)

    #define unity_SpriteProp  UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpritePropArray)
    #define unity_SpriteColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
#endif

// С Unity 7000 flip лежит битами в unity_SpriteProps.x: бит 0 - X, бит 1 - Y.
float3 FlipSprite(float3 positionOS)
{
#ifdef UNITY_INSTANCING_ENABLED
    uint bits = asuint(unity_SpriteProp.x);
#else
    uint bits = asuint(unity_SpriteProps.x);
#endif
    float2 flip = float2(bits & 1u ? -1.0 : 1.0, bits & 2u ? -1.0 : 1.0);
    return float3(positionOS.xy * flip, positionOS.z);
}

#endif
