#ifndef TOONSHARED_BANDSHADOW_INCLUDED
#define TOONSHARED_BANDSHADOW_INCLUDED

// 공유 툰 코어 (D-3): 파라메트릭 1·2차 그림자 밴드.
//   CharacterToon.shader ForwardToon 의 밴드 공식(결정 #17)을 함수로 추출 — 캐릭터/배경이
//   동일한 border/blur/strength 의미로 같은 셀 경계를 그려 한 화면 톤을 일치시킨다.
// 규약: CBUFFER 멤버 비참조, 인자 전달만. Core.hlsl 선행 include 가정.

// 단일 밴드 계수(0=빛, 1=그림자). blur=0(하드 컷) 시 0-나눗셈 방지 가드.
//   smoothstep(border-blur, border+blur, lightVal) 의 보색 = "그림자 정도".
half ToonBandFactor(half lightVal, half border, half blur, half strength)
{
    half b = max(blur, 1e-4h);
    return (1.0h - smoothstep(border - b, border + b, lightVal)) * strength;
}

// 1·2차 밴드를 albedo 에 입혀 셰이딩된 색을 반환. shadeMask(0=빛,1=그림자) 출력 —
//   호출 측이 앰비언트/하이라이트 대비를 셀 톤에 맞춰 조절하는 데 사용.
half3 ToonShadeBands(
    half3 albedo, half lightVal,
    half border1, half blur1, half3 shadowColor1,
    half border2, half blur2, half3 shadowColor2,
    half strength, out half shadeMask)
{
    half s1 = ToonBandFactor(lightVal, border1, blur1, strength);
    half s2 = ToonBandFactor(lightVal, border2, blur2, strength);
    half3 c = albedo;
    c = lerp(c, albedo * shadowColor1, s1); // 1차 그림자 색
    c = lerp(c, albedo * shadowColor2, s2); // 2차(심부) 그림자 색
    shadeMask = saturate(max(s1, s2));
    return c;
}

// 베이크 GI(라이트맵/SH) 휘도를 밴드로 양자화해 셀 톤화 (배경 핵심 — D-2 후보식).
//   bandCount 단계로 계단화 후 softness 로 경계 AA. 정적 배경의 부드러운 PBR GI 를 셀 룩으로 끌어온다.
half ToonPosterize(half value, half bandCount, half softness)
{
    half n = max(bandCount, 1.0h);
    half scaled = saturate(value) * n;
    half lower = floor(scaled);
    half fracPart = scaled - lower;    // 0..1 현재 밴드 내 위치 (frac 내장함수 가림 방지)
    half s = max(softness, 1e-4h);
    half edge = smoothstep(0.5h - s, 0.5h + s, fracPart); // 밴드 경계 중앙에서 다음 단계로
    return saturate((lower + edge) / n);
}

#endif // TOONSHARED_BANDSHADOW_INCLUDED
