#ifndef SCENETOON_INPUT_INCLUDED
#define SCENETOON_INPUT_INCLUDED

// 배경 전용 툰 셰이더 입력 (Docs/10). 모든 패스가 이 파일 하나만 include 한다.
//   UnityPerMaterial CBUFFER 를 단일 정의로 강제 → 전 패스 동일 레이아웃(SRP Batcher 핵심, 계획서 8장).
// 배경 전용 셀 코어(ToonShared)를 사용 — 캐릭터 인라인 수식을 복제해 톤 일치(캐릭터는 ToonShared 비의존, D-3 재검토).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "../../ToonShared/ShaderLibrary/ToonRamp.hlsl"
#include "../../ToonShared/ShaderLibrary/ToonBandShadow.hlsl"
#include "../../ToonShared/ShaderLibrary/ToonRim.hlsl"

// --- 텍스처 / 샘플러 (CBUFFER 밖) ---
TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);      SAMPLER(sampler_BumpMap);
TEXTURE2D(_ILMMap);       SAMPLER(sampler_ILMMap);     // 배경 ILM: 주로 G(그림자 편향)/A(외곽 억제)만 사용
TEXTURE2D(_RampMap);      SAMPLER(sampler_RampMap);
TEXTURE2D(_LayerMap);     SAMPLER(sampler_LayerMap);   // B4: 정점컬러 2-알베도 블렌딩용 2차 레이어
TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);// B4: AO(R)

// --- 머티리얼 상수: 전 패스 동일 레이아웃 유지 (순서/타입 변경 금지) ---
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half4  _RimColor;
    half4  _ShadowColor;     // 1차 그림자 색
    half4  _Shadow2ndColor;  // 2차(심부) 그림자 색
    half4  _GIShadeColor;    // 라이트맵 툰화 시 그림자 영역 틴트

    // Surface 렌더 상태(Opaque/Transparent/Cutout). ShaderGUI 가 Blend/ZWrite/RenderQueue 설정.
    half   _Surface;
    half   _SrcBlend;
    half   _DstBlend;
    half   _ZWrite;
    half   _Cull;            // 컬링 모드(0=Off 양면/식생, 2=Back). 렌더 상태 Cull [_Cull].
    half   _Cutoff;          // 식생 알파 컷아웃 임계(_ALPHATEST_ON)

    // 노멀맵
    half   _BumpScale;
    half   _UseNormalMap;    // [Toggle(_USE_NORMALMAP)] backing

    // Ramp / 음영 공통
    half   _RampRow;
    half   _ShadowOffsetScale;
    half   _ShadeFloor;
    half   _AmbientStrength;
    half   _UseRamp;         // [Toggle(_USE_RAMP)] backing
    half   _UseILM;          // [Toggle(_USE_ILM)] backing

    // 파라메트릭 밴드(공유 코어)
    half   _ShadowBorder;
    half   _ShadowBlur;
    half   _Shadow2ndBorder;
    half   _Shadow2ndBlur;
    half   _ShadowStrength;
    half   _ReceiveShadowStrength;

    // 라이트맵 GI 툰화 (B1 에서 본격 사용 — D-2)
    half   _GIBandCount;
    half   _GIBandSoftness;

    // 림
    half   _RimThreshold;
    half   _RimSoftness;
    half   _RimIntensity;
    half   _UseRim;          // [Toggle(_USE_RIM)] backing

    // 추가광 셀셰이딩
    half   _UseAddLights;    // [Toggle(_USE_ADD_LIGHTS)] backing
    half   _AdditionalLightStrength;

    // per-object 외곽선 억제(B3): DepthNormals.w 에 기록 → SS 아웃라인이 읽어 엣지 제거.
    half   _UseOutlineSuppress; // [Toggle(_USE_OUTLINE_SUPPRESS)] backing
    half   _OutlineSuppress;    // 억제량 0..1 (HLSL 사용)

    // 식생(B2 — D-5). 키워드 off 또는 strength 0 이면 no-op.
    half4  _WindParams;      // x=공간 주파수, y=난류, z=잎 펄럭, w=예약
    half   _WindStrength;
    half   _WindSpeed;
    half   _UseWind;         // [Toggle(_USE_WIND)] backing
    half   _TranslucencyStrength;
    half4  _TranslucencyColor;
    half   _UseTranslucency; // [Toggle(_USE_TRANSLUCENCY)] backing

    // B4 — 트라이플래너 / 정점컬러 2-알베도 블렌딩 / AO
    half   _UseTriplanar;       // [Toggle(_USE_TRIPLANAR)] backing
    half   _TriplanarScale;     // 월드 UV 스케일
    half   _TriplanarBlend;     // 평면 블렌드 샤프니스(클수록 경계 또렷)
    half   _UseVertexBlend;     // [Toggle(_USE_VERTEXCOLOR_BLEND)] backing
    half4  _LayerColor;         // 2차 레이어 틴트
    half   _LayerBlendChannel;  // 0=R/1=G/2=B/3=A 정점컬러 채널(float 분기)
    half   _UseOcclusion;       // AO 사용(키워드 아님 — float 분기, off면 페치 스킵)
    half   _OcclusionStrength;
