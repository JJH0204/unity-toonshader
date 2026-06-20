using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Presets;

namespace CharacterToon.Editor
{
    /// <summary>
    /// lilToon-style custom material inspector (ShaderGUI) for the CharacterToon shader.
    /// PHASE 1: 카테고리 폴드아웃 섹션 + Simple/Advanced 모드. (결정 #14)
    ///
    /// 용어는 작업자 친숙도를 위해 lilToon 기준 영문 라벨을 쓰고, 마우스 롤오버 시 한글 툴팁을 보여준다.
    /// (라벨/툴팁은 _labels 딕셔너리. 프로퍼티 식별자(_BaseMap 등)는 그대로, 표시만 바뀐다.)
    ///
    /// 단일 티어(로비 품질) 방침(결정 #15)에 따라 품질-티어 토글(LOBBY_HQ)은 UI에 두지 않는다.
    /// MatCap / Angel Ring / Skin SSS / Eyes 는 단일 고품질 셰이더의 일부로 Advanced 모드에 직접 노출된다.
    ///
    /// 모든 프로퍼티는 materialEditor.ShaderProperty 로 렌더 → [Toggle(_USE_*)], [KeywordEnum](_Part),
    /// [HDR] 등 어트리뷰트 동작과 키워드 동기화가 자동 보존된다.
    /// 누락된 프로퍼티는 FindProperty(..., false) 가 null 을 반환 → 조용히 스킵(셰이더 변경에도 미예외).
    ///
    /// TODO Phase 2: 프리셋 저장/불러오기 (결정 #14 ③).
    /// </summary>
    public class CharacterToonShaderGUI : ShaderGUI
    {
        private const string ShaderModePrefKey = "CharacterToon.ShaderGUI.Mode";
        private const string FoldoutPrefKeyPrefix = "CharacterToon.ShaderGUI.Foldout.";

        // 폴드아웃 영속화용 섹션 ID
        private const string SectionMain = "Main";
        private const string SectionNormal = "Normal";
        private const string SectionShadow = "Shadow";
        private const string SectionRim = "Rim";
        private const string SectionAddLights = "AddLights";
        private const string SectionOutline = "Outline";
        private const string SectionPBR = "PBR";
        private const string SectionEmission = "Emission";
        private const string SectionMatCap = "MatCap";
        private const string SectionHair = "Hair";
        private const string SectionSSS = "SSS";
        private const string SectionEye = "Eye";
        private const string SectionRendering = "Rendering";

        private enum ViewMode { Simple = 0, Advanced = 1 }

