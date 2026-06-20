# 04 — NiloToon 급 기능 갭 로드맵

> 목적: 결정 #15(단일 고품질 + **NiloToon 급 도전**)에 맞춰, 현재 CharacterToon 기능과 NiloToon 급 목표 사이의 **갭을 리스트업**하고 우선순위·도전 범위를 정한다.
> 근거: 메모리 `single-tier-lobby-quality`, `Docs/01`(개정본), `Docs/03`.
> ⚠️ 본 목록은 NiloToon 공개 정보 + 셀툰 일반 기법 지식 기반으로 작성. **확정 전 NiloToon 공식 문서/Booth 설명과 대조 권장**(NiloCat: https://github.com/ColinLeung-NiloCat , NiloToon은 유료 에셋 — 코드 비참조, 기능/UX만 벤치마크).

---

## A. 현재 보유 기능 스냅샷 (CharacterToon)
- Base albedo + `_BaseColor`, 부위 키워드(`_PART_*`)
- ILM 마스크(RGBA 규약) + 중립 폴백, **ILM 패커 에디터 툴**
- Ramp 음영(half-Lambert + ILM.G 경계 바이어스), `_ShadeFloor`, SH 앰비언트
- 캐릭터 전용 라이트 **글로벌 주입**(`_CharacterLightDirWS`, w 폴백) + Forward+ 클러스터 라이트 병행
- 림 라이트(프레넬 smoothstep) + 상호작용 부스트(상시)
- **Face SDF** 좌우 플립 샘플링(항상 smoothstep) + 수동 헤어 그림자 마스크 + 검증 툴
- **Inverted Hull 아웃라인**: 스무스 노멀(tangent) 베이커, 거리/FOV/비균일스케일 보정, ILM.A 억제, width=0 LOD
- MatCap(ILM.R 마스크), 헤어 **Angel Ring**(이방성), 피부 **SSS 근사**, 눈 **Parallax+하이라이트**, **Emission**(별도 맵), 스텐실 렌더순서
- 5패스(ForwardToon/Outline/ShadowCaster/DepthOnly/DepthNormals), 단일 CBUFFER(SRP Batcher), 변형 스트리퍼
- lilToon식 **커스텀 ShaderGUI**(폴드아웃 + Simple/Advanced)

---

## B. 갭 리스트 (카테고리별)
> 표기 — 현재 → NiloToon급 목표 / **P0**(핵심·우선) **P1**(중요) **P2**(고급·후순위)

### B1. 라이팅 & 그림자
| # | 기능 | 현재 | NiloToon급 목표 | P |
|---|---|---|---|---|
| L1 | **퍼-캐릭터 라이트 제어 시스템** | 단일 글로벌 주입 컴포넌트 | RendererFeature/Volume로 캐릭터별 라이트 방향·색·강도 오버라이드, 씬 라이트와 블렌드, 다수 캐릭터 일괄 관리 | **P1** |
| L2 | **깊이 인지 림 라이트** | 프레넬(노멀 기반)만 | depth 비교로 **화면상 폭 일정** 림(실루엣 외곽 따라가는 라이트), 후광/역광 표현 | **P1** |
| L3 | **추가 라이트(포인트/스폿) 셀 반영** | ✅**완료(2026-06-20)** — `ShadeAdditionalToon` 루프(Forward+클러스터+FP디렉셔널), `_USE_ADD_LIGHTS` 토글·`_AdditionalLightStrength`, `_ADDITIONAL_LIGHT_SHADOWS` | additional light 루프를 셀 양자화로 반영(이동 광원 반응) | P1 |
| L4 | **받는 그림자 톤 셰이핑** | `shadowAttenuation` 곱 | 셀프/캐스트 섀도를 램프·소프트니스로 정형화(딱딱한 2단 그림자), 그림자 색 제어 | **P0** |
| L5 | 환경광/주야 통합 | SH 앰비언트 단순 | 환경 큐브맵/주야 라이트 통합, indirect 색 제어 | P2 |

### B2. 얼굴
| # | 기능 | 현재 | 목표 | P |
|---|---|---|---|---|
| F1 | 코·얼굴 자동 음영 정교화 | SDF 플립 + 수동 헤어마스크 | 거리별 SDF 소프트니스 적응, 코 라인 음영, 표정 atlas 대응 | P1 |
| F2 | **얼굴 위 헤어 그림자 자동 투영**(결정 #7) | 수동 마스크만 | 프록시/깊이 기반 자동 투영 | P2 |

### B3. 헤어
| # | 기능 | 현재 | 목표 | P |
|---|---|---|---|---|
| H1 | Angel Ring 고도화 | 단일 이방성 | shift 맵 기반 1·2차 하이라이트, 지터, 색분리 | P1 |
| H2 | 헤어 전용 tangent 소스 | UV 미분 유도 | 메시 tangent 분리(아웃라인 스무스노멀과 충돌 회피, 결정 #3 메모) | P2 |

### B4. 아웃라인
| # | 기능 | 현재 | 목표 | P |
|---|---|---|---|---|
| O1 | **깊이 인지/스크린스페이스 아웃라인** | Inverted Hull(메시)만 | depth/normal edge 기반 스크린스페이스 외곽선 RendererFeature 병행(내부선·접합부) | P1 |
| O2 | 정점 컬러 폭 제어 | tangent 스무스노멀 + ILM.A | vertex color로 부위별 폭 가중, z-offset 클리핑 방지 | P1 |
| O3 | 거리 LOD 페이드 | width=0 프리셋 | 거리별 폭 자동 페이드/컬링 | P2 |

### B5. 머티리얼 표현
| # | 기능 | 현재 | 목표 | P |
|---|---|---|---|---|
| M1 | **툰 스페큘러** | MatCap만 | GGX 근사 후 단계화(셀 스페큘러), 옷/금속 구분, ILM.B 폭 활용 | **P0** |
| M2 | 디테일/2차 맵 | 없음 | 디테일 albedo/노멀, 2차 컬러 마스크(lilToon식) | P2 |
| M3 | 노멀맵 | URA 미제공·미사용 | 노멀맵 경로(타 캐릭터 대비) | P2 |

### B6. 눈
| # | 기능 | 현재 | 목표 | P |
|---|---|---|---|---|
| E1 | 눈 시차 정교화 | 근사 parallax | 굴절형 parallax, 카메라 정면 고정 하이라이트, 홍채 깊이 | P1 |

### B7. 시스템 / 품질 / 성능
| # | 기능 | 현재 | 목표 | P |
|---|---|---|---|---|
| S1 | **글로벌 제어 RendererFeature** | 개별 글로벌 주입 | NiloToon식 All-in-one RendererFeature(글로벌 파라미터·퍼캐릭터·아웃라인 패스 통합 관리) | **P1** |
| S2 | 거리 기반 품질 페이드 | 없음 | 원거리 기능 자동 축소(HQ↔경량 자연 전환) — 단일 티어 방침 하 비용 완화책 | **P0(리스크 대응)** |
| S3 | HDR/블룸 친화 발광 | 기본 가산 | 블룸 파이프라인 연계, 발광 톤 분리 | P2 |
| S4 | 변형/precision 감사 | 스트리퍼 골격 | allowlist 확정, half precision 감사, 모바일 점검(M5) | P1 |

---

## C. 권장 도전 순서
1. **P0 먼저(룩·리스크 핵심)**: L4(그림자 톤 셰이핑), M1(툰 스페큘러), S2(거리 품질 페이드 — 단일 티어 다수렌더 비용 방어).
2. **P1(NiloToon 차별 기능)**: L1(퍼-캐릭터 라이트)+S1(글로벌 RendererFeature), L2(깊이 림), O1/O2(스크린스페이스·정점폭 아웃라인), L3, H1, F1, E1, S4.
3. **P2(고급·후순위)**: 환경광 통합, 자동 헤어 그림자, 디테일/노멀, 블룸 연계 등.

## D. 주의
- 단일 티어(결정 #15)라 기능이 늘수록 **쿼터뷰 다수 캐릭터 비용**이 커진다 → S2(거리 품질 페이드)와 M5 프로파일링이 안전판.
- NiloToon은 **유료 에셋**: 코드 비참조, 기능 구성·UX·결과 품질만 벤치마크. 자작 HLSL 유지.
- 각 항목 착수 전 NiloToon 공식 문서로 동작·UX 대조 → 본 표의 "목표"를 구체 스펙으로 확정.

---

## E. 구현 현황 (2026-06-18)
> Claude 직접 구현 + Codex 검증 + 헤드리스 CompileCheck(errors=0). 모두 **사용자 Unity 시각/런타임 확인 대기**.

- [x] **L1 퍼-캐릭터 라이트** — `CharacterToonLight`(우선순위/색/세기 오버라이드) + `CharacterToonManager`(중앙 레지스트리, **단일-활성 highest-priority** 모델) → 글로벌 `_CharacterLightDirWS`/`_CharacterLightColor`(linear) 주입. 셰이더 base shading + 툰 스페큘러가 캐릭터 라이트 색 사용. null 키라이트는 기존 메인라이트 폴백 보존(`_useTransformAsDirection` opt-in). **다중 동시 퍼-렌더러는 미구현(향후 MPB/퍼-드로우)**.
- [x] **S1 글로벌 RendererFeature** — `CharacterToonRendererFeature`(ScriptableRendererFeature). `AddRenderPasses`에서 `CharacterToonManager.PushActive()`로 카메라별 글로벌 통합. **사용자가 렌더러 에셋에 1회 추가 필요**(서브에셋).
- [x] **L2 깊이 인지 림** — 셰이더 `_USE_DEPTH_RIM`. 화면공간 깊이 비교로 실루엣 따라 화면상 폭 일정 역광 림. 정면(nVS.xy~0) 억제. DepthTexture 필요.
- [x] **O1 스크린스페이스 아웃라인** — `ScreenSpaceOutline.shader`(깊이/노멀 Roberts 엣지) + RenderGraph 풀스크린 패스(`AddBlitPass`+`cameraColor` 스왑, `requiresIntermediateTexture`, `ConfigureInput(Depth|Normal)`, XR `SETUP_STEREO`). RendererFeature Outline 섹션에서 셰이더 지정+Enabled.
- **메시 Inverted Hull 아웃라인(WP-E)과 병행** — O1은 내부선/접합부 보강용, 기존 메시 외곽선은 유지.
- [x] **프리셋 저장/불러오기 (결정 #14 Phase 2)** — ShaderGUI 상단 Preset 바. Unity `Preset`(.preset)으로 머티리얼 프로퍼티+키워드 일괄 저장/적용(`ApplyMaterialPropertyDrawers`로 키워드 재동기화).

**미구현(차기 후보)**: L3(추가광 셀 반영), L4 심화(그림자 톤 셰이핑 — 일부 WP-A로 충족), L5(환경광 통합), F1/F2(얼굴 심화·자동 헤어그림자), H1(Angel Ring 고도화), O2(정점컬러 폭)/O3(거리 LOD 페이드), M2(디테일맵), E1(눈 굴절), S2(거리 품질 페이드·리스크 대응 P0), S4(precision 감사·M5).
