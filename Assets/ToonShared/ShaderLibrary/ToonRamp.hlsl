#ifndef TOONSHARED_RAMP_INCLUDED
#define TOONSHARED_RAMP_INCLUDED

// 공유 툰 코어 (D-3): 캐릭터(CharacterToon)·배경(SceneToon)이 동일한 셀 톤을 내기 위한
//   half-Lambert → Ramp 입력(U) 변환과 Ramp LUT 샘플 헬퍼.
// 규약: 모든 함수는 CBUFFER 멤버를 직접 참조하지 않고 인자로만 받는다(양쪽 셰이더 재사용 위해).
// 선행: 호출 측이 com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl 을 먼저 include 한다
//   (TEXTURE2D_PARAM / SAMPLE_TEXTURE2D / half 타입 매크로 제공).

// NdotL(-1..1) → half-Lambert(0..1). 셀 음영의 표준 입력.
half ToonHalfLambert(half ndotl)
{
    return ndotl * 0.5h + 0.5h;
}

// half-Lambert 에 그림자 진입 편향(ILM.G 규약: (g-0.5)*scale)을 더해 Ramp U 산출.
//   bias>0 = 빛 쪽(그림자 늦게 진입), bias<0 = 그림자 쪽.
half ToonRampU(half halfLambert, half shadowBias)
{
    return saturate(halfLambert + shadowBias);
}

// Ramp LUT(가로=음영 전이, 세로=재질군 행) 샘플. 텍스처/샘플러는 호출 측이 넘긴다.
half3 SampleToonRamp(TEXTURE2D_PARAM(rampTex, rampSampler), half rampU, half rampRow)
{
    return SAMPLE_TEXTURE2D(rampTex, rampSampler, float2(rampU, rampRow)).rgb;
}

#endif // TOONSHARED_RAMP_INCLUDED