        // lilToon 기준 영문 라벨 + 한글 툴팁(마우스 롤오버). 키 = 프로퍼티 식별자.
        private static readonly Dictionary<string, GUIContent> _labels = new Dictionary<string, GUIContent>
        {
            // Surface
            { "_Surface",            new GUIContent("Rendering Mode",       "렌더링 모드. Opaque=불투명, Transparent=반투명(알파 블렌드, ZWrite off). 부위별로 다른 모드 가능(눈 등은 Opaque 유지).") },
            // Main Color
            { "_BaseMap",            new GUIContent("Main Texture",        "메인(베이스) albedo 텍스처. 컬러이므로 sRGB로 임포트.") },
            { "_BaseColor",          new GUIContent("Main Color",          "메인 색조(틴트). 텍스처에 곱해진다.") },
            { "_Part",               new GUIContent("Material Type",       "부위 타입. Face=얼굴(SDF 음영), Hair=머리(Angel Ring), Skin=피부(SSS), Cloth=옷.") },
            // Normal Map (WP-B)
            { "_UseNormalMap",       new GUIContent("Use Normal Map",      "노멀맵 사용. 끄면 메시 보간 노멀 그대로(섭동 없음).") },
            { "_BumpMap",            new GUIContent("Normal Map",          "탄젠트 공간 노멀맵. 데이터이므로 Normal map 타입으로 임포트(선형).") },
            { "_BumpScale",          new GUIContent("Normal Scale",        "노멀 섭동 세기. 라이팅·MatCap·림에 반영된다.") },
            // Shadow
            { "_RampMap",            new GUIContent("Shadow Ramp",         "음영 그라데이션 LUT. 가로(U)=명암, 행(V)=부위별 음영 톤.") },
            { "_RampRow",            new GUIContent("Ramp Row (V)",        "사용할 램프 행. 부위별 음영 톤 선택.") },
            { "_ShadowOffsetScale",  new GUIContent("Shadow Border Offset","ILM G채널로 음영 진입 경계를 당기거나 미는 양.") },
            { "_ShadeFloor",         new GUIContent("Shadow Floor",        "최소 밝기 하한. 저조도에서 얼굴이 묻히지 않게 한다.") },
            { "_AmbientStrength",    new GUIContent("Ambient (SH)",        "환경광(Spherical Harmonics) 세기.") },
            { "_AOStrength",         new GUIContent("SSAO Strength",       "화면공간 AO 영향도(0=무시, 1=URP 표준). URP 렌더러에 SSAO(ScreenSpaceAmbientOcclusion) 기능이 켜져 있어야 효과. 과하면 셀 음영이 뭉개질 수 있어 낮춘다.") },
            { "_UseILM",             new GUIContent("Use ILM Map",         "ILM 마스크 사용. 끄면 중립값(R0/G0.5/B0.5/A0)으로 동작(모든 ILM 효과 무효). 데이터 맵이므로 Linear 임포트.") },
            { "_ILMMap",             new GUIContent("ILM Map (RGBA)",      "4-in-1 마스크 패킹(실제 동작): R=MatCap 마스크 강도, G=음영경계 바이어스(0.5중립), B=툰 스페큘러 폭(PBR 시), A=외곽선 억제(1=억제). ※보조램프/내부선은 미구현. 정의=Docs/08.") },
            // 파라메트릭 1·2차 그림자 밴드 (결정 #17)
            { "_ShadowColor",        new GUIContent("Shadow Color 1st",    "1차 그림자 색(베이스 컬러에 곱해진다).") },
            { "_ShadowBorder",       new GUIContent("Shadow Border 1st",   "1차 그림자가 시작되는 명암 경계 위치(=빛↔그림자 경계).") },
            { "_ShadowBlur",         new GUIContent("Shadow Blur 1st",     "1차 그림자 경계의 부드러움(블러).") },
            { "_Shadow2ndColor",     new GUIContent("Shadow Color 2nd",    "2차(심부) 그림자 색.") },
            { "_Shadow2ndBorder",    new GUIContent("Shadow Border 2nd",   "2차 그림자 경계 위치. 보통 1차보다 어두운 쪽(낮은 값).") },
            { "_Shadow2ndBlur",      new GUIContent("Shadow Blur 2nd",     "2차 그림자 경계의 부드러움.") },
            { "_ShadowStrength",     new GUIContent("Shadow Strength",     "그림자 전체 적용 강도/범위. 0이면 그림자 없음.") },
            { "_ReceiveShadowStrength",new GUIContent("Receive Cast Shadow","받는 캐스트 그림자(메인 라이트 섀도)가 음영을 어둡게 하는 정도. 운용: 얼굴 OFF(0)/바디 ON(1).") },
            { "_ShadowMaskType",     new GUIContent("Shadow Mask Type",    "음영 마스크 방식. SDF=얼굴 거리장 플립샘플(얼굴 전용), Strength=일반 half-Lambert 음영. 얼굴이 아니면 항상 Strength로 동작.") },
            { "_ShadowMaskTex",      new GUIContent("Shadow Mask (R)",     "일반 그림자 억제 마스크(R채널). 0인 영역은 그림자가 안 진다(눈동자·눈 흰자 등). 미할당(white)=전체 적용.") },
            { "_UseRamp",            new GUIContent("Use Ramp LUT",        "켜면 파라메트릭 밴드 대신 RampMap LUT로 음영을 계산(고급/특수 톤).") },
            // Rim Light
            { "_UseRim",             new GUIContent("Use Rim Light",       "림 라이트(역광 테두리) 사용. 끄면 연산이 컴파일 단계에서 제거된다(Depth Rim은 별도 토글).") },
            { "_RimColor",           new GUIContent("Rim Color",           "림 라이트(역광 테두리) 색.") },
            { "_RimThreshold",       new GUIContent("Rim Border",          "림이 시작되는 프레넬 임계값. 클수록 가장자리에만.") },
            { "_RimSoftness",        new GUIContent("Rim Blur",            "림 경계의 부드러움.") },
            { "_RimIntensity",       new GUIContent("Rim Intensity",       "림 세기.") },
            { "_RimInteractionBoost",new GUIContent("Rim Interaction Boost","근접/포커스 등 상호작용 시 림을 올리는 배수.") },
            // Depth Rim (L2)
            { "_UseDepthRim",        new GUIContent("Use Depth Rim",       "화면공간 깊이 기반 역광 림(실루엣 따라). 카메라 거리 무관 굵기. DepthTexture 필요.") },
            { "_DepthRimColor",      new GUIContent("Depth Rim Color",     "깊이 림 색.") },
            { "_DepthRimWidth",      new GUIContent("Depth Rim Width (px)","림 폭(픽셀). 화면상 일정.") },
            { "_DepthRimThreshold",  new GUIContent("Depth Rim Threshold", "실루엣으로 판정할 깊이 점프 임계값.") },
            { "_DepthRimIntensity",  new GUIContent("Depth Rim Intensity", "깊이 림 세기.") },
            // Additional Lights (L3)
            { "_UseAddLights",       new GUIContent("Use Additional Lights","씬의 부가광(point/spot)이 캐릭터에 셀 음영을 주게 한다. 끄면 컴파일 제거(키라이트만). 원거리는 S2로 별도 절감.") },
            { "_AdditionalLightStrength",new GUIContent("Additional Light Strength","부가광 가산 세기. 과발광 시 낮춘다.") },
            // Outline
            { "_UseOutline",         new GUIContent("Use Outline",         "외곽선(인버티드 헐) 패스 사용. 끄면 외곽선 패스가 정점 단계에서 클립되어 그려지지 않는다.") },
            { "_OutlineColor",       new GUIContent("Outline Color",       "외곽선 색(외곽선 컬러맵에 곱해진다).") },
            { "_OutlineMap",         new GUIContent("Outline Color Map",   "외곽선 색 텍스처(요구 #6). 기본 white=Outline Color 그대로.") },
            { "_OutlineMask",        new GUIContent("Outline Mask (R)",    "외곽선 전용 마스크(요구 #6). R=폭 가중, 0이면 해당 부위 외곽선 제거.") },
            { "_OutlineWidth",       new GUIContent("Outline Width",       "외곽선 두께. 0이면 비활성. 기본은 거리/FOV 무관 화면상 두께 일정.") },
            { "_OutlineDistanceFade",new GUIContent("Distance Fade",       "원거리 굵기 감쇠(요구 #7). 0=화면일정, 1=멀수록 얇게.") },
            { "_OutlineFadeStart",   new GUIContent("Fade Start Dist",     "거리 페이드 시작 거리. 이 거리 너머부터 외곽선이 얇아진다.") },
            { "_OutlineDepthOffset", new GUIContent("Outline Depth Offset","외곽선 깊이 오프셋(겹침/클리핑 방지).") },
            // PBR (WP-D)
            // 4-1: 단일 통합 툰 스페큘러 모델(별도 URP Specular 워크플로 아님). 컨트롤 역할 명확화.
            //   Tint/Step = 하이라이트 스타일(색/셀 경계), Metallic/Smoothness = 재질(반사 틴트/날카로움+환경반사).
            //   MatCap과의 차이: PBR 스페큘러는 "라이트 방향에 반응"(빛 움직이면 하이라이트 이동), MatCap은 뷰 고정 아트지정.
            { "_UsePBR",             new GUIContent("Use PBR Spec/Reflect","라이트 반응형 툰 스페큘러 + 환경 리플렉션. 끄면 가산 없음. (뷰 고정 스타일 하이라이트는 MatCap 사용 — 둘은 별개 용도.)") },
            { "_SpecularTint",       new GUIContent("Highlight Tint",      "[하이라이트 스타일] 툰 스페큘러 하이라이트 색. Metallic이 높으면 albedo 색으로도 틴트되어 곱해진다.") },
            { "_SpecularStep",       new GUIContent("Highlight Edge",      "[하이라이트 스타일] 셀 경계 부드러움(작을수록 하드한 툰 하이라이트). 크기는 Smoothness가 결정.") },
            { "_MetallicGlossMap",   new GUIContent("Metallic/Smoothness", "[재질] R=메탈릭, A=스무스니스 맵(마스크). 데이터이므로 선형 임포트. 미할당 시 아래 스칼라값 사용.") },
            { "_Metallic",           new GUIContent("Metallic",            "[재질] 금속성. 높을수록 하이라이트가 albedo 색으로 틴트되고 환경 반사가 강해진다.") },
            { "_Smoothness",         new GUIContent("Smoothness",          "[재질] 매끄러움. 하이라이트 크기/날카로움(좁을수록 매끈) + 환경 반사 선명도를 함께 결정.") },
            { "_ReflectionStrength", new GUIContent("Reflection Strength", "[재질] 환경(reflection probe) 반사 세기. Metallic에도 비례.") },
            // Emission
            { "_UseEmission",        new GUIContent("Use Emission",        "발광 사용. 끄면 가산 없음.") },
            { "_EmissionMap",        new GUIContent("Emission Map",        "발광 마스크/색 맵. 데이터이므로 선형(linear) 임포트.") },
            { "_EmissionColor",      new GUIContent("Emission Color (HDR)","발광 색·세기(HDR). 맵에 곱해진다.") },
            // Face Shadow (SDF)
            { "_FaceSDF",            new GUIContent("Face SDF",            "얼굴 SDF(거리장). 좌우 플립 샘플링으로 빛 방향별 얼굴 음영을 만든다. (마스크 타입=SDF일 때 사용)") },
            { "_SDFSoftness",        new GUIContent("SDF Blur",            "SDF 음영 경계의 부드러움(smoothstep 폭).") },
            { "_UseHairShadow",      new GUIContent("Use Hair Shadow",     "얼굴 위에 드리우는 머리카락 그림자(마스크) 사용. 끄면 컴파일 제거.") },
            { "_HairShadowMask",     new GUIContent("Hair Shadow Mask",    "얼굴 위에 드리우는 머리카락 그림자 마스크.") },
            { "_HairShadowStrength", new GUIContent("Hair Shadow Strength","머리카락 그림자 세기.") },
            { "_DebugFaceLit",       new GUIContent("DEBUG: Face Lit",     "진단용 흑백 출력(빌드에서 제거됨). 라이트 회전 시 경계 이동으로 SDF 정상 여부 확인.") },
            // MatCap
            { "_UseMatCap",          new GUIContent("Use MatCap",          "MatCap(뷰공간 구면 반사) 사용. 끄면 가산 없음.") },
            { "_MatCap",             new GUIContent("MatCap",              "뷰공간 구면 반사 텍스처(스페큘러/금속 느낌). 가산(발광형) 블렌드.") },
            { "_MatCapStrength",     new GUIContent("MatCap Strength",     "MatCap 세기.") },
            { "_MatCapNormalStrength",new GUIContent("Normal Influence",   "노말이 MatCap UV에 미치는 영향도. 0=지오메트릭(매끈한 클래식 MatCap, 시점 흔들림 없음), 1=노말맵 디테일(뷰공간 특성상 시점따라 흔들림). MatCap1/2 공통. 흔들림이 어색하면 낮춘다.") },
            { "_UseMatCapMask",      new GUIContent("Use Separate Mask",   "별도 MatCap 마스크 사용(결정 #16). 끄면 ILM.R(ILM 사용 시)·아니면 전체(1)로 폴백.") },
            { "_MatCapMask",         new GUIContent("MatCap Mask (R)",     "MatCap 적용 마스크(R채널). ILM.R과 분리된 전용 마스크.") },
            { "_UseMatCap2",         new GUIContent("Use Second MatCap",   "두 번째 MatCap 사용(한 머티리얼에 MatCap 2개). 끄면 컴파일 제거.") },
            { "_MatCap2",            new GUIContent("Second MatCap",       "두 번째 뷰공간 구면 반사 텍스처.") },
            { "_MatCap2Strength",    new GUIContent("Second Strength",     "두 번째 MatCap 세기(Add는 가산량, Multiply는 곱 영향도).") },
            { "_MatCap2Blend",       new GUIContent("Second Blend",        "두 번째 MatCap 블렌드. Add=가산(하이라이트형), Multiply=곱(음영/AO형).") },
            { "_UseMatCap2Mask",     new GUIContent("Use Second Mask",     "두 번째 MatCap 전용 마스크 사용. 끄면 ILM.R(ILM 사용 시)·아니면 전체(1) 폴백.") },
            { "_MatCap2Mask",        new GUIContent("Second Mask (R)",     "두 번째 MatCap 적용 마스크(R채널).") },
            // Hair (Angel Ring)
            { "_UseAngelRing",       new GUIContent("Use Angel Ring",      "머리카락 천사고리(이방성 하이라이트) 사용. Part=Hair 에서만 동작. 끄면 컴파일 제거.") },
            { "_AngelRingColor",     new GUIContent("Angel Ring Color",    "머리카락 천사고리(이방성 하이라이트) 색.") },
            { "_AngelRingIntensity", new GUIContent("Angel Ring Intensity","천사고리 세기.") },
            { "_AngelRingPower",     new GUIContent("Angel Ring Sharpness","천사고리의 날카로움(지수). 클수록 가늘다.") },
            { "_AngelRingAngle",     new GUIContent("Angel Ring Angle",    "링 방향 회전(도). 링이 세로로 나오면 90° 부근으로 돌려 가로로 맞춘다.") },
            { "_AngelRingMask",      new GUIContent("Angel Ring Mask (R)", "결 마스크(R). 머리 결 따라 세기 조절 + 꼭대기/바닥 등 안 보일 영역 0으로 제외. 미할당(white)=전체.") },
            // SSS (Skin)
            { "_UseSSS",             new GUIContent("Use Skin SSS (optional)","[deprecate, 기본 OFF] 피부 표면하산란 근사(터미네이터 붉은 가산 밴드). 하드 셀에선 잘 안 읽힘 → 그림자 밴드 색(Shadow Color)으로 대체 권장. Part=Skin 전용. Docs/06 §4-4.") },
            { "_SkinSSSColor",       new GUIContent("SSS Color",           "피부 표면하산란 근사 색(보통 붉은 톤).") },
            { "_SkinSSSCenter",      new GUIContent("SSS Border Center",   "SSS가 강조될 음영 경계 위치.") },
            { "_SkinSSSWidth",       new GUIContent("SSS Border Width",    "SSS 경계 폭.") },
            { "_SkinSSSStrength",    new GUIContent("SSS Strength",        "SSS 세기.") },
            // Eye
            { "_UseEyeParallax",     new GUIContent("Use Eye Parallax",    "눈 시차 오버레이 사용. 끄면 컴파일 제거. (전체 홍채 치환형 깊이효과는 별도 눈 쉐이더 권장 — Docs/06 3-1)") },
            { "_EyeMap",             new GUIContent("Eye Overlay",         "눈 시차 오버레이 맵. RGB=오버레이 색(홍채 디테일/캐치라이트), A=적용 마스크(흰자/외곽=0). 기본 black=효과 없음(안전).") },
            { "_EyeParallaxStrength",new GUIContent("Eye Parallax",        "시선에 따른 UV 이동량(깊이감). 0이면 시차 없음.") },
            { "_EyeHighlightStrength",new GUIContent("Eye Overlay Strength","오버레이 가산 세기. 0이면 적용 안 됨.") },
            // Rendering
            { "_StencilRef",         new GUIContent("Stencil Ref",         "스텐실 참조값. 앞머리 투과: 쓰기·읽기 머티리얼이 같은 값 사용(예 1). 값보다 Comp 비교가 핵심.") },
            { "_StencilComp",        new GUIContent("Stencil Comp",        "스텐실 비교 함수. 쓰기 머티리얼=Always, 읽기(눈썹/눈)=Equal.") },
            { "_StencilPass",        new GUIContent("Stencil Pass Op",     "스텐실 통과 시 연산. 쓰기=Replace(Ref 기록), 읽기=Keep.") },
            { "_ZTestMode",          new GUIContent("ZTest",               "깊이 테스트. 기본 LessEqual. 앞머리 투과 읽기 머티리얼(눈썹/눈)이 헤어 깊이를 무시하고 위에 그릴 땐 Always.") },
            { "_UseStencilMask",     new GUIContent("Use Stencil Mask",    "스텐실 영역 마스크 사용(앞머리 영역만 적용). 끄면 컴파일 제거.") },
            { "_StencilMask",        new GUIContent("Stencil Mask (R)",    "적용 영역 마스크(R). 마스크 밖 픽셀은 clip(색/깊이/스텐실 모두 제외). 미할당(white)=전체.") },
            { "_StencilMaskCutoff",  new GUIContent("Mask Cutoff",         "마스크 컷오프 임계값. R이 이 값보다 작으면 clip.") },
            { "_UseQualityFade",     new GUIContent("Distance Quality Fade","원거리에서 비싼 HQ 가산항(MatCap/PBR/AngelRing/SSS/DepthRim) 연산을 스킵+페이드. 다수 캐릭터 비용 방어. 끄면 항상 풀 품질.") },
            { "_QualityFadeStart",   new GUIContent("Fade Start Dist",     "이 거리(m)부터 HQ 항이 줄기 시작.") },
            { "_QualityFadeEnd",     new GUIContent("Fade End Dist",       "이 거리(m)에서 HQ 항 0(이후 연산 스킵). Start보다 커야 함.") },
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            // 헤더
            EditorGUILayout.LabelField("CharacterToon", EditorStyles.boldLabel);

            // 보기 모드 선택 (영속화)
            ViewMode currentMode = (ViewMode)EditorPrefs.GetInt(ShaderModePrefKey, (int)ViewMode.Simple);
            string[] modeLabels = { "Simple", "Advanced" };
            ViewMode newMode = (ViewMode)GUILayout.Toolbar((int)currentMode, modeLabels);
            if (newMode != currentMode)
            {
                EditorPrefs.SetInt(ShaderModePrefKey, (int)newMode);
                currentMode = newMode;
            }

            // 결정 #14 Phase 2: 프리셋 저장/불러오기 (머티리얼 프로퍼티+키워드 일괄)
            DrawPresetBar(materialEditor);

            // 2-3: 렌더링 모드(Opaque/Transparent) — 인스펙터 최상단. 변경 시 Blend/ZWrite/RenderQueue 설정.
            DrawSurfaceModeDropdown(materialEditor, properties);

            EditorGUILayout.Space();

            // [S] Simple+Advanced 공통 섹션 (lilToon 용어)
            DrawFoldoutSection(materialEditor, properties, SectionMain, "Main Color",
                new[] { "_BaseMap", "_BaseColor", "_Part" });

            DrawFoldoutSection(materialEditor, properties, SectionNormal, "Normal Map",
                new[] { "_UseNormalMap", "_BumpMap", "_BumpScale" });

            // 2-1: SDF + 일반 그림자 + Ramp/ILM 을 하나의 Shadow 섹션으로 병합(모델러 요구 — 인스펙터 단일 섹션).
            DrawShadowSection(materialEditor, properties, currentMode);

            DrawFoldoutSection(materialEditor, properties, SectionRim, "Rim Light",
                new[] { "_UseRim", "_RimColor", "_RimThreshold", "_RimSoftness", "_RimIntensity", "_RimInteractionBoost",
                        "_UseDepthRim", "_DepthRimColor", "_DepthRimWidth", "_DepthRimThreshold", "_DepthRimIntensity" });

            DrawFoldoutSection(materialEditor, properties, SectionAddLights, "Additional Lights",
                new[] { "_UseAddLights", "_AdditionalLightStrength" });

            DrawFoldoutSection(materialEditor, properties, SectionOutline, "Outline",
                new[] { "_UseOutline", "_OutlineColor", "_OutlineMap", "_OutlineMask", "_OutlineWidth",
                        "_OutlineDistanceFade", "_OutlineFadeStart", "_OutlineDepthOffset" });

            // 4-1: 단일 통합 모델을 하위그룹으로 정리(스타일 vs 재질). 별도 Specular 워크플로 아님.
            DrawPBRSection(materialEditor, properties);

            DrawFoldoutSection(materialEditor, properties, SectionEmission, "Emission",
                new[] { "_UseEmission", "_EmissionMap", "_EmissionColor" });

            // [A] Advanced 전용 섹션
            if (currentMode == ViewMode.Advanced)
            {
                DrawFoldoutSection(materialEditor, properties, SectionMatCap, "MatCap",
                    new[] { "_UseMatCap", "_MatCap", "_MatCapStrength", "_MatCapNormalStrength", "_UseMatCapMask", "_MatCapMask",
                            "_UseMatCap2", "_MatCap2", "_MatCap2Strength", "_MatCap2Blend", "_UseMatCap2Mask", "_MatCap2Mask" });

                DrawFoldoutSection(materialEditor, properties, SectionHair, "Hair (Angel Ring)",
                    new[] { "_UseAngelRing", "_AngelRingColor", "_AngelRingIntensity", "_AngelRingPower",
                            "_AngelRingAngle", "_AngelRingMask" });

                DrawFoldoutSection(materialEditor, properties, SectionSSS, "SSS (Skin · optional/deprecate)",
                    new[] { "_UseSSS", "_SkinSSSColor", "_SkinSSSCenter", "_SkinSSSWidth", "_SkinSSSStrength" });

                DrawFoldoutSection(materialEditor, properties, SectionEye, "Eye",
                    new[] { "_UseEyeParallax", "_EyeMap", "_EyeParallaxStrength", "_EyeHighlightStrength" });

                if (DrawFoldoutHeader(SectionRendering, "Rendering"))
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawPropertyIfExists(materialEditor, properties, "_ZTestMode");
                        EditorGUILayout.LabelField("Stencil (앞머리 투과 등)", EditorStyles.miniBoldLabel);
                        DrawPropertyIfExists(materialEditor, properties, "_StencilRef");
                        DrawPropertyIfExists(materialEditor, properties, "_StencilComp");
                        DrawPropertyIfExists(materialEditor, properties, "_StencilPass");
                        DrawPropertyIfExists(materialEditor, properties, "_UseStencilMask");
                        DrawPropertyIfExists(materialEditor, properties, "_StencilMask");
                        DrawPropertyIfExists(materialEditor, properties, "_StencilMaskCutoff");
                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Quality (거리 페이드)", EditorStyles.miniBoldLabel);
                        DrawPropertyIfExists(materialEditor, properties, "_UseQualityFade");
                        DrawPropertyIfExists(materialEditor, properties, "_QualityFadeStart");
                        DrawPropertyIfExists(materialEditor, properties, "_QualityFadeEnd");
                        EditorGUILayout.Space();
                        materialEditor.RenderQueueField();
                        materialEditor.EnableInstancingField();
                        materialEditor.DoubleSidedGIField();
                    }
                }
            }

