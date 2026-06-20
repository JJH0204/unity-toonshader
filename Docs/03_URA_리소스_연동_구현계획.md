# 03 — URA 캐릭터 리소스 연동: 기능 리스트업 & 구현계획

> 근거 문서: `01_..._구현계획_최종.md`(규약/룩/결정), `02_마일스톤_태스크분해.md`(M0–M5 상태).
> 대상 리소스: `Assets/Sample/Texture/URA/`, `Assets/Sample/Material/URA/`, `Assets/Sample/FBX/URA/URA.fbx`.
> 작성 목적: 실제 캐릭터(URA) 텍스처를 셰이더에 연동하기 위해 **추가로 필요한 기능을 리스트업**하고, 마일스톤/오케스트레이션 규칙에 맞춘 **구현계획**을 확정한다.
> 현재 셰이더는 M0–M4 본문 대부분 구현 완료 상태. 본 계획은 "스파이크/검증용 placeholder"에서 "실제 캐릭터 1체 구동"으로 가는 **연동(integration) 단계**다.

---

## A. URA 리소스 인벤토리 (실측)

### A.1 파트별 albedo (PSD, 2048급)
| PSD | 용도(추정) | 대응 머티리얼 |
|---|---|---|
| `Base_Head.psd` | 머리/얼굴 albedo | Head / Face |
| `Basic_Body.psd` | 몸통 albedo | Body |
| `Basic_Arm_Leg.psd` | 팔/다리/어깨 albedo | Arm_Leg_Shoulder |
| `Hair.psd` | 머리카락 albedo | Hair |
| `Basic_Eyes.psd` | 눈 albedo | Eyes |

### A.2 얼굴 SDF
| 파일 | 비고 |
|---|---|
| `Head_SDF.psd` | 머리 UV 레이아웃의 얼굴 그림자 SDF (2048) |
| `SDF/A.png … H.png` | 8방향 단계 마스크 (2048). **고전 8-step 얼굴 SDF 베이크 소스** |
| `SDF/sdf.png`, `SDF/Sdf_Channel.png` | A–H를 합성한 **결과 SDF** (2048) — 이미 합본 존재 |

### A.3 마스크 (Mask/ — 파트별 분리)
| 파일 | 의미 |
|---|---|
| `Mask/Base_Head_Matcap.psd` | 머리 MatCap 강도 마스크 |
| `Mask/Basic_Body_Matcap.psd` | 몸통 MatCap 마스크 |
| `Mask/Basic_Arm_Leg_Matcap.psd` | 팔/다리 MatCap 마스크 |
| `Mask/Basic_Body_Emissive.psd` | 몸통 발광 마스크 |
| `Mask/Basic_Arm_Leg_Emissive.psd` | 팔/다리 발광 마스크 |
| `Mask/Hair_Mask.psd` | 머리카락 마스크 (Angel Ring/스페큘러/그림자용) |

### A.4 없는 것 (확인)
- **노멀맵 없음** (Rossi와 달리 URA는 `_N` 없음) → 노멀 매핑 비대상.
- **RampMap 없음** → 파트별 그라데이션 램프는 **별도 저작 필요**(아트/placeholder).
- **패킹된 ILM(RGBA) 없음** → URA 파이프라인은 ILM 단일맵이 아니라 **분리 마스크** 방식. (핵심, §C)

---

## B. 갭 분석 (현재 셰이더 ↔ URA 리소스)

