#ifndef SCENETOON_TERRAIN_INPUT_INCLUDED
#define SCENETOON_TERRAIN_INPUT_INCLUDED

// 지형(Unity Terrain) 전용 입력 — B2 초안 (Docs/10 §7.3, D-4 Unity Terrain 경로).
//   4-스플랫(_Control RGBA + _Splat0..3) 1차 블렌딩. 공유 음영 코어로 SceneToon 과 동일 셀 톤.
//   draft 한계: 5+ 레이어(add pass)·터레인 홀·레이어별 노멀맵·인스턴싱 미지원(B4+ 에서 확장).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "../../ToonShared/ShaderLibrary/ToonRamp.hlsl"
#include "../../ToonShared/ShaderLibrary/ToonBandShadow.hlsl"

TEXTURE2D(_Control); SAMPLER(sampler_Control);
TEXTURE2D(_Splat0);  SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);  SAMPLER(sampler_Splat1);
TEXTURE2D(_Splat2);  SAMPLER(sampler_Splat2);
TEXTURE2D(_Splat3);  SAMPLER(sampler_Splat3);

CBUFFER_START(UnityPerMaterial)
    float4 _Control_ST;
    float4 _Splat0_ST;
    float4 _Splat1_ST;
    float4 _Splat2_ST;
    float4 _Splat3_ST;

    half4  _ShadowColor;
    half4  _Shadow2ndColor;
    half4  _GIShadeColor;

    half   _ShadowBorder;
    half   _ShadowBlur;
    half   _Shadow2ndBorder;
    half   _Shadow2ndBlur;
    half   _ShadowStrength;
    half   _ReceiveShadowStrength;

    half   _ShadeFloor;
    half   _AmbientStrength;
    half   _GIBandCount;
    half   _GIBandSoftness;
CBUFFER_END

// 4-스플랫 알베도 블렌딩.
half3 SampleTerrainAlbedo(float2 uv)
{
    half4 ctrl = SAMPLE_TEXTURE2D(_Control, sampler_Control, uv * _Control_ST.xy + _Control_ST.zw);
    half3 a0 = SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, uv * _Splat0_ST.xy + _Splat0_ST.zw).rgb;
    half3 a1 = SAMPLE_TEXTURE2D(_Splat1, sampler_Splat1, uv * _Splat1_ST.xy + _Splat1_ST.zw).rgb;
    half3 a2 = SAMPLE_TEXTURE2D(_Splat2, sampler_Splat2, uv * _Splat2_ST.xy + _Splat2_ST.zw).rgb;
    half3 a3 = SAMPLE_TEXTURE2D(_Splat3, sampler_Splat3, uv * _Splat3_ST.xy + _Splat3_ST.zw).rgb;
    half wsum = max(ctrl.r + ctrl.g + ctrl.b + ctrl.a, 1e-4h);
    return (a0 * ctrl.r + a1 * ctrl.g + a2 * ctrl.b + a3 * ctrl.a) / wsum;
}

#endif // SCENETOON_TERRAIN_INPUT_INCLUDED
