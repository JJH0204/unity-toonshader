# Unity 6 URP HLSL 툰 셰이더 구현 계획 (최종)

| 항목 | 내용 |
|---|---|
| 목적 | 쿼터뷰 인게임과 1인칭 로비 쇼케이스를 하나의 URP/HLSL 캐릭터 셰이더 체계로 구현 |
| 엔진 기준 | Unity 6.3 LTS, URP 17.x 계열. 정확한 패치 버전은 프로젝트 생성 후 고정 |
| 렌더링 방향 | 자체 HLSL 셰이더. NiloCat 예제·UTS3·lilToon은 채택 대상이 아니라 검증된 참고 자료 |
| 핵심 룩 | LUT Ramp, ILM 맵, Face SDF, Inverted Hull Outline, 캐릭터 전용 라이트, 림 라이트, 고품질 기능(MatCap·Angel Ring·SSS·Eye, 상시 활성) |
| 배포 파이프라인 | **별도·후순위** (셰이더 작업 완료 후 진행). 본 문서는 구현 범위만 다룸 |
| 선행 문서 | `01_Unity6_URP_HLSL_툰셰이더_구현계획.md` (1차 검증본) |
| 문서 버전 | v2 (최종) + 개정노트(아래) |

---

## ⚠️ 개정 노트 (2026-06-17, 결정 #15·#14 — 본문 전역 오버라이드)