CBUFFER_END

// 식생 바람(정점) — 모든 지오메트리 패스(Forward/Shadow/Depth/DepthNormals)가 동일 호출 → 그림자/깊이 정합.
//   지면 고정(오브젝트 로컬 y가 클수록 더 흔들림), 저주파 스웨이 + 고주파 펄럭. _USE_WIND off 또는 _WindStrength 0 = no-op.
//   sin 유계라 NaN/폭주 없음("무풍/강풍 안정"). 바람 방향은 draft 고정(추후 글로벌 주입 가능 — D-5).
float3 ApplyWindWS(float3 positionWS, float3 positionOS)
{
#if defined(_USE_WIND)
    float t = _Time.y * _WindSpeed;
    float stiffness = max(positionOS.y, 0.0);                       // 밑동(y=0) 고정, 끝으로 갈수록 ↑
    float phase = (positionWS.x + positionWS.z) * _WindParams.x + t;
    float sway    = sin(phase) + _WindParams.y * sin(phase * 2.37 + 1.3);
    float flutter = _WindParams.z * sin(t * 6.0 + (positionWS.x + positionWS.z) * 3.0);
    float2 dir = normalize(float2(1.0, 0.3));                       // draft 고정 바람 방향(XZ)
    positionWS.xz += dir * ((sway + flutter) * stiffness * _WindStrength * 0.1);
#endif
    return positionWS;
}

// B4 트라이플래너 — 월드 위치를 3평면으로 샘플, 지오메트릭 노멀로 블렌드(경사 UV 스트레치 제거).
//   바위/절벽/메시 지형용. 텍스처/샘플러는 호출 측이 전달.
half3 SampleTriplanar(TEXTURE2D_PARAM(tex, smp), float3 positionWS, half3 N, half scale, half sharp)
{
    half3 bw = pow(abs(N), max(sharp, 1.0h));
    bw /= (bw.x + bw.y + bw.z + 1e-4h);
    half3 cx = SAMPLE_TEXTURE2D(tex, smp, positionWS.zy * scale).rgb; // X 평면
    half3 cy = SAMPLE_TEXTURE2D(tex, smp, positionWS.xz * scale).rgb; // Y 평면(상면)
    half3 cz = SAMPLE_TEXTURE2D(tex, smp, positionWS.xy * scale).rgb; // Z 평면
    return cx * bw.x + cy * bw.y + cz * bw.z;
}

// B4 정점컬러 채널 선택(0=R/1=G/2=B/3=A).
half SelectVertexChannel(half4 vc, half ch)
{
    return ch < 0.5h ? vc.r : (ch < 1.5h ? vc.g : (ch < 2.5h ? vc.b : vc.a));
}

#endif // SCENETOON_INPUT_INCLUDED