| # | 갭 | 현재 상태 | 영향 |
|---|---|---|---|
| G1 | **머티리얼 전부 `_PART_NONE`** | Body/Face/Hair/Eyes 모두 `_PART_NONE`, `_Part:0` | Face SDF·Angel Ring·SSS·Eye 등 파트 기능이 **하나도 활성화 안 됨** |
| G2 | **Face 머티리얼 BaseMap 오연결** | `Face._BaseMap = Head_SDF.psd` | albedo 자리에 SDF가 들어감. 정상: BaseMap=Base_Head, `_FaceSDF`=Head_SDF |
| G3 | **Emission 기능 부재** | 셰이더에 emission 샘플/CBUFFER 없음 | Body/Arm_Leg Emissive 마스크를 쓸 수 없음 |
| G4 | **MatCap 마스크 소스 불일치** | 셰이더는 `ilm.r`로 matcap 강도 | URA는 분리 Matcap 마스크 제공 → 연결 경로 필요 |
| G5 | **Hair 마스크 미연동** | Angel Ring에 마스크 입력 없음 | `Hair_Mask`로 링/스페큘러 영역 제한 불가 |
| G6 | **마스크/SDF 임포트 sRGB=1 (오류)** | 데이터 마스크가 sRGB로 임포트됨 | 선형 데이터가 감마 왜곡 → 음영/발광 어긋남 |
| G7 | **머티리얼이 BaseMap만 연결** | ILM/Ramp/SDF/MatCap/Eye 슬롯 비어 있음 | 룩 파이프라인 미구성 |
| G8 | **URA.fbx 스무스 노멀 미베이크** | tangent에 스무스 노멀 없음 | 아웃라인이 메시 노멀 폴백(이음새 갈라짐 가능) |

---

## C. 핵심 결정 (구현 전 잠금 필요) — 미해결 결정 레지스터 확장

> `01_계획` 10장의 결정 #1–#10과 별개로, **URA 연동에서 새로 드러난 결정**. 임의 확정 금지 항목.

### 결정 #11 — 분리 마스크 → ILM 통합 전략 (**확정 ✅**)
URA는 Matcap/Emissive/Hair를 **분리 PSD**로 제공하나, 프로젝트 규약은 **ILM RGBA 단일맵**(R=spec/MatCap, G=shadow bias, B=spec width, A=innerline/outline)으로 잠겨 있음("Never repurpose").
- **확정: ILM 패커 + Emission 분리 (하이브리드)** — MatCap/Hair 마스크는 **ILM 채널로 베이크**(규약·SRP Batcher 유지, 샘플러 1개). ILM은 R/G/B/A가 이미 점유 완료라 채널 여유가 없으므로 **Emission만 별도 `_EmissionMap` 신설**.
  - MatCap 강도 → ILM.R, Hair 마스크 → ILM.A(또는 inner-line과 공존 정책은 패커에서 결정).
  - G(shadow bias)는 마스크 미제공 → 중립 0.5 또는 별도 저작.
- (기각) 전부 분리 샘플러: 변형·샘플러 증가·규약 이중화. (기각) 전부 ILM 패킹: 채널 규약 위반.

### 결정 #12 — 얼굴 SDF 소스 (**확정 ✅**)
- **확정: 합본 즉시 사용** — `SDF/sdf.png`(또는 `Head_SDF`)를 `_FaceSDF`에 직접 연결해 즉시 구동.
- A–H 8-step → 단일 SDF 합성 베이커 에디터 툴은 **백로그**(타 캐릭터 확장 시).

### 결정 #13 — Ramp 저작 책임
RampMap 미제공. placeholder 2단 하드컷 램프를 **툴로 생성**할지, 아트가 저작할지. → 연동 단계는 **placeholder 램프 생성기/기본 그라데이션**으로 진행, 최종은 아트.

