#ifndef TOONSHARED_RIM_INCLUDED
#define TOONSHARED_RIM_INCLUDED

// 배경(SceneToon) 전용 셀 코어 — 프레넬 기반 샤프 림.
//   참고: 캐릭터 림과 달리 softness=0 가드(max)가 있어 동작이 미세하게 다름(캐릭터는 ToonShared 비의존이라 무관).
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
