#ifndef CHARACTER_TOON_INPUT_INCLUDED
#define CHARACTER_TOON_INPUT_INCLUDED

// 모든 패스가 이 파일 하나만 include 한다.
// UnityPerMaterial CBUFFER 레이아웃을 단일 정의로 강제 -> 전 패스 동일 레이아웃 (SRP Batcher 핵심, 계획서 8장).

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

// --- 텍스처 / 샘플러 (CBUFFER 밖) ---
TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);        SAMPLER(sampler_BumpMap);
TEXTURE2D(_ILMMap);         SAMPLER(sampler_ILMMap);
TEXTURE2D(_RampMap);        SAMPLER(sampler_RampMap);
TEXTURE2D(_FaceSDF);        SAMPLER(sampler_FaceSDF);
TEXTURE2D(_HairShadowMask); SAMPLER(sampler_HairShadowMask);
TEXTURE2D(_ShadowMaskTex);  SAMPLER(sampler_ShadowMaskTex);  // 2-1: 일반 그림자 억제 마스크(R)
TEXTURE2D(_StencilMask);    SAMPLER(sampler_StencilMask);    // 3-2: 스텐실 영역 마스크(R, 앞머리 투과)
TEXTURE2D(_MatCap);         SAMPLER(sampler_MatCap);
TEXTURE2D(_MatCapMask);     SAMPLER(sampler_MatCapMask);
TEXTURE2D(_MatCap2);        SAMPLER(sampler_MatCap2);      // 2-5: 두 번째 MatCap
TEXTURE2D(_MatCap2Mask);    SAMPLER(sampler_MatCap2Mask);
TEXTURE2D(_AngelRingMask);  SAMPLER(sampler_AngelRingMask);// 4-6: 천사고리 결 마스크(R)
TEXTURE2D(_EyeMap);         SAMPLER(sampler_EyeMap);
TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);
TEXTURE2D(_MetallicGlossMap);   SAMPLER(sampler_MetallicGlossMap);
TEXTURE2D(_OutlineMap);     SAMPLER(sampler_OutlineMap);
TEXTURE2D(_OutlineMask);    SAMPLER(sampler_OutlineMask);

