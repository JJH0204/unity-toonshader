#ifndef TOONSHARED_RIM_INCLUDED
#define TOONSHARED_RIM_INCLUDED

// 공유 툰 코어 (D-3): 프레넬 기반 샤프 림. 캐릭터(계획서 본편 4.4)·배경이 동일한 림 톤 공유.
// 규약: CBUFFER 비참조, 인자 전달만. Core.hlsl 선행 include 가정.

// N=월드 노멀(정규화), V=월드 뷰 방향(정규화). threshold 위에서 림 발생, softness 로 경계 폭.
//   softness→0 이면 step 에 가까운 날카로운 경계(Genshin/Blue Protocol 풍).
half3 ToonRim(half3 N, half3 V, half threshold, half softness, half3 rimColor, half intensity)
{
    half fresnel = 1.0h - saturate(dot(N, V));
    half rim = smoothstep(threshold, threshold + max(softness, 1e-4h), fresnel);
    return rim * rimColor * intensity;
}

#endif // TOONSHARED_RIM_INCLUDED