            EditorGUILayout.Space();
        }

        /// <summary>
        /// 2-3: 렌더링 모드(Opaque/Transparent) 드롭다운. 변경 시 모든 타깃 머티리얼에 Blend/ZWrite/RenderQueue/RenderType 적용.
        /// 단일 쉐이더 + 머티리얼 블렌드 상태 방식(URP Lit 표준) — 모드별 쉐이더 복제 없이 부위별 모드 선택.
        /// </summary>
        private void DrawSurfaceModeDropdown(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            MaterialProperty surface = FindProperty("_Surface", properties, false);
            if (surface == null)
                return;

            int mode = (int)surface.floatValue;
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = surface.hasMixedValue;
            _labels.TryGetValue("_Surface", out GUIContent label);
            int newMode = EditorGUILayout.Popup(label ?? new GUIContent("Rendering Mode"), mode, new[] { "Opaque", "Transparent" });
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                surface.floatValue = newMode;
                foreach (Object t in materialEditor.targets)
                {
                    if (t is Material m)
                    {
                        Undo.RecordObject(m, "Set Rendering Mode");
                        ApplySurfaceMode(m, newMode);
                        EditorUtility.SetDirty(m);
                    }
                }
            }
        }

        /// <summary>모드값에 맞춰 머티리얼의 Blend/ZWrite/RenderQueue/RenderType 태그를 설정한다.</summary>
        private static void ApplySurfaceMode(Material m, int mode)
        {
            if (mode == 1) // Transparent
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent; // 3000
            }
            else // Opaque
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = (int)RenderQueue.Geometry; // 2000
            }
        }

        /// <summary>
        /// 결정 #14 Phase 2 — 프리셋 저장/불러오기 바.
        /// Unity Preset(.preset) 자산으로 머티리얼의 프로퍼티+키워드를 일괄 저장/적용한다(코드 비참조, lilToon UX 벤치마크).
        /// </summary>
        private void DrawPresetBar(MaterialEditor materialEditor)
        {
            var mat = materialEditor.target as Material;
            if (mat == null)
                return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Preset", GUILayout.Width(48));

                if (GUILayout.Button("Save…", EditorStyles.miniButtonLeft))
                {
                    string path = EditorUtility.SaveFilePanelInProject(
                        "Save CharacterToon Preset", mat.name + "_Preset", "preset",
                        "프리셋(.preset) 저장 위치를 선택하세요");
                    if (!string.IsNullOrEmpty(path))
                    {
                        var preset = new Preset(mat);
                        AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(path));
                        AssetDatabase.SaveAssets();
                    }
                }

                if (GUILayout.Button("Load…", EditorStyles.miniButtonRight))
                {
                    string abs = EditorUtility.OpenFilePanel("Load CharacterToon Preset", "Assets", "preset");
                    if (!string.IsNullOrEmpty(abs))
                    {
                        string rel = abs;
                        if (abs.StartsWith(Application.dataPath))
                            rel = "Assets" + abs.Substring(Application.dataPath.Length);

                        var preset = AssetDatabase.LoadAssetAtPath<Preset>(rel);
                        if (preset != null && preset.CanBeAppliedTo(mat))
                        {
                            foreach (Object t in materialEditor.targets)
                            {
                                Undo.RecordObject(t, "Apply CharacterToon Preset");
                                preset.ApplyTo(t);
                                // 프로퍼티값으로부터 [Toggle]/[KeywordEnum] 키워드 재동기화(Codex 권장)
                                if (t is Material m)
                                    MaterialEditor.ApplyMaterialPropertyDrawers(m);
                                EditorUtility.SetDirty(t);
                            }
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("CharacterToon",
                                "선택한 프리셋을 이 머티리얼에 적용할 수 없습니다(타입 불일치).", "확인");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 2-1: SDF + 일반 그림자 + Ramp/ILM 을 하나의 Shadow 섹션으로 병합한다(모델러 요구).
        /// 마스크 타입(Enum) 선택, 1·2차 밴드, 받는 그림자/억제 마스크, Face SDF, 그리고 Advanced 전용(Ramp/ILM/바이어스)을
        /// 하위 그룹 라벨로 구분해 한 폴드아웃에 표시한다.
        /// </summary>
        private void DrawShadowSection(MaterialEditor materialEditor, MaterialProperty[] properties, ViewMode mode)
        {
            if (!DrawFoldoutHeader(SectionShadow, "Shadow"))
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUI.IndentLevelScope())
            {
                // 마스크 방식 선택 (SDF / Strength)
                DrawPropertyIfExists(materialEditor, properties, "_ShadowMaskType");

                // 1·2차 그림자 밴드 (border/blur/color/range)
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Shadow Bands", EditorStyles.miniBoldLabel);
                foreach (string p in new[] {
                    "_ShadowColor", "_ShadowBorder", "_ShadowBlur",
                    "_Shadow2ndColor", "_Shadow2ndBorder", "_Shadow2ndBlur",
                    "_ShadowStrength" })
                    DrawPropertyIfExists(materialEditor, properties, p);

                // 받는 그림자 + 일반 그림자 억제 마스크
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Receive / Mask", EditorStyles.miniBoldLabel);
                DrawPropertyIfExists(materialEditor, properties, "_ReceiveShadowStrength");
                DrawPropertyIfExists(materialEditor, properties, "_ShadowMaskTex");

                // Face SDF (마스크 타입 = SDF 일 때 사용)
                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Face SDF", EditorStyles.miniBoldLabel);
                DrawPropertyIfExists(materialEditor, properties, "_FaceSDF");
                DrawPropertyIfExists(materialEditor, properties, "_SDFSoftness");
                DrawPropertyIfExists(materialEditor, properties, "_UseHairShadow");
                DrawPropertyIfExists(materialEditor, properties, "_HairShadowMask");
                DrawPropertyIfExists(materialEditor, properties, "_HairShadowStrength");

                // Advanced 전용: 디버그 / Ramp LUT / 음영 바이어스 / 환경광 / ILM
                if (mode == ViewMode.Advanced)
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("Advanced", EditorStyles.miniBoldLabel);
                    foreach (string p in new[] {
                        "_DebugFaceLit",
                        "_ShadowOffsetScale", "_ShadeFloor", "_AmbientStrength", "_AOStrength",
                        "_UseRamp", "_RampMap", "_RampRow",
                        "_UseILM", "_ILMMap" })
                        DrawPropertyIfExists(materialEditor, properties, p);
                }
            }
        }

        /// <summary>
        /// 4-1: PBR 섹션을 "하이라이트 스타일" vs "재질(메탈릭/반사)" 하위그룹으로 정리.
        /// 단일 통합 툰 스페큘러 모델임을 UI로 분명히 한다(별도 URP Specular 워크플로 없음, 충돌 없음).
        /// </summary>
        private void DrawPBRSection(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            if (!DrawFoldoutHeader(SectionPBR, "PBR / Specular"))
                return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            using (new EditorGUI.IndentLevelScope())
            {
                DrawPropertyIfExists(materialEditor, properties, "_UsePBR");

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Highlight (라이트 반응 하이라이트)", EditorStyles.miniBoldLabel);
                DrawPropertyIfExists(materialEditor, properties, "_SpecularTint");
                DrawPropertyIfExists(materialEditor, properties, "_SpecularStep");

                EditorGUILayout.Space(2);
                EditorGUILayout.LabelField("Material (메탈릭 / 환경 반사)", EditorStyles.miniBoldLabel);
                DrawPropertyIfExists(materialEditor, properties, "_MetallicGlossMap");
                DrawPropertyIfExists(materialEditor, properties, "_Metallic");
                DrawPropertyIfExists(materialEditor, properties, "_Smoothness");
                DrawPropertyIfExists(materialEditor, properties, "_ReflectionStrength");
            }
        }

        /// <summary>폴드아웃 헤더 + 박스 본문으로 섹션을 그린다.</summary>
        private void DrawFoldoutSection(MaterialEditor materialEditor, MaterialProperty[] properties,
            string sectionId, string sectionLabel, string[] propertyNames)
        {
            if (DrawFoldoutHeader(sectionId, sectionLabel))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (string propName in propertyNames)
                        DrawPropertyIfExists(materialEditor, properties, propName);
                }
            }
        }

        /// <summary>폴드아웃 헤더를 그리고 열림 여부를 반환한다(EditorPrefs 영속화).</summary>
        private bool DrawFoldoutHeader(string sectionId, string label)
        {
            string prefKey = FoldoutPrefKeyPrefix + sectionId;
            bool isOpen = EditorPrefs.GetBool(prefKey, true); // 기본 열림

            bool newOpen = EditorGUILayout.Foldout(isOpen, label, true);
            if (newOpen != isOpen)
                EditorPrefs.SetBool(prefKey, newOpen);

            return newOpen;
        }

        /// <summary>
        /// 프로퍼티가 존재하면 ShaderProperty 로 그린다(키워드/HDR 등 동작 보존). 없으면 조용히 스킵.
        /// 라벨/툴팁은 _labels(lilToon 용어 + 한글 툴팁) 우선, 없으면 셰이더 displayName 폴백.
        /// </summary>
        private void DrawPropertyIfExists(MaterialEditor materialEditor, MaterialProperty[] properties, string propertyName)
        {
            MaterialProperty prop = FindProperty(propertyName, properties, false);
            if (prop == null)
                return;

            if (_labels.TryGetValue(propertyName, out GUIContent label))
                materialEditor.ShaderProperty(prop, label);
            else
                materialEditor.ShaderProperty(prop, prop.displayName);
        }
    }
}