### 결정 #14 — 머티리얼 UX 방향: lilToon 사용성 추종 (**확정 ✅**)
작업자(아티스트)들이 lilToon으로 포트폴리오 경험이 있어 가장 익숙함 → 자작 CharacterToon의 **머티리얼 인스펙터/워크플로를 lilToon 사용성에 맞춤**. (메모리 [[liltoon-ux-direction]])
- **확정 채택**: ① **카테고리 폴드아웃 커스텀 `ShaderGUI`** ② **Simple/Advanced 모드** ③ **프리셋 저장/불러오기**. (KR/EN 현지화 비채택)
- **시점**: **지금부터 — 기본 ShaderGUI 먼저.** 진행 중인 와이어링을 도와 ILM-on-without-map 같은 실수를 UI가 방지.
- **제약(CLAUDE.md)**: lilToon은 **참고용만**(자작 HLSL·런타임 비의존). 코드 복사 금지(MIT, 채택 시 표기). UX 패턴만 미러링, ShaderGUI 자작. "실사용 조합=material preset" 방침과 정합.
- 목표: 리소스 적용 방식~결과물이 lilToon과 유사 + 추가로 **NiloToon 급** 도전(결정 #15 품질 상향과 연계).

### 결정 #15 — 단일 티어(로비 품질) + NiloToon 급 목표 (**확정 ✅ — 바인딩 스펙 개정 필요**)
**인게임/로비 품질을 분리하지 않고, 로비(고품질) 기준 단일 셰이더로 구현한다.** 추가로 NiloToon 급 품질을 목표로 도전. (메모리 [[single-tier-lobby-quality]])
- ✅ **스펙 개정 완료(2026-06-17)**: `CLAUDE.md`(프로젝트 소개·환경·셰이더 아키텍처·마일스톤)와 `01_계획서`(상단 개정 배너 + 목적·방향·§1.1/§1.2·키워드·§7·M4 정의부)를 단일 티어로 개정. 본문 잔여 2티어 표현은 01 상단 배너가 전역 오버라이드.
- **셰이더 함의**: `LOBBY_HQ`로 게이팅하던 기능(MatCap, Hair Angel Ring, Skin SSS, Eye Parallax, smoothstep SDF, rim boost)을 **상시 활성화**. `LOBBY_HQ` 키워드/게이트 **제거 가능**(변형 수 감소). (별도 셰이더 리팩터 태스크)
- **리스크**: 원래 인게임(쿼터뷰 다수 캐릭터) 비용. 로비급 단가가 다수 렌더에 부담 가능 → 룩 확정 후 **M5 프로파일링에서 재점검**.
- **NiloToon 급 갭(후속 상세화)**: 고품질 아웃라인(거리/노멀 인지), 정교한 Face/헤어 그림자, 스크린스페이스 림/스페큘러, 퍼-캐릭터 라이트 컨트롤, 환경광 통합 등 — 별도 기능 갭 리스트로 정리 예정.

---

## D. 필요 기능 리스트 (우선순위순)

1. **[P0] PSD 임포트 설정 일괄 정리** — albedo=sRGB on, 마스크/SDF/Ramp=linear(sRGB off), 압축/밉맵/래핑 정책. (G6)
2. **[P0] 머티리얼 파트/맵 와이어링** — 각 머티리얼 `_Part` 키워드 설정, BaseMap·ILM·Ramp·SDF·MatCap·Eye 슬롯 연결, Face 오연결 수정. (G1,G2,G7)
3. **[P0] Emission 기능 신설** — `_EmissionMap`+`_EmissionColor`+`_USE_EMISSION` 키워드, CBUFFER/샘플러 추가, frag 가산. (G3)
4. **[P1] 분리 마스크 → ILM 패커 에디터 툴** — 결정 #11(A/C) 확정 후. MatCap/Hair 마스크를 ILM 채널로 베이크. (G4,G5)
5. **[P1] Hair 마스크 → Angel Ring 연동** — ILM 채널(또는 분리) 마스크로 링 영역 제한. (G5)
6. **[P1] URA.fbx 스무스 노멀 베이크** — 기존 `SmoothNormalBaker`로 tangent 베이크 메시 생성. (G8)
7. **[P1] 파트별 placeholder Ramp 생성** — 2단 하드컷/소프트 그라데이션. (결정 #13)
8. **[P2] 머티리얼 프리셋 확정** — 인게임 티어(LOBBY_HQ off) / 로비 티어(LOBBY_HQ on) 2세트. (M4 T4-6, M5)
9. **[P2] URA 1체 쇼케이스 씬 구성 + 검증 툴 실측** — Face SDF Validator, 아웃라인, MatCap 룩 확인. (M2/M3/M4 게이트)
10. **[P3] (백로그) A–H 8-step SDF 베이커** — 결정 #12.

---

## E. 구현계획 (단계별 · 오케스트레이션 역할 명시)

> 역할: **Claude**=계획/통합/수정, **Copilot**=구현(기본 로컬), **Codex**=검증/교차검토.
> 셰이더 본문/툴 변경은 다파일·코어로직 → 일부는 GitHub Gate 대상이나, 본 저장소는 비-git이므로 **로컬 경로(`copilot-local.sh`) 기본**. PR은 git 전환 시 재검토.

### 단계 0 — 결정 잠금 (Claude + 사용자)
- 결정 #11/#12/#13 확정. (#11이 이후 단계의 분기점)
- 산출: 본 문서 §C 업데이트 + `01_계획` 10장 동기화.

### 단계 1 — [P0] PSD 임포트 설정 정리 (Copilot 로컬 → Codex 검증)
- 변경 파일: `Assets/Sample/Texture/URA/**/*.psd.meta` (또는 `AssetPostprocessor` 스크립트 `URATextureImportPostprocessor.cs` 신설).
- 내용: albedo sRGB on; `Mask/*`·`SDF/*`·Ramp sRGB **off**; 압축=고품질/BC7, 밉맵 정책, wrap=Clamp(SDF/Ramp).
- 테스트 영향: 임포트 후 색/음영 정상화. 시각 확인(에디터).
- 롤백: postprocessor 비활성 또는 .meta 복원.

### 단계 2 — [P0] Emission 기능 셰이더 신설 (Copilot 로컬 → Codex 검증 → Claude 통합)
- 변경 파일: `ToonInput.hlsl`(`_EmissionMap` 샘플러 + CBUFFER `_EmissionColor`), `CharacterToon.shader`(`_USE_EMISSION` shader_feature_local, frag 가산 `shaded += emission`).
- **SRP Batcher 주의**: CBUFFER 레이아웃 전 패스 동일 유지(`_EmissionColor` 추가 위치 고정).
- 테스트 영향: 기존 머티리얼 영향 없음(키워드 off 시 no-op). 변형 수 +1.
- 롤백: 키워드/블록 제거.

### 단계 3 — [P1] 분기: 결정 #11 결과
- **A/C안(권장)**: ILM 패커 에디터 툴 `ILMPackerWindow.cs`(Window/CharacterToon/ILM Packer) 신설. 입력=분리 마스크, 출력=ILM RGBA PNG. (Copilot 로컬 → Codex 검증)
  - 변경: `Assets/CharacterToon/Editor/ILMPackerWindow.cs`, `*.Editor.asmdef`(기존).
- **B안**: 셰이더에 `_MatCapMask` 등 분리 샘플러 + 키워드 추가(ToonInput/shader).
- Hair 마스크 → Angel Ring: 선택안에 맞춰 `_PART_HAIR` 블록에서 마스크 곱.

### 단계 4 — [P1] URA.fbx 스무스 노멀 베이크 (Claude 실행/사용자 에디터)
- 기존 `SmoothNormalBaker` 사용 → tangent 베이크 메시 에셋 생성. (에디터 GUI)
- 테스트 영향: 아웃라인 이음새 개선. 롤백: 원본 FBX 메시 사용.

### 단계 5 — [P1] placeholder Ramp 생성 (Copilot 로컬)
- 파트별 2단 하드컷 + 소프트 1종, `Assets/CharacterToon/Textures/Ramp_*.png` 또는 생성 툴.

### 단계 6 — [P0/P2] 머티리얼 와이어링 & 프리셋 (Claude/사용자 에디터)
- 각 URA 머티리얼: `_Part` 설정(Face/Hair/Skin/Cloth), BaseMap·ILM·Ramp·SDF·MatCap·Eye·Emission 슬롯 연결, **Face의 BaseMap 오연결 수정**.
- 인게임/로비 2개 프리셋 세트(LOBBY_HQ off/on, MatCap·AngelRing·SSS·Eye 키워드).
- 테스트 영향: 룩 정상화. 롤백: 머티리얼 복원.

### 단계 7 — [P2] 쇼케이스 씬 + 검증 (Claude/사용자 에디터)
- URA 1체 배치, CharacterToonLight/Face 컴포넌트 연결, Face SDF Validator·아웃라인·MatCap 룩 실측 → M2/M3/M4 게이트 판정.

---

## F. 변경 파일 요약 / 테스트 영향 / 롤백 (계획 규약)

| 단계 | 변경/신규 파일 | 테스트 영향 | 롤백 |
|---|---|---|---|
| 1 | `*.psd.meta` 또는 `URATextureImportPostprocessor.cs` | 색·음영 정상화(시각) | meta 복원/툴 off |
| 2 | `ToonInput.hlsl`, `CharacterToon.shader` | 변형 +1, off 시 no-op | 키워드/블록 제거 |
| 3 | `Editor/ILMPackerWindow.cs` (A) / 셰이더 샘플러(B) | 마스크 룩 활성 | 툴/샘플러 제거 |
| 4 | (에셋) URA 스무스노멀 메시 | 아웃라인 개선 | 원본 메시 |
| 5 | `Textures/Ramp_*.png` | 음영 단계 | placeholder 제거 |
| 6 | URA `*.mat` | 전체 룩 구성 | 머티리얼 복원 |
| 7 | 쇼케이스 씬 | 게이트 판정 | 씬 폐기 |

---

## G. 마일스톤 매핑
- 단계 1–2: M1 연동 보강 + M4 Emission(신규 결정 #11/#14).
- 단계 3–5: M4 로비 HQ 실데이터 연동 / M3 아웃라인 실메시.
- 단계 6–7: M2/M3/M4 **게이트 실측**(현재 "메커니즘 통과", "실데이터 룩 미판정" 상태를 닫음), M5 프리셋 진입.

---

## H. 구현 진행 현황 (코드 레벨 — 에디터 실측은 별도)
> 흐름: Copilot 구현 → Codex 검증 → Claude 통합/수정. 에디터 컴파일·시각 확인은 Unity에서 별도.

- [x] **단계 2 — Emission 셰이더** (`ToonInput.hlsl`, `CharacterToon.shader`): `_EmissionMap`+`_EmissionColor`(HDR)+`_USE_EMISSION`, ForwardToon 한정, off 시 no-op. **Codex가 `_USE_EYE_PARALLAX` 블록 `#endif` 누락(전처리기 불균형) 지적 → 수정**. `#if`/`#endif` 14/14 균형 확인.
- [x] **단계 3 — ILM 패커** (`Editor/ILMPackerWindow.cs`, 메뉴 Window/CharacterToon/ILM Packer): 분리 마스크→ILM RGBA, Read/Write 미요구(Blit), 결과 강제 Linear 임포트, 선택적 머티리얼 할당. **Codex 지적 반영**: sRGB 소스 감지 경고, `result`/`temp` 누수 finally 정리, 데드코드 제거, 경로 검증.
- [x] **단계 1 — PSD 임포트 정책** (`Editor/URATextureImportPostprocessor.cs`): `Assets/Sample/Texture/URA/` 한정, albedo=sRGB / Mask·SDF=linear(SDF는 Clamp+CompressedHQ), 메뉴 "Reimport URA Textures"로 기존 자산 일괄 적용. **Codex 전체 PASS**, `ToLowerInvariant` 하드닝 반영.
- [~] **단계 6 — 머티리얼 와이어링**: Unity가 열린 채 사용자가 일부 와이어링 진행 중 → 직접 .mat 편집 대신 **Inspector 체크리스트 제공**(아래 §I). 파트 정리 확정: Eyes→None, Head→Skin, Arm_Leg Emission 연결.
- [x] **lilToon식 ShaderGUI Phase 1** (`Editor/CharacterToonShaderGUI.cs` + 셰이더 `CustomEditor` 1줄): 카테고리 폴드아웃 + Simple/Advanced 모드(EditorPrefs 영속화), 모든 프로퍼티 `ShaderProperty`로 렌더(키워드/HDR 보존), 누락 프로퍼티 안전 스킵, 단일 티어(티어 토글 없음). **Codex 전 항목 PASS**. (결정 #14·#15) — Copilot 샌드박스 쓰기 실패로 Claude가 직접 작성. **Phase 2(프리셋 저장/불러오기) 미구현**.
- [x] **ShaderGUI 용어 lilToon화 + 한글 툴팁**: 41개 프로퍼티 라벨을 lilToon 기준 영문 용어로, 마우스 롤오버 시 한글 툴팁(`Dictionary<string,GUIContent>`). 섹션명도 lilToon식(Main Color/Shadow/Rim Light/…/Rendering). **Codex 전 항목 PASS**(41키 셰이더와 일치·중복/오타 없음, 키워드 보존). 사용자 편의성 목적.
- [x] **NiloToon 급 기능 갭 로드맵** 작성: `Docs/04_NiloToon급_기능_갭_로드맵.md` (현재 기능 스냅샷 + 카테고리별 갭 P0~P2 + 도전 순서). 결정 #15 품질 목표 구체화.
- [x] **LOBBY_HQ 키워드 제거 리팩터** (`CharacterToon.shader`): `#pragma ... LOBBY_HQ` 삭제, Face SDF 항상 smoothstep, rim boost·MatCap·Angel Ring·Skin SSS의 `LOBBY_HQ` 래퍼 제거(내부 `_USE_*`/`_PART_*` 가드 유지). 변형 수 절반(로컬 640→320). 스트리퍼 주석 갱신. **Codex 전 항목 PASS**(전처리기 균형 12/12, CBUFFER 불변, SRP Batcher 유지). (결정 #15)
- [ ] **단계 4·5·7**: 스무스 노멀 베이크 / placeholder Ramp / 쇼케이스 씬 — 에디터 작업.
- ⚠️ **선행 작업**: Unity 에디터에서 ① 컴파일 통과 확인 ② "Reimport URA Textures" 실행(마스크 sRGB→linear) → 이후 ILM 패커 사용.

---

## I. 단계 6 — 머티리얼 Inspector 와이어링 체크리스트
> Unity가 열려 있어 디스크 .mat 직접 편집은 충돌 위험 → 아래를 Inspector에서 적용한다. 모든 머티리얼 셰이더 = `CharacterToon/Character`.
> 슬롯명은 Inspector 헤더 기준. ✓=이미 정상, ⚠️=수정 필요, ＋=신규 연결.

**Face** (얼굴 SDF)
- Base Map: ⚠️ `Head_SDF` → **`Base_Head`** 로 교체 (현재 albedo 자리에 SDF가 들어가 있음)
- Face SDF: `Sdf_Channel` (현재) — ⚠️ Face 메시 UV와 일치 검증 필요. **Face SDF Validator + Use ILM 아래 `_DEBUG_FACELIT` ON** 으로 확인(라이트 회전 시 경계가 공간 이동하면 정상). 안 맞으면 `Head_SDF`(머리 UV) 또는 `sdf.png`로 교체.
- Part Type: **Face** (유지) / Use ILM Map: **OFF**

**Head** (머리 스킨)
- Base Map: `Base_Head` ✓
- Part Type: ⚠️ Face → **Skin** (Head는 머리 스킨, SDF는 Face 머티리얼 전용)
- Use ILM Map: OFF

**Body**
- Base Map: `Basic_Body` ✓ / Emission Map: `Basic_Body_Emissive` ✓ / Use Emission: ON ✓ / _UseEmission=1 ✓
- Emission Color: HDR(≈14.9) — ⚠️ 매우 강함. 실제 룩에서 과발광 여부 확인 후 아트 튜닝
- Part Type: Cloth (유지)

**Arm_Leg_Shoulder**
- Base Map: `Basic_Arm_Leg` ✓
- Emission Map: ＋ **`Basic_Arm_Leg_Emissive`** 연결 / **Use Emission: ON** / Emission Color: Body와 동일 의도로 설정(HDR)
- Part Type: Cloth (유지)

**Hair**
- Base Map: `Hair` ✓
- Ramp Map: ⚠️ **깨진 참조(b8c96…) 제거 → None** (단계 5 램프 생성 후 연결. 없으면 white 폴백)
- Use ILM Map: ⚠️ **OFF** (현재 ON인데 ILM 맵 없음 → black 샘플로 shadow bias/outline 오작동. ILM 패킹 후 ON+할당)
- Part Type: Hair (유지)

**Eyes**
- Base Map: `Basic_Eyes` ✓
- Part Type: ⚠️ Face → **None** (눈은 얼굴 SDF 대상 아님)
- Eye Map: 시차(EyeParallax) 쓸 때만 `Basic_Eyes` + Use Eye Parallax 키워드; 안 쓰면 비움
- Use ILM Map: OFF

**보류(별도 단계 의존)**
- **ILM**: ILM 패커 실행 → Body/Head/Arm Matcap 마스크 + Hair_Mask → ILM 생성 후 각 머티리얼 ILM Map 할당 + Use ILM ON. 단 **MatCap 스피어 텍스처가 URA에 없음** → MatCap 룩은 스피어 확보 전까지 보류.
- **Ramp**(단계 5): 파트별 램프 생성 후 Ramp Map 연결.
- **로비 프리셋**(단계 8): 위 인게임 세트 복제 + `LOBBY_HQ` 키워드 ON + MatCap/AngelRing/SSS/EyeParallax 키워드.