// --- 머티리얼 상수: 전 패스 동일 레이아웃 유지 (순서/타입 변경 금지) ---
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half4  _OutlineColor;
    half4  _RimColor;

    // 2-3: Surface 모드 렌더 상태(Opaque/Transparent). 패스의 Blend/ZWrite가 이 값을 읽는다.
    // ShaderGUI 상단 Rendering Mode 드롭다운이 모드 변경 시 설정. 기본 = Opaque(One/Zero/ZWrite On).
    half   _Surface;
    half   _SrcBlend;
    half   _DstBlend;
    half   _ZWrite;

    // WP-B: 노멀맵
    half   _BumpScale;
    half   _UseNormalMap;     // [Toggle(_USE_NORMALMAP)] backing float, batcher 유지 위해 CBUFFER 포함

    half   _RampRow;
    half   _ShadowOffsetScale;
    half   _ShadeFloor;
    half   _AmbientStrength;
    half   _AOStrength;       // SSAO 영향도(0=무시, 1=URP 표준). _SCREEN_SPACE_OCCLUSION 시만.

    // 결정 #17: 파라메트릭 1·2차 그림자 밴드 (모델러 요구 — border/blur/color/range)
    half4  _ShadowColor;
    half4  _Shadow2ndColor;
    half   _ShadowBorder;
    half   _ShadowBlur;
    half   _Shadow2ndBorder;
    half   _Shadow2ndBlur;
    half   _ShadowStrength;
    half   _ReceiveShadowStrength;
    half   _ShadowMaskType;   // 2-1: 0=SDF(얼굴 플립샘플) / 1=Strength(half-Lambert 일반 음영). float 분기(키워드 아님 — 변형 절감)
    half   _UseRamp;          // [Toggle(_USE_RAMP)] backing float, batcher 유지 위해 CBUFFER 포함

    // 림
    half   _RimThreshold;
    half   _RimSoftness;
    half   _RimIntensity;
    half   _RimInteractionBoost;
    half   _UseRim;           // [Toggle(_USE_RIM)] backing float (2-4 Use 토글)
    // L3: 부가광 셀셰이딩
    half   _UseAddLights;     // [Toggle(_USE_ADD_LIGHTS)] backing float
    half   _AdditionalLightStrength;

    // L2: 깊이 인지 림(스크린스페이스)
    half4  _DepthRimColor;
    half   _DepthRimWidth;
    half   _DepthRimThreshold;
    half   _DepthRimIntensity;
    half   _UseDepthRim;      // [Toggle(_USE_DEPTH_RIM)] backing float

    // 아웃라인 (WP-E: 거리 페이드 추가)
    half   _UseOutline;       // [Toggle(_USE_OUTLINE)] backing float (2-4 Use 토글) — Outline 패스에서 사용
    half   _OutlineWidth;
    half   _OutlineDepthOffset;
    half   _OutlineDistanceFade;   // 0=화면일정(기존), 1=원거리 얇아짐
    half   _OutlineFadeStart;      // 페이드 시작 거리

    // SDF / 로비 HQ
    half   _SDFSoftness;
    half   _MatCapStrength;
    half   _UseMatCap;        // [Toggle(_USE_MATCAP)] backing float
    half   _UseMatCapMask;    // 결정 #16: MatCap 마스크 분기 float(키워드 아님 — 변형 절감, _USE_MATCAP 내부 lerp)
    half   _MatCapNormalStrength; // 4-5: 노말이 MatCap UV에 미치는 영향도(0=지오메트릭 매끈, 1=노말맵 섭동). MatCap/2 공통.
    // 2-5: 두 번째 MatCap (별도 슬롯/강도/마스크/블렌드)
    half   _MatCap2Strength;
    half   _UseMatCap2;       // [Toggle(_USE_MATCAP2)] backing float
    half   _UseMatCap2Mask;   // float 분기(변형 절감)
    half   _MatCap2Blend;     // 0=Add(가산), 1=Multiply(곱) — float 분기
    half4  _AngelRingColor;
    half   _AngelRingIntensity;
    half   _AngelRingPower;
    half   _AngelRingAngle;   // 4-6: 링 방향 회전(도). 세로↔가로 등.
    half   _UseAngelRing;     // [Toggle(_USE_ANGELRING)] backing float (2-4 Use 토글, _PART_HAIR 내)
    half4  _SkinSSSColor;
    half   _SkinSSSCenter;
    half   _SkinSSSWidth;
    half   _SkinSSSStrength;
    half   _UseSSS;           // [Toggle(_USE_SSS)] backing float (2-4 Use 토글, _PART_SKIN 내)

    // M4: Eyes (T4-4)
    half   _UseEyeParallax;   // [Toggle(_USE_EYE_PARALLAX)] backing float (2-4 Use 토글)
    half   _EyeParallaxStrength;
    half   _EyeHighlightStrength;

    // M4: Eye render-order stencil (T4-5)
    half   _StencilRef;
    half   _StencilComp;
    half   _StencilPass;
    // 3-2: 앞머리 투과(stencil) 보조 — 깊이 테스트 모드 + 영역 마스크
    half   _ZTestMode;
    half   _UseStencilMask;     // [Toggle(_USE_STENCIL_MASK)] backing float
    half   _StencilMaskCutoff;

    // S2: 거리 기반 품질 페이드 — 원거리에서 비싼 HQ 가산항을 스킵+페이드(다수 캐릭터 비용 방어, Docs/04 S2)
    half   _UseQualityFade;     // [Toggle(_USE_QUALITY_FADE)] backing float
    float  _QualityFadeStart;   // 이 거리부터 HQ 감소 시작
    float  _QualityFadeEnd;     // 이 거리에서 HQ 가산항 0(이후 연산 스킵)

    // M1: ILM 토글
    half   _UseILM;

    // M2: Face SDF + Hair Shadow
    half   _UseHairShadow;    // [Toggle(_USE_HAIR_SHADOW)] backing float (2-4 Use 토글)
    half   _HairShadowStrength;

    // Part 선택 (KeywordEnum _Part) — 키워드 구동용 머티리얼 프로퍼티, 배쳐 유지 위해 CBUFFER 포함
    half   _Part;

    // WP-D: PBR 툰 스페큘러 / 리플렉션
    half4  _SpecularTint;
    half   _Metallic;
    half   _Smoothness;
    half   _SpecularStep;
    half   _ReflectionStrength;
    half   _UsePBR;           // [Toggle(_USE_PBR)] backing float

    // Emission (M4)
    half4  _EmissionColor;
    half   _UseEmission;      // [Toggle(_USE_EMISSION)] backing float, batcher 유지 위해 CBUFFER 포함

    // 진단용 (Toggle _DebugFaceLit)
    half   _DebugFaceLit;
CBUFFER_END

// --- 매 프레임 스크립트가 주입하는 글로벌 (UnityPerMaterial 밖! 배쳐 호환 유지, 계획서 8장) ---
// M1(T1-5)/M2(T2-2)에서 C# 컴포넌트가 Shader.SetGlobalVector 로 채운다.
float4 _CharacterLightDirWS;
float4 _CharacterLightColor;   // L1: rgb=색(linear), a=세기. a<=0 이면 메인 라이트 색 사용.
float4 _FaceForwardWS;
float4 _FaceRightWS;

#endif // CHARACTER_TOON_INPUT_INCLUDED