> 본 v2 문서는 **2티어(인게임 저비용 / 로비 HQ)** 를 전제로 작성되었으나, 이후 방향이 바뀌었다. 아래가 **본문의 모든 2티어 서술보다 우선**한다. (상세: `03_URA_리소스_연동_구현계획.md` 결정 #15·#14, 메모리 `single-tier-lobby-quality`·`liltoon-ux-direction`)
>
> - **결정 #15 — 단일 티어**: 인게임/로비 품질을 분리하지 않고 **로비(고품질) 기준 단일 셰이더**로 구현한다. 본문에서 "인게임 티어=저비용/기능 OFF, 로비 티어=HQ"로 나눈 모든 구절은 무효. `LOBBY_HQ` 키워드/게이트는 **제거 대상**이며, HQ 기능(MatCap·Angel Ring·SSS·Eye·smoothstep SDF·rim boost)은 **상시 활성**. "인게임/로비"는 이제 *품질 등급이 아니라 사용 화면(쿼터뷰/근접)의 차이*일 뿐이다. 추가로 **NiloToon 급** 품질을 목표로 도전.
>   - 쿼터뷰 다수 캐릭터 성능은 **M5 프로파일링에서 재점검**(로비급 단가의 다수 렌더 부담 가능).
> - **결정 #14 — lilToon UX**: 머티리얼 인스펙터/워크플로를 lilToon 사용성에 맞춘다(자작 ShaderGUI: 카테고리 폴드아웃 + Simple/Advanced + 프리셋). lilToon은 여전히 참고용(런타임 비의존, 코드 비복사).
> - **구현 현황**: M0–M4 본문 대부분 구현됨. Emission 기능(`_USE_EMISSION`)·ILM 패커·ShaderGUI 추가됨. 현재는 URA 샘플 캐릭터 연동 단계.

---

## 0. 검증 결과 요약

1차 검증본의 판단을 계승하되 **품질 티어 분기는 폐기**(결정 #15). 자체 HLSL + 검증된 셰이더를 정답지로 참조, **단일 고품질(로비 기준) 셰이더**, 진짜 비용은 에디터 자동화라는 방향은 유효하다.

확정한 사실(2026-06 기준):

- **Unity Toon Shader**: GitHub `0.14.1-preview`, package.json 최소 `6000.0`. 여전히 preview → 런타임 의존 금지, 참고만.
- **Unity 6.3 LTS**: 2027년 12월까지 지원. **6.3 LTS 기준이 출시·라이브 운영에 맞다**(6.0 LTS는 2026-10 종료).
- **NiloCat 공개 예제**: "Unity 2022.3 LTS용 학습 예제". Unity 6 무검증 베이스로 확정 금지 → T0 포팅 스파이크 필수.
- **lilToon**: 확인 시점 안정 릴리스 `2.3.2`(2.3.x대 유지보수 중), 최소 Unity 2022.3, MIT. 직접 채택이 아니라 프리셋/인스펙터 UX·기능 토글 설계만 참고.

본 최종본에서 검증본 대비 보강한 것:

- **림 라이트 복원**: 셀 셰이딩 필수 요소이자 로비 상호작용 반응 요구사항인데 검증본 HLSL/CBUFFER에서 누락되어 있어 복원(4.4, 8).
- **미해결 결정 레지스터(10장)**: 본문에 흩어져 있던 "M0/M1에서 검증" 항목을 담당·시점과 함께 한 표로 통합.
- **배포 파이프라인 분리**: 팀 배포·작가 오소링 UX는 후순위로 제외. 단, 셰이더 검증에 필수인 최소 인에디터 툴(SDF 검증기·스무스 노멀 베이커)만 구현 전제로 유지(9장).

참고 출처:
- UTS repo: https://github.com/Unity-Technologies/com.unity.toonshader
- UTS package.json: https://raw.githubusercontent.com/Unity-Technologies/com.unity.toonshader/master/com.unity.toonshader/package.json
- Unity 6 support: https://unity.com/releases/unity-6/support
- NiloCat 예제: https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
- lilToon: https://github.com/lilxyzw/lilToon

---

## 1. 구현 목표

### 1.1 쿼터뷰(인게임) 사용 시 고려사항
> 결정 #15로 **품질 티어 구분 폐기**. 아래는 *품질을 낮추는 별도 티어가 아니라* 쿼터뷰 화면에서 신경 쓸 점이다(품질은 로비 기준 단일).

- 실루엣 안정성·다수 캐릭터 성능·명암 가독성 우선
- HQ 기능(MatCap·Angel Ring·피부 SSS·눈 시차 등)도 **상시 활성**이 기본. 비용은 M5 프로파일링에서 재점검
- 아웃라인은 군중/원거리에서 width=0 프리셋으로 비활성 가능(LOD 전략)
- SRP Batcher·변형 수 최소화는 여전히 최우선

### 1.2 근접 쇼케이스(로비) — 단일 품질 기준

이 게임의 캐릭터 상품성을 보여주는 핵심 화면이자 **셰이더 품질의 기준선**. 아래 기능들은 (티어가 아니라) 단일 셰이더에 **상시 포함**된다:

- 풀 해상 Face SDF + 얼굴 위 헤어 그림자
- 눈동자 시차, 고정 하이라이트, 속눈썹/눈썹/앞머리 렌더 순서 제어
- 머리카락 Angel Ring + 이방성 하이라이트
- 피부 그림자 경계의 따뜻한 SSS 근사
- 옷 재질 구분용 MatCap/스페큘러 + UV 내부선
- 거리/FOV 변화에 안정적인 클린 아웃라인
- 캐릭터 전용 3점 라이트 리그 + 상호작용 반응 림 + 포토 모드 확장 여지

---

## 2. 셰이더 구조

단일 셰이더 유지, 부위 기능은 local keyword로 제어. (결정 #15: 아래 `LOBBY_HQ` 품질-티어 키워드는 **제거 대상** — HQ 기능 상시 활성. 별도 셰이더 리팩터 태스크에서 게이트 제거 후 본 스니펫도 갱신)

```hlsl
// (제거 대상, 결정 #15) #pragma shader_feature_local _ LOBBY_HQ
#pragma shader_feature_local _ _PART_FACE _PART_HAIR _PART_SKIN _PART_CLOTH
#pragma shader_feature_local _ _USE_MATCAP
#pragma shader_feature_local _ _USE_INNERLINE
#pragma shader_feature_local _ _USE_EYE_PARALLAX
```

패스 구조:

```text
CharacterToon.shader
├─ ForwardToon     LightMode = UniversalForwardOnly 또는 UniversalForward (T0 확정)
├─ Outline         Inverted Hull, Cull Front
├─ ShadowCaster    URP 표준 ShadowCaster 기반
├─ DepthOnly       URP 표준 DepthOnly 기반
└─ DepthNormals    SSAO/DepthNormals 필요 시만 활성
```

`UniversalForwardOnly`는 캐릭터를 Deferred 장면과 분리할 때 유리. Deferred를 쓰지 않으면 `UniversalForward`도 후보 → T0에서 URP 17 실제 경로로 확정.

---

## 3. 데이터 규약

### 3.1 필수 텍스처

| 텍스처 | 용도 | 인게임 | 로비 |
|---|---|---:|---:|
| BaseMap | 기본 색 | 필수 | 필수 |
| ILMMap | 스페큘러·그림자 편향·내부선 마스크 | 필수 | 필수 |
| RampMap | 셀 음영 색상/전이 | 필수 | 필수 |
| FaceSDF | 얼굴 전용 음영 커버리지 | 선택 | 필수 |
| MatCap | 금속/가죽/장식 반사 | OFF | 선택 |
| HairMask | Angel Ring/헤어 스페큘러 마스크 | OFF | 선택 |
| EyeMap | 눈동자·하이라이트·시차 | 단순 | 필수 |

### 3.2 ILM 채널 (프로젝트 중간 변경 금지)

| 채널 | 의미 | 사용처 |
|---|---|---|
| R | 스페큘러/MatCap 강도 | 옷·장식·머리 광택 |
| G | 그림자 진입 편향 | Ramp U 보정 |
| B | 스페큘러 폭 또는 보조 램프 선택 | 재질별 광택 폭 |
| A | 내부선/아웃라인 억제 마스크 | 로비 내부선, 외곽선 부분 제거 |

ILM 누락 시 중립값(R=0, G=0.5, B=0.5, A=0)으로 동작해 텍스처 없이도 일단 선다.

---

## 4. 라이팅

### 4.1 Ramp 기반 기본 음영

```hlsl
half3 N = normalize(input.normalWS);
half3 V = normalize(GetWorldSpaceViewDir(input.positionWS));
half3 L = normalize(GetCharacterLightDirWS(input));

half ndotl = dot(N, L);
half halfLambert = ndotl * 0.5h + 0.5h;

half4 ilm = SAMPLE_TEXTURE2D(_ILMMap, sampler_ILMMap, input.uv);
half shadowBias = (ilm.g - 0.5h) * _ShadowOffsetScale;
half rampU = saturate(halfLambert + shadowBias);
half rampV = _RampRow;

half3 ramp = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, float2(rampU, rampV)).rgb;
half3 shaded = baseColor.rgb * ramp;
```

RampMap은 수학이 아니라 룩 제어 도구. 피부=부드러운 램프, 머리/옷=하드 컷 램프처럼 부위별로 다른 행 사용.

### 4.2 캐릭터 라이트 (다수 캐릭터 전략)

로비는 장면 조명과 분리된 전용 라이트 리그. 인게임은 비용·배칭을 고려해 단계적 접근.

| 상황 | 권장 방식 |
|---|---|
| 로비 단일 캐릭터 | 글로벌 `_LobbyKeyLightDirWS`, `_LobbyFillColor`, `_LobbyRimDirWS` |
| 인게임 다수 캐릭터 | 메인 라이트 추종 + 캐릭터 밝기 하한 |
| 특정 영웅 클로즈업 다수 | StructuredBuffer에 캐릭터별 파라미터 저장 후 renderer ID로 접근 (검토) |

`MaterialPropertyBlock`은 SRP Batcher 효율을 떨어뜨릴 수 있으므로 기본 경로로 쓰지 않는다. SRP Batcher는 같은 shader variant + 지속 material constant buffer로 CPU 비용을 줄이므로 per-renderer 재질 변경을 남발하지 않는다.

> Forward+ 환경에서 `_ADDITIONAL_LIGHTS` 처리 경로가 기존 Forward와 다르므로, 라이트 리그를 additional light로 구성할지 셰이더 프로퍼티로 주입할지 T0에서 함께 확인.

### 4.3 밝기 하한

```hlsl
half3 ambient = SampleSH(N) * _AmbientStrength;
shaded = max(shaded, baseColor.rgb * _ShadeFloor) + ambient;
```

저조도 던전(설원·지하철·동굴)에서 얼굴이 묻히지 않도록 `_ShadeFloor`. 로비는 라이트 리그로 해결하고 하한값은 낮게.

### 4.4 림 라이트 (샤프, 상호작용 반응)

셀 셰이딩의 핵심 어필 요소. 인게임은 약하게, 로비는 강하게 + 상호작용 시 상향.

```hlsl
half fresnel = 1.0h - saturate(dot(N, V));
half rim = smoothstep(_RimThreshold, _RimThreshold + _RimSoftness, fresnel);
half3 rimColor = rim * _RimColor.rgb * _RimIntensity;

#if defined(LOBBY_HQ)
    rimColor *= _RimInteractionBoost; // 스크립트가 상호작용 시 상향(살아있는 인상)
#endif

shaded += rimColor;
```

> 샤프 림(Genshin/Blue Protocol 풍)이 필요하면 `smoothstep` 대신 `step`으로 경계를 날카롭게. 부위별 림 억제는 ILM 보조 채널 또는 별도 마스크로.

---

## 5. Face SDF

얼굴은 일반 `NdotL`로 처리하지 않는다(근접에서 코/입/볼 그림자가 지저분). 정면/우측 벡터와 라이트 방향 관계로 좌우 플립 샘플링.

```hlsl
float2 lightXZ = normalize(_CharacterLightDirWS.xz);
float FdotL = dot(normalize(_FaceForwardWS.xz), lightXZ);
float RdotL = dot(normalize(_FaceRightWS.xz), lightXZ);

float2 uvFace = input.uv;
float2 uvFlip = float2(1.0 - input.uv.x, input.uv.y);

half sdfLeft  = SAMPLE_TEXTURE2D(_FaceSDF, sampler_FaceSDF, uvFace).r;
half sdfRight = SAMPLE_TEXTURE2D(_FaceSDF, sampler_FaceSDF, uvFlip).r;
half sdf = lerp(sdfLeft, sdfRight, step(0.0, RdotL));

half coverage = saturate((-FdotL + 1.0) * 0.5);

#if defined(LOBBY_HQ)
    half faceLit = smoothstep(coverage - _SDFSoftness, coverage + _SDFSoftness, sdf);
#else
    half faceLit = step(coverage, sdf);
#endif
```

**주의(확정식 아님, M1/M2 검증 대상)**: SDF 흑백 의미, UV 좌우 대칭, `RdotL` 플립 방향은 아트 파이프라인에 따라 반전될 수 있다. 에디터 검증 툴에서 좌/우/정면/후면 라이트 테스트를 반드시 통과시킨다.

로비용 얼굴 위 헤어 그림자(2단계):
1. M1 — 얼굴 전용 헤어 섀도우 마스크 텍스처 수동 지정.
2. M2 — 헤어 메시/전용 프록시에서 얼굴 UV로 투영하는 자동화 검토.

---

## 6. 아웃라인 (Inverted Hull)

```hlsl
float3 smoothNormalOS = DecodeSmoothOutlineNormal(input);
float3 posOS = input.positionOS.xyz;
float width = ComputeOutlineWidth(input, _OutlineWidth);

posOS += smoothNormalOS * width * _OutlineMask;
```

검증 항목:
- 스무스 노멀 저장 채널(UV2/UV3/tangent/color) — 프로젝트 충돌 최소 지점 결정(10장).
- 비균일 스케일에서 폭 찌그러짐 여부.
- FOV/거리 변화에서 로비 클로즈업 외곽선 두께 안정성.
- 얼굴 안쪽·입 주변·손가락 사이 불필요 선 → ILM.A 또는 OutlineMask로 억제.
- 모바일 해상도에서 TAA/MSAA와 함께 계단 현상 허용 범위.

인게임 잡몹/군중은 아웃라인 LOD로 끄거나 단순화.

---

## 7. 고품질 기능 (상시 활성 — 구 `LOBBY_HQ`, 결정 #15로 티어 게이트 폐기)

변형 폭증 방지를 위해 실제 사용 조합만 material preset으로 만든다.

```hlsl
#if defined(LOBBY_HQ)
    #if defined(_USE_MATCAP)
        float3 normalVS = mul((float3x3)UNITY_MATRIX_V, N);
        float2 matcapUV = normalVS.xy * 0.5 + 0.5;
        half3 matcap = SAMPLE_TEXTURE2D(_MatCap, sampler_MatCap, matcapUV).rgb;
        shaded += matcap * ilm.r * _MatCapStrength;
    #endif

    #if defined(_PART_HAIR)
        half3 T = normalize(input.tangentWS);
        half3 H = normalize(L + V);
        half aniso = 1.0h - saturate(abs(dot(T, H)));
        half ring = pow(aniso, _AngelRingPower) * _AngelRingIntensity;
        shaded += ring * _AngelRingColor.rgb;
    #endif

    #if defined(_PART_SKIN)
        half border = 1.0h - saturate(abs(rampU - _SkinSSSCenter) / _SkinSSSWidth);
        shaded += border * _SkinSSSColor.rgb * _SkinSSSStrength;
    #endif
#endif
```

눈은 얼굴 셰이더 내부의 부가 기능으로 분리:
- 눈동자 parallax offset(`_USE_EYE_PARALLAX`)
- 카메라 고정 하이라이트 + 하이라이트 마스크
- **속눈썹/앞머리/눈썹 렌더 순서 제어**: 스텐실 또는 별도 머티리얼 큐로, 앞머리에 가려도 눈/하이라이트가 의도대로 보이게(근접 어필 직결). 구현 방식은 M4에서 스텐실 기반으로 우선 검증.

---

## 8. SRP Batcher와 변형 관리

모든 material property를 `CBUFFER_START(UnityPerMaterial)`에, 전 패스 동일 레이아웃 유지.

```hlsl
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _OutlineColor;
    half4 _RimColor;

    half _RampRow;
    half _ShadowOffsetScale;
    half _ShadeFloor;
    half _AmbientStrength;

    // 림
    half _RimThreshold;
    half _RimSoftness;
    half _RimIntensity;
    half _RimInteractionBoost;

    // 아웃라인
    half _OutlineWidth;
    half _OutlineDepthOffset;

    // SDF / 로비 HQ
    half _SDFSoftness;
    half _MatCapStrength;
    half4 _AngelRingColor;
    half _AngelRingIntensity;
    half _AngelRingPower;
    half4 _SkinSSSColor;
    half _SkinSSSCenter;
    half _SkinSSSWidth;
    half _SkinSSSStrength;
CBUFFER_END
```

> 매 프레임 스크립트가 주입하는 값(`_CharacterLightDirWS`, `_FaceForwardWS`, `_FaceRightWS`, 로비 라이트 리그 등)은 per-material CBUFFER 밖의 글로벌 셰이더 프로퍼티로 두어 배쳐 호환을 깨지 않는다.

변형 원칙:
- 파이프라인/그림자 키워드는 URP 패키지 최신 템플릿을 보고 확정.
- 부위별 기능은 `shader_feature_local` 우선.
- 품질 티어는 material preset으로 고정해 런타임 토글 최소화.
- 빌드 전 `ShaderVariantCollection` + URP variant stripping 결과 확인.

---

## 9. 구현 측 최소 검증 툴

> 팀 전체 배포·작가 오소링 UX·프리셋/린터/온보딩은 **후순위 별도 문서**로 다룬다(셰이더 완료 후). 아래는 셰이더 자체를 검증하기 위해 구현과 동시에 필요한 최소 인에디터 도구만.

1. **스무스 아웃라인 노멀 베이커** — FBX 무수정으로 평활 노멀을 합의된 정점 채널에 베이크(아웃라인 검증 전제, M3).
2. **Face SDF 검증 툴** — forward/left 벡터 세팅 + 가상 광원 좌→우 회전 프리뷰 + 좌우 플립 정상 판정(M1/M2 게이트).

이 둘은 결과물의 일관성 도구가 아니라 **셰이더 정확성 게이트**이므로 구현 범위에 포함한다.

---

## 10. 미해결 결정 레지스터

| # | 결정 항목 | 옵션 | 결정 시점 | 담당 |
|---|---|---|---|---|
| 1 | ForwardToon LightMode | `UniversalForwardOnly` vs `UniversalForward` | T0 | 셰이더 |
| 2 | NiloCat 예제 Unity 6 포팅 성립 여부 | 포팅 / 처음부터 자작 | T0 | 셰이더 |
| 3 | 스무스 노멀 저장 채널 | UV2 / UV3 / tangent / color | M3 | 셰이더 + 모델러 |
| 4 | 다수 캐릭터 라이트 방식 | 메인추종 / 라이트그룹 / StructuredBuffer+ID | M1~ | 셰이더 |
| 5 | Face SDF 흑백·UV대칭·플립 방향 | 아트 파이프라인에 맞춰 확정 | M1/M2 | 셰이더 + 아트 |
| 6 | 아웃라인 거리/FOV 보정식 | 비균일스케일·스키닝·모바일 검증 후 | M0/M3 | 셰이더 |
| 7 | 얼굴 위 헤어 그림자 자동 투영 | 수동 마스크 / 프록시 투영 | M2 | 셰이더 |
| 8 | Forward+ additional light 경로 | additional light / 프로퍼티 주입 | T0 | 셰이더 |
| 9 | 눈 렌더 순서 | 스텐실 / 머티리얼 큐 | M4 | 셰이더 |
| 10 | 변형 stripping 범위 | 사용 조합 preset 고정 | M5 | 셰이더 |

---

## 11. 마일스톤

| 단계 | 산출물 | 통과 조건 |
|---|---|---|
| M0 | Unity 6.3 URP 빈 프로젝트 + NiloCat 예제 포팅 스파이크 | 컴파일·씬 렌더·SRP Batcher 호환 확인, 결정 #1/#2/#6/#8 정리 |
| M1 | BaseMap + ILM + RampMap + 캐릭터 라이트 + 림 | 1체/10체 테스트에서 룩·CPU 비용 확인 |
| M2 | Face SDF + 얼굴 검증 툴 최소판 | 좌/우/정면/후면 라이트 통과, 로비 클로즈업 통과 |
| M3 | Inverted Hull + 스무스 노멀 베이크 연동 | 스키닝·비균일 스케일·FOV에서 외곽선 안정 |
| M4 | HQ 기능(상시 활성): 눈, MatCap, Angel Ring, 피부 SSS | 대표 캐릭터 1체 마케팅 스크린샷 품질 도달 |
| M5 | 변형 정리·프로파일링·PC/모바일 품질 프리셋 | 인게임/로비 목표 프레임 통과 |

---

## 12. 완료 기준

- 대표 캐릭터 1체가 로비 쇼케이스 씬에서 얼굴·눈·머리·옷 재질의 어필 포인트를 명확히 보여준다.
- 쿼터뷰 전투 씬에서 다수 캐릭터가 SRP Batcher 친화적 변형 수로 렌더된다.
- Face SDF와 Outline이 에디터 검증 툴에서 자동 체크 가능하다.
- 미해결 결정 레지스터의 전 항목이 해당 마일스톤에서 닫힌다.
- (배포 파이프라인은 본 문서 범위 밖 — 셰이더 완료 후 별도 착수)
