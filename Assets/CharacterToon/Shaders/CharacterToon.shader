Shader "CharacterToon/Character"
{
    // M0 스켈레톤 (T0-6). 5개 패스 + UnityPerMaterial 단일 레이아웃.
    // ForwardToon 본문은 M1(T1-1~T1-6)에서 ILM/Ramp/캐릭터라이트/림으로 채운다.
    Properties
    {
        [Header(Surface)]
        // 2-3: 렌더링 모드(Opaque/Transparent). ShaderGUI 상단 드롭다운이 Blend/ZWrite/RenderQueue를 설정.
        [Enum(Opaque,0,Transparent,1)] _Surface ("Rendering Mode", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1

        [Header(Base)]
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)

        [Header(Normal)]
        [Toggle(_USE_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Range(0,2)) = 1.0

        _ILMMap("ILM Map (RGBA)", 2D) = "black" {}
        [Toggle(_USE_ILM)] _UseILM ("Use ILM Map", Float) = 0
        [Toggle(_USE_RAMP)] _UseRamp ("Use Ramp LUT (optional)", Float) = 0
        _RampMap("Ramp Map", 2D) = "white" {}
        _RampRow("Ramp Row (V)", Range(0,1)) = 0.5
        _ShadowOffsetScale("Shadow Offset Scale", Range(0,1)) = 0.2
        _ShadeFloor("Shade Floor", Range(0,1)) = 0.2
        _AmbientStrength("Ambient Strength", Range(0,2)) = 1.0
        _AOStrength("SSAO Strength", Range(0,1)) = 1.0

        // 결정 #17: 파라메트릭 1·2차 그림자 밴드 (Range = 슬라이더+수치)
        _ShadowColor("Shadow Color 1st", Color) = (0.72,0.74,0.82,1)
        _ShadowBorder("Shadow Border 1st", Range(0,1)) = 0.5
        _ShadowBlur("Shadow Blur 1st", Range(0,1)) = 0.1
        _Shadow2ndColor("Shadow Color 2nd", Color) = (0.55,0.57,0.66,1)
        _Shadow2ndBorder("Shadow Border 2nd", Range(0,1)) = 0.25
        _Shadow2ndBlur("Shadow Blur 2nd", Range(0,1)) = 0.1
        _ShadowStrength("Shadow Strength", Range(0,1)) = 1.0
        _ReceiveShadowStrength("Receive Cast Shadow", Range(0,1)) = 1.0
        // 2-1: 그림자 마스크 타입(SDF=얼굴 플립샘플 / Strength=일반 half-Lambert) + 일반 그림자 억제 마스크
        [Enum(SDF,0,Strength,1)] _ShadowMaskType("Shadow Mask Type", Float) = 0
        _ShadowMaskTex("Shadow Mask (R, suppress)", 2D) = "white" {}

        [Header(Part)]
        [KeywordEnum(None, Face, Hair, Skin, Cloth)] _Part ("Part Type", Float) = 0

        [Header(Rim)]
        [Toggle(_USE_RIM)] _UseRim ("Use Rim Light", Float) = 1
        _RimColor("Rim Color", Color) = (1,1,1,1)
        _RimThreshold("Rim Threshold", Range(0,1)) = 0.6
        _RimSoftness("Rim Softness", Range(0,0.5)) = 0.05
        _RimIntensity("Rim Intensity", Range(0,4)) = 1.0
        _RimInteractionBoost("Rim Interaction Boost", Range(1,4)) = 1.0

        [Header(Depth Rim)]
        [Toggle(_USE_DEPTH_RIM)] _UseDepthRim ("Use Depth Rim (screenspace)", Float) = 0
        _DepthRimColor("Depth Rim Color", Color) = (1,1,1,1)
        _DepthRimWidth("Depth Rim Width (px)", Range(0,10)) = 2.0
        _DepthRimThreshold("Depth Rim Threshold", Range(0.001,1)) = 0.1
        _DepthRimIntensity("Depth Rim Intensity", Range(0,4)) = 1.0

        [Header(Additional Lights)]
        // L3: 부가광(point/spot) 셀셰이딩. 키라이트(메인/캐릭터) 외 씬 광원이 캐릭터에 툰 음영을 주게 한다.
        [Toggle(_USE_ADD_LIGHTS)] _UseAddLights ("Use Additional Lights", Float) = 1
        _AdditionalLightStrength("Additional Light Strength", Range(0,2)) = 1.0

        [Header(Outline)]
        [Toggle(_USE_OUTLINE)] _UseOutline ("Use Outline", Float) = 1
        _OutlineColor("Outline Color", Color) = (0,0,0,1)
        _OutlineMap("Outline Color Map", 2D) = "white" {}
        _OutlineMask("Outline Mask (R, suppress)", 2D) = "white" {}
        _OutlineWidth("Outline Width", Range(0,5)) = 1.0
        _OutlineDistanceFade("Outline Distance Fade", Range(0,1)) = 0.0
        _OutlineFadeStart("Outline Fade Start Dist", Range(0.1,50)) = 5.0
        _OutlineDepthOffset("Outline Depth Offset", Range(0,1)) = 0.0

        [Header(Face SDF)]
        _FaceSDF("Face SDF", 2D) = "white" {}
        _SDFSoftness("SDF Softness", Range(0,0.5)) = 0.05
        [Toggle(_USE_HAIR_SHADOW)] _UseHairShadow ("Use Hair Shadow (on Face)", Float) = 0
        _HairShadowMask("Hair Shadow Mask", 2D) = "white" {}
        _HairShadowStrength("Hair Shadow Strength", Range(0,1)) = 1.0
        [Toggle(_DEBUG_FACELIT)] _DebugFaceLit ("DEBUG: Show faceLit (grayscale)", Float) = 0

        [Header(Lobby HQ)]
        [Toggle(_USE_MATCAP)] _UseMatCap ("Use MatCap", Float) = 0
        _MatCap("MatCap", 2D) = "black" {}
        _MatCapStrength("MatCap Strength", Range(0,4)) = 1.0
        [HDR] _MatCapColor("MatCap Color", Color) = (1,1,1,1)
        _MatCapBlur("MatCap Blur", Range(0,1)) = 0.0
        // 4-5: 노말이 MatCap UV에 미치는 영향도(0=지오메트릭 매끈/시점 흔들림 없음, 1=노말 섭동 디테일). MatCap/2 공통.
        _MatCapNormalStrength("MatCap Normal Influence", Range(0,1)) = 1.0
        [Toggle] _UseMatCapMask ("Use Separate MatCap Mask", Float) = 0
        _MatCapMask("MatCap Mask (R)", 2D) = "white" {}
        // 2-5: 두 번째 MatCap 슬롯 (한 머티리얼에 MatCap 2개)
        [Toggle(_USE_MATCAP2)] _UseMatCap2 ("Use Second MatCap", Float) = 0
        _MatCap2("Second MatCap", 2D) = "black" {}
        _MatCap2Strength("Second MatCap Strength", Range(0,4)) = 1.0
        [HDR] _MatCap2Color("Second MatCap Color", Color) = (1,1,1,1)
        _MatCap2Blur("Second MatCap Blur", Range(0,1)) = 0.0
        [Enum(Add,0,Multiply,1)] _MatCap2Blend("Second MatCap Blend", Float) = 0
        [Toggle] _UseMatCap2Mask ("Use Second MatCap Mask", Float) = 0
        _MatCap2Mask("Second MatCap Mask (R)", 2D) = "white" {}
        [Toggle(_USE_ANGELRING)] _UseAngelRing ("Use Angel Ring (Hair)", Float) = 1
        _AngelRingColor("Angel Ring Color", Color) = (1,1,1,1)
        _AngelRingIntensity("Angel Ring Intensity", Range(0,4)) = 1.0
        _AngelRingPower("Angel Ring Power", Range(1,128)) = 20.0
        // 4-6: 링 방향(세로↔가로) 회전 각도 + 결 마스크(꼭대기/바닥 제외, 머리 결 따라)
        _AngelRingAngle("Angel Ring Angle", Range(0,360)) = 0.0
        _AngelRingMask("Angel Ring Mask (R)", 2D) = "white" {}
        // 4-4: SSS deprecate(기본 OFF). 하드 셀에선 효과가 잘 안 읽혀(모델러) 그림자 밴드 색(_ShadowColor/_Shadow2ndColor)으로 대체 권장.
        //   합동 시각 세션에서 불필요 확정 시 하드 삭제(복구 레시피 Docs/06 §4-4). Docs/08 참고.
        [Toggle(_USE_SSS)] _UseSSS ("Use Skin SSS (optional)", Float) = 0
        _SkinSSSColor("Skin SSS Color", Color) = (1,0.4,0.4,1)
        _SkinSSSCenter("Skin SSS Center", Range(0,1)) = 0.5
        _SkinSSSWidth("Skin SSS Width", Range(0.001,0.5)) = 0.1
        _SkinSSSStrength("Skin SSS Strength", Range(0,2)) = 0.5

        [Header(Eyes)]
        [Toggle(_USE_EYE_PARALLAX)] _UseEyeParallax ("Use Eye Parallax", Float) = 0
        _EyeMap("Eye Overlay (RGB=detail, A=mask)", 2D) = "black" {}
        _EyeParallaxStrength("Eye Parallax", Range(0,0.2)) = 0.03
        _EyeHighlightStrength("Eye Overlay Strength", Range(0,4)) = 1.0

        [Header(PBR)]
        [Toggle(_USE_PBR)] _UsePBR ("Use PBR Specular/Reflection", Float) = 0
        _MetallicGlossMap("Metallic(R) Smoothness(A)", 2D) = "white" {}
        _Metallic("Metallic", Range(0,1)) = 0.0
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _SpecularTint("Specular Tint", Color) = (1,1,1,1)
        _SpecularStep("Specular Step (cel)", Range(0,0.5)) = 0.05
        _ReflectionStrength("Reflection Strength", Range(0,1)) = 0.5

        [Header(Emission)]
        _EmissionMap("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,1)
        [Toggle(_USE_EMISSION)] _UseEmission ("Use Emission", Float) = 0

        [Header(Stencil)]
        [IntRange] _StencilRef("Stencil Ref", Range(0,255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comp", Float) = 8
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass("Stencil Pass Op", Float) = 0
        // 3-2: 앞머리 투과(헤어 뒤 눈썹/눈 표시) 지원 — 깊이 테스트 + 스텐실 영역 마스크
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTestMode("ZTest", Float) = 4   // 4=LEqual(기본), 8=Always(읽기 머티리얼이 헤어 깊이 무시하고 위에 그릴 때)
        [Toggle(_USE_STENCIL_MASK)] _UseStencilMask ("Use Stencil Mask", Float) = 0
        _StencilMask("Stencil Mask (R)", 2D) = "white" {}
        _StencilMaskCutoff("Stencil Mask Cutoff", Range(0,1)) = 0.5

        [Header(Quality)]
        // S2: 거리 품질 페이드 — 원거리 캐릭터의 비싼 HQ 가산항(MatCap/PBR/AngelRing/SSS/DepthRim) 스킵+페이드
        [Toggle(_USE_QUALITY_FADE)] _UseQualityFade ("Use Distance Quality Fade", Float) = 0
        _QualityFadeStart("Quality Fade Start Dist", Range(0,100)) = 15.0
        _QualityFadeEnd("Quality Fade End Dist", Range(0,200)) = 40.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        // =========================================================
        // ForwardToon
        // 결정 #1 (T0-3): UniversalForwardOnly 확정.
        //   근거: PC=Forward+, Mobile=Forward, Deferred 미사용. GBuffer 패스가 없는
        //   캐릭터 셰이더이므로 ForwardOnly가 세 경로(Forward/Forward+/Deferred) 모두에서
        //   올바르게 렌더된다. 향후 누가 렌더러를 Deferred로 바꿔도 안전.
        // =========================================================
        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Back
            Blend [_SrcBlend] [_DstBlend]   // 2-3: Opaque=One,Zero / Transparent=SrcAlpha,OneMinusSrcAlpha
            ZWrite [_ZWrite]                // 2-3: Opaque=On / Transparent=Off
            ZTest [_ZTestMode]              // 3-2: 기본 LEqual / 앞머리 투과 읽기 머티리얼은 Always
            Stencil { Ref [_StencilRef] Comp [_StencilComp] Pass [_StencilPass] }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // 결정 #8 (T0-4): 키라이트(메인/캐릭터 글로벌 주입) + SH 앰비언트 + L3 부가광 셀셰이딩.
            //   L3(갭, 2026-06-20): frag가 GetAdditionalLight 루프(ShadeAdditionalToon)를 사용하므로 부가광 키워드를 되살림.
            //   (앞서 미사용이라 제거했던 것 — Docs/07. 이제 _USE_ADD_LIGHTS 토글로 머티리얼별 on/off + S2 거리 페이드와 병행.)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // 결정 #15: 단일 고품질 티어 — LOBBY_HQ 품질-티어 키워드 제거(HQ 기능 상시 활성, 변형 수 절감)
            #pragma shader_feature_local _PART_NONE _PART_FACE _PART_HAIR _PART_SKIN _PART_CLOTH
            #pragma shader_feature_local _ _DEBUG_FACELIT
            #pragma shader_feature_local _ _USE_NORMALMAP
            #pragma shader_feature_local _ _USE_ILM
            #pragma shader_feature_local _ _USE_RAMP
            #pragma shader_feature_local _ _USE_HAIR_SHADOW
            #pragma shader_feature_local _ _USE_MATCAP
            #pragma shader_feature_local _ _USE_EYE_PARALLAX
            #pragma shader_feature_local _ _USE_EMISSION
            #pragma shader_feature_local _ _USE_PBR
            #pragma shader_feature_local _ _USE_DEPTH_RIM
            #pragma shader_feature_local _ _USE_RIM
            #pragma shader_feature_local _ _USE_ANGELRING
            #pragma shader_feature_local _ _USE_SSS
            #pragma shader_feature_local _ _USE_MATCAP2
            #pragma shader_feature_local _ _USE_STENCIL_MASK
            #pragma shader_feature_local _ _USE_QUALITY_FADE
            #pragma shader_feature_local _ _USE_ADD_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "../ShaderLibrary/ToonInput.hlsl"
            #include "../ShaderLibrary/ToonLighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;   // URP fog: 정점에서 계산해 프래그에서 MixFog
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   n = GetVertexNormalInputs(input.normalOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS   = n.normalWS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = (half)ComputeFogFactor(p.positionCS.z);   // 안개 씬에서 캐릭터 분리감 방지
                return o;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // 3-2: 스텐실 영역 마스크 — 마스크 밖 픽셀은 clip(색/깊이/스텐실 쓰기 모두 제외).
                //   앞머리 투과 셋업에서 "앞머리 영역만" 스텐실을 쓰거나 읽기 머티리얼 적용 범위를 제한할 때.
                //   기본 white=clip(1-cutoff)>0 → 전체 통과(no-op). 키워드 off 시 완전 no-op.
            #if defined(_USE_STENCIL_MASK)
                clip(SAMPLE_TEXTURE2D(_StencilMask, sampler_StencilMask, input.uv).r - _StencilMaskCutoff);
            #endif

                // WP-B: 노멀맵 적용 시 N 섭동. 키워드 off → 보간 노멀 그대로(no-op).
                // 메시 TANGENT 대신 픽셀 미분 코탄젠트 프레임 사용 → 결정 #3(아웃라인 스무스노멀이
                // TANGENT.xyz 점유)과 양립. 스키닝/미러 UV 자동 정합. (ToonLighting.hlsl)
                half3 N = normalize(input.normalWS);
            #if defined(_USE_NORMALMAP)
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                N = ApplyNormalMapDerivative(N, input.positionWS, input.uv, normalTS);
            #endif
                half3 V = normalize(GetWorldSpaceViewDir(input.positionWS));

                // S2: 거리 품질 페이드 계수(1=근거리 풀HQ, 0=원거리). 키워드 off면 상수 1 → 분기/곱 컴파일 제거(완전 no-op).
                //   원거리에서 비싼 HQ 가산항을 if로 스킵(샘플/연산 절약)하고 동시에 페이드(팝 방지). 캐릭터 단위 coherent 분기.
                // SSAO: 화면공간 AO를 캐릭터 음영에 반영. 키워드 off면 GetScreenSpaceAmbientOcclusion이 1/1 반환(no-op).
                //   _AOStrength로 영향도 조절(툰은 과한 AO가 셀을 뭉갤 수 있어 다이얼). indirect=앰비언트, direct=직접광.
                half aoIndirect = 1.0h;
                half aoDirect   = 1.0h;
            #if defined(_SCREEN_SPACE_OCCLUSION)
                AmbientOcclusionFactor aoFac = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                aoIndirect = lerp(1.0h, aoFac.indirectAmbientOcclusion, _AOStrength);
                aoDirect   = lerp(1.0h, aoFac.directAmbientOcclusion,   _AOStrength);
            #endif

                half hqAmount = 1.0h;
            #if defined(_USE_QUALITY_FADE)
                // 거리는 픽셀별 positionWS가 아니라 "오브젝트 원점"으로 계산한다 — 드로우 전체가 동일한 hqAmount가 되어
                //   분기가 완전 quad-uniform(발산 없음) → 분기 내 ddx/ddy(Angel Ring) · 암묵적 밉 샘플(MatCap/PBR)이 안전하고
                //   분기 coherence도 최대(perf). 또한 캐릭터 단위 페이드라 "반쪽만 품질 다름"을 방지한다.
                float3 objPosWS = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float distToCam = distance(objPosWS, _WorldSpaceCameraPos);
                hqAmount = saturate(1.0h - (half)((distToCam - _QualityFadeStart) / max(_QualityFadeEnd - _QualityFadeStart, 1e-3)));
            #endif

                // --- M1: ILM + Character Light + Rim ---
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // Sample ILM or use neutral fallback
                half4 ilm = half4(0.0h, 0.5h, 0.5h, 0.0h);
            #if defined(_USE_ILM)
                ilm = SAMPLE_TEXTURE2D(_ILMMap, sampler_ILMMap, input.uv);
            #endif

                // Character light vs main light.
                // w>0.5 이고 xyz가 0이 아닐 때만 사용 — 0이면 normalize(0)=NaN 이므로 메인 라이트로 폴백(Codex 지적).
                half3 L;
                if (_CharacterLightDirWS.w > 0.5h && dot(_CharacterLightDirWS.xyz, _CharacterLightDirWS.xyz) > 1e-6) {
                    L = normalize(_CharacterLightDirWS.xyz);
                } else {
                    L = mainLight.direction;
                }

                // Half-Lambert + ILM shadow bias
                half ndotl = dot(N, L);
                half halfLambert = ndotl * 0.5h + 0.5h;
                half shadowBias = (ilm.g - 0.5h) * _ShadowOffsetScale;
                half lightVal = saturate(halfLambert + shadowBias);  // 0=그림자, 1=빛

                // M2: Face SDF flip sampling (decision #5: SDF black/white, UV symmetry, RdotL flip direction provisional)
                #if defined(_PART_FACE)
                // 2-1: 마스크 타입 = SDF(0) 일 때만 얼굴 SDF 플립샘플. Strength(1) 이면 위 half-Lambert lightVal 그대로 사용.
                if (_ShadowMaskType < 0.5h)
                {
                    // 라이트/얼굴 벡터 모두 안전 정규화 — 글로벌 미설정(0)이어도 기본값으로 동작.
                    // 얼굴 벡터가 (0,0,0)이면 normalize(0,0)=NaN -> coverage NaN -> 라이트 무반응이 되므로
                    // forward 기본 +Z(0,1), right 기본 +X(1,0)로 폴백. (Validator "Apply Face Vectors" 미클릭 방어)
                    float2 lightXZ = SafeNormalizeXZ(L.xz,               float2(0.0, 1.0));
                    float2 fwdXZ   = SafeNormalizeXZ(_FaceForwardWS.xz,  float2(0.0, 1.0));
                    float2 rightXZ = SafeNormalizeXZ(_FaceRightWS.xz,    float2(1.0, 0.0));
                    float FdotL = dot(fwdXZ, lightXZ);
                    float RdotL = dot(rightXZ, lightXZ);
                    float2 uvFace = input.uv;
                    float2 uvFlip = float2(1.0 - input.uv.x, input.uv.y);
                    half sdfLeft  = SAMPLE_TEXTURE2D(_FaceSDF, sampler_FaceSDF, uvFace).r;
                    half sdfRight = SAMPLE_TEXTURE2D(_FaceSDF, sampler_FaceSDF, uvFlip).r;
                    half sdf = lerp(sdfLeft, sdfRight, step(0.0, RdotL));
                    half coverage = saturate((-FdotL + 1.0) * 0.5);
                    // 결정 #15: 단일 고품질 — 항상 소프트 경계.
                    // SDF 경계 폭 = 아티스트 소프트니스(값 공간, 매끈) + fwidth(sdf) AA 바닥(더하기, 곱하기 아님).
                    //  - 소프트니스 0: 폭이 fwidth만 남아 ~1px AA → 가파른 영역의 계단/픽셀 노이즈 제거.
                    //  - 소프트니스 ↑: 폭이 매끈한 값 공간 상수에 지배, fwidth 기여는 무시됨 → 깔끔하게 부드러워짐.
                    //  ※ 직전엔 _SDFSoftness*fwidth로 '곱'했더니, fwidth의 2x2 quad 양자화 잡음이
                    //    높은 블러에서 폭 전체로 증폭되어 그림자가 얼룩덜룩(블록 얼룩)해짐 → '더하기'로 분리.
                    half halfBand = _SDFSoftness + fwidth(sdf) * 0.5h;
                    half faceLit = smoothstep(coverage - halfBand, coverage + halfBand, sdf);
                    
                    // Hair shadow mask: sample mask at input.uv and darken faceLit
                    #if defined(_USE_HAIR_SHADOW)
                        half hairMask = SAMPLE_TEXTURE2D(_HairShadowMask, sampler_HairShadowMask, input.uv).r;
                        faceLit *= lerp(1.0h, hairMask, _HairShadowStrength);
                    #endif
                    
                    #if defined(_DEBUG_FACELIT)
                        // 진단: faceLit을 흑백으로 직접 출력. 라이트 회전 시
                        //  - 경계가 공간적으로 이동 -> SDF 정상 (룩 문제는 ramp/base)
                        //  - 얼굴 전체가 한꺼번에 흰<->검 -> SDF 텍스처가 균일(.r 변화 없음)
                        //  - 전혀 안 변함 -> Part가 Face가 아니거나 다른 머티리얼
                        return half4(faceLit, faceLit, faceLit, 1.0h);
                    #endif

                    lightVal = faceLit;
                }
                #endif

                // 결정 #17: 받는 캐스트 그림자를 라이팅 값에 반영(세기 _ReceiveShadowStrength)
                lightVal *= lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadowStrength);

                // 2-1: 일반 그림자 마스크 — R<1 영역은 그림자를 억제(빛 쪽으로 끌어올림).
                // 눈동자·눈 안쪽 흰자 등 만화적 특성상 음영을 안 지게 하고 싶은 영역 제어용.
                // 중립 폴백 = white(R=1) → no-op. 마스크/SDF/일반음영 어느 경로든 lightVal에 일괄 적용.
                half shadowMaskTex = SAMPLE_TEXTURE2D(_ShadowMaskTex, sampler_ShadowMaskTex, input.uv).r;
                lightVal = lerp(1.0h, lightVal, shadowMaskTex);

                // 음영 적용 — 기본: 파라메트릭 1·2차 밴드(모델러 요구), 옵션(_USE_RAMP): RampMap LUT
                half3 shadedAlbedo;
                half shadeMask;   // 0=빛, 1=그림자. 아래 앰비언트 변조에 사용(셀 대비 보존).
            #if defined(_USE_RAMP)
                half3 ramp = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, half2(lightVal, _RampRow)).rgb;
                shadedAlbedo = baseColor.rgb * ramp;
                shadeMask = 1.0h - saturate(lightVal);
            #else
                // 1·2차 그림자 팩터(1=그림자,0=빛): border 위치 + blur 폭, _ShadowStrength=전체 적용 범위
                // blur=0(하드 그림자) 시 smoothstep 동일 edge → 0나눗셈 NaN 방지 (Codex 지적)
                half blur1 = max(_ShadowBlur,    1e-4h);
                half blur2 = max(_Shadow2ndBlur, 1e-4h);
                half shadow1 = (1.0h - smoothstep(_ShadowBorder    - blur1, _ShadowBorder    + blur1, lightVal)) * _ShadowStrength;
                half shadow2 = (1.0h - smoothstep(_Shadow2ndBorder - blur2, _Shadow2ndBorder + blur2, lightVal)) * _ShadowStrength;
                shadedAlbedo = baseColor.rgb;
                shadedAlbedo = lerp(shadedAlbedo, baseColor.rgb * _ShadowColor.rgb,    shadow1); // 1차 그림자 색
                shadedAlbedo = lerp(shadedAlbedo, baseColor.rgb * _Shadow2ndColor.rgb, shadow2); // 2차(심부) 그림자 색
                shadeMask = saturate(max(shadow1, shadow2));
            #endif

                // Base shading (직접광)
                // L1: 캐릭터 라이트 활성(w>0.5) 이고 색 세기>0 이면 퍼-캐릭터 색/세기 사용, 아니면 메인 라이트 색.
                half3 lightColor = mainLight.color;
                if (_CharacterLightDirWS.w > 0.5h && _CharacterLightColor.a > 0.0h)
                    lightColor = _CharacterLightColor.rgb * _CharacterLightColor.a;
                half3 shaded = shadedAlbedo * lightColor * aoDirect;   // SSAO: 직접광에 direct AO

                // 환경광(SH): 빛 영역엔 가득, 그림자 영역엔 (1-shadeMask)로 줄여서 더한다.
                // 이렇게 하지 않으면 밝은 라이트(intensity>1)에서 빛·그림자 양쪽이 모두 1.0을 넘겨
                // 흰색으로 클리핑 → 셀 그림자가 화면에서 사라진다(디버그 그레이스케일은 멀쩡한데
                // 컬러 출력만 음영이 안 보이던 근본 원인). 그림자엔 앰비언트를 빼서 셀 대비를 지킨다.
                half3 ambient = SampleSH(N) * _AmbientStrength * baseColor.rgb;
                shaded += ambient * (1.0h - shadeMask) * aoIndirect;   // SSAO: 앰비언트에 indirect AO

                // 그림자 최소 밝기 하한(완전 검정 방지). 그림자 톤은 _ShadeFloor 위에서 유지된다.
                shaded = max(shaded, baseColor.rgb * _ShadeFloor);

                // L3(갭): 부가광(point/spot, Forward+ 클러스터 포함) 셀셰이딩 — 동적 라이팅 퀄리티.
                //   키라이트(메인/캐릭터)에 더해 씬 추가 광원을 셀 단계화해 가산(가산광이라 그림자 영역은 안 어둡게).
                //   _USE_ADD_LIGHTS off거나 _ADDITIONAL_LIGHTS 미설정이면 컴파일 제거(비용 0). 그림자는 _ADDITIONAL_LIGHT_SHADOWS.
            #if defined(_ADDITIONAL_LIGHTS) && defined(_USE_ADD_LIGHTS)
                {
                    half addBlur = max(_ShadowBlur, 1e-4h);
                    half3 addSum = (half3)0.0h;
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);  // Forward+ 클러스터 조회용(렌더스케일/XR 정합)
                    uint addCount = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                    // Forward+: 클러스터에 포함되지 않는 추가 디렉셔널 광원 별도 루프(URP Lit 패턴)
                    for (uint di = 0u; di < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); di++)
                        addSum += ShadeAdditionalToon(GetAdditionalLight(di, inputData.positionWS, half4(1,1,1,1)), N, baseColor.rgb, _ShadowBorder, addBlur);
                #endif
                    LIGHT_LOOP_BEGIN(addCount)
                        addSum += ShadeAdditionalToon(GetAdditionalLight(lightIndex, inputData.positionWS, half4(1,1,1,1)), N, baseColor.rgb, _ShadowBorder, addBlur);
                    LIGHT_LOOP_END
                    shaded += addSum * _AdditionalLightStrength * aoDirect;   // SSAO: 부가광에도 direct AO(일관)
                }
            #endif

                // Rim light (2-4: _USE_RIM 토글. off 시 컴파일 스트립. Depth Rim은 별도 _USE_DEPTH_RIM.)
                half3 rimColor = (half3)0.0h;
            #if defined(_USE_RIM)
                half fresnel = 1.0h - saturate(dot(N, V));
                half rim = smoothstep(_RimThreshold, _RimThreshold + _RimSoftness, fresnel);
                // 결정 #15: HQ 기능 상시 활성 (구 LOBBY_HQ 게이트 제거)
                rimColor = rim * _RimColor.rgb * _RimIntensity * _RimInteractionBoost;
            #endif

                // L2(갭): 깊이 인지 림 — 화면공간 노멀 방향으로 씬 깊이를 비교, 배경과의 깊이 점프(실루엣)에
                //   화면상 폭 일정한 역광 테두리를 더한다. 프레넬 림과 달리 카메라 거리에 무관한 굵기.
                //   요구: URP 카메라 DepthTexture 활성(현 렌더러는 DepthOnly/SSAO로 이미 사용 중).
            #if defined(_USE_DEPTH_RIM)
                if (hqAmount > 0.0h)   // S2: 원거리면 씬 깊이 2회 샘플 스킵 + 페이드
                {
                    float2 screenUV   = input.positionCS.xy / _ScreenParams.xy;
                    float  centerDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                    float3 nVS    = mul((float3x3)UNITY_MATRIX_V, N);
                    half   facing = saturate((half)length(nVS.xy));             // 정면(nVS.xy~0)에선 림 억제 — 실루엣만
                    float2 rimDir = normalize(nVS.xy + float2(1e-5, 1e-5));      // 화면공간 바깥 방향
                    float2 rimUV  = screenUV + rimDir * (_DepthRimWidth / _ScreenParams.xy);
                    float  nbrDepth = LinearEyeDepth(SampleSceneDepth(rimUV), _ZBufferParams);
                    half   depthRim = smoothstep(_DepthRimThreshold, _DepthRimThreshold * 2.0h, (half)(nbrDepth - centerDepth)) * facing;
                    rimColor += depthRim * _DepthRimColor.rgb * _DepthRimIntensity * hqAmount;
                }
            #endif

                // M4(T4-1) + WP-C: MatCap (specular/reflection, 가산=발광형 블렌드 — 요구 #3)
                #if defined(_USE_MATCAP)
                if (hqAmount > 0.0h)   // S2: 원거리면 MatCap 샘플/연산 스킵 + 페이드
                {
                    // 노멀 구동 UV — N은 WP-B 노멀맵 섭동을 반영(요구 #11 노멀→MatCap).
                    // 4-5: 영향도로 지오메트릭 노멀과 섭동 노멀 사이 보간. 0이면 매끈한 클래식 MatCap(뷰공간 swim 없음),
                    //   1이면 노말 디테일(뷰공간 특성상 시점따라 흔들림). 모델러 "어색한 swim"을 다이얼로 조절.
                    half3 mcN = SafeNormalize(lerp((half3)normalize(input.normalWS), N, _MatCapNormalStrength));
                    float3 normalVS = mul((float3x3)UNITY_MATRIX_V, mcN);
                    float2 matcapUV = normalVS.xy * 0.5 + 0.5;
                    // 블러: 밉 바이어스로 소프트닝(분기 quad-uniform → BIAS 안전). 베이스 컬러: 틴트 곱.
                    half3 matcap = SAMPLE_TEXTURE2D_BIAS(_MatCap, sampler_MatCap, matcapUV, _MatCapBlur * CHAR_MATCAP_BLUR_MAX).rgb * _MatCapColor.rgb;
                    // 결정 #16: 별도 MatCap 마스크. 변형 절감(Codex): 별도 키워드 대신 _UseMatCapMask(CBUFFER float)로 분기.
                    // 2-4 피드백: 별도 마스크 off 일 때의 베이스를 ILM 사용 시 ilm.r, ILM 미사용 시 1.0(전체)로 한다.
                    //   (기존엔 ILM off여도 ilm.r=0 폴백 → MatCap이 마스크를 켜야만 보이던 트랩 → 해소.)
                    half maskTex = SAMPLE_TEXTURE2D(_MatCapMask, sampler_MatCapMask, input.uv).r;
                    half matcapBase = 1.0h;
                #if defined(_USE_ILM)
                    matcapBase = ilm.r;
                #endif
                    half matcapMask = lerp(matcapBase, maskTex, _UseMatCapMask);
                    shaded += matcap * matcapMask * _MatCapStrength * hqAmount;
                }
                #endif

                // 2-5: 두 번째 MatCap — 별도 슬롯/마스크/강도, Add(가산)/Multiply(곱) 블렌드 선택.
                //   첫 MatCap과 동일한 노멀 구동 UV(WP-B 섭동 N 반영). 마스크는 첫 MatCap과 같은 폴백 규칙.
                #if defined(_USE_MATCAP2)
                if (hqAmount > 0.0h)   // S2: 원거리면 두 번째 MatCap 스킵 + 페이드
                {
                    half3 mcN2 = SafeNormalize(lerp((half3)normalize(input.normalWS), N, _MatCapNormalStrength)); // 4-5: 노말 영향도(공통)
                    float3 normalVS2 = mul((float3x3)UNITY_MATRIX_V, mcN2);
                    float2 matcap2UV = normalVS2.xy * 0.5 + 0.5;
                    half3 matcap2 = SAMPLE_TEXTURE2D_BIAS(_MatCap2, sampler_MatCap2, matcap2UV, _MatCap2Blur * CHAR_MATCAP_BLUR_MAX).rgb * _MatCap2Color.rgb;
                    half mask2Tex = SAMPLE_TEXTURE2D(_MatCap2Mask, sampler_MatCap2Mask, input.uv).r;
                    half matcap2Base = 1.0h;
                #if defined(_USE_ILM)
                    matcap2Base = ilm.r;
                #endif
                    half matcap2Mask = lerp(matcap2Base, mask2Tex, _UseMatCap2Mask);
                    if (_MatCap2Blend < 0.5h)
                        shaded += matcap2 * matcap2Mask * _MatCap2Strength * hqAmount;                              // Add(가산형)
                    else
                        shaded = lerp(shaded, shaded * matcap2, saturate(matcap2Mask * _MatCap2Strength) * hqAmount); // Multiply(음영/AO형)
                }
                #endif

                // WP-D: PBR 툰 스페큘러 + 환경 리플렉션 (요구 #5, 갭 M1)
                // 4-1 정리(결정): 여기에는 **단일 통합 툰 스페큘러** 하나만 있다. URP식 "Specular 워크플로(_SpecColor 맵)"와
                //   "Metallic 워크플로"가 동시에 도는 이중 BRDF가 아니다 → 충돌 없음. _SpecularTint/_SpecularStep는 하이라이트
                //   스타일(색/셀 경계), _Metallic/_Smoothness는 재질(albedo 틴트/날카로움+환경반사)로 같은 항을 함께 구성한다.
                //   MatCap과의 역할 분담: PBR 스페큘러=라이트 방향 반응(빛 이동 시 하이라이트 이동), MatCap=뷰 고정 아트지정.
                //   → 라이트 반응 하이라이트는 MatCap이 대체 못 하므로 유지(제거 안 함).
                #if defined(_USE_PBR)
                if (hqAmount > 0.0h)   // S2: 원거리면 metallic/gloss 샘플·환경반사 스킵 + 페이드
                {
                    // metallic=R, smoothness=A (맵 미할당 시 기본 white → 스칼라 폴백)
                    half4 mg = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                    half metallic   = mg.r * _Metallic;
                    half smoothness = mg.a * _Smoothness;

                    // 툰 스페큘러 — Blinn-Phong 하이라이트를 셀 단계화. ILM.B로 폭 가중(ILM 규약 B=스페큘러 폭).
                    // SafeNormalize: L==-V(half-vector 0)일 때 NaN 방지(Codex 지적).
                    half3 Hspec = SafeNormalize(L + V);
                    half ndoth  = saturate(dot(N, Hspec));
                    half specPower = exp2(smoothness * 11.0h);                 // smoothness→하이라이트 날카로움(1~2048)
                    half spec      = pow(ndoth, specPower) * (ilm.b * 0.5h + 0.5h);
                    half toonSpec  = smoothstep(0.5h - _SpecularStep, 0.5h + _SpecularStep, spec);
                    half3 specTint = _SpecularTint.rgb * lerp((half3)1.0h, baseColor.rgb, metallic); // 금속은 albedo로 틴트
                    shaded += toonSpec * specTint * lightColor * hqAmount;   // L1: 캐릭터 라이트 색 일관 적용(Codex)

                    // 환경 리플렉션(reflection probe) — 금속/반사강도 비례, perceptualRoughness로 밉 선택.
                    // 5-arg 오버로드: 프로브 블렌딩(_REFLECTION_PROBE_BLENDING)·박스 투영(_REFLECTION_PROBE_BOX_PROJECTION)·
                    //   Forward+ 프로브 아틀라스 처리 → 실내 이동 시 반사 튐 완화. positionWS=박스투영, screenUV=아틀라스 조회.
                    half perceptualRoughness = 1.0h - smoothness;
                    half3 envRefl = GlossyEnvironmentReflection(reflect(-V, N), input.positionWS, perceptualRoughness, 1.0h, GetNormalizedScreenSpaceUV(input.positionCS));
                    shaded += envRefl * metallic * _ReflectionStrength * hqAmount;
                }
                #endif

                // M4(T4-2): Angel Ring (hair anisotropic specular). 2-4: _USE_ANGELRING 토글(기본 ON).
                #if defined(_PART_HAIR) && defined(_USE_ANGELRING)
                if (hqAmount > 0.0h)   // S2: 원거리면 Angel Ring 스킵 + 페이드
                {
                    half3 hairT = (half3)GetUVTangentWS(input.positionWS, input.uv);
                    // 4-6: 링 방향 회전 — 접선 평면(hairT, hairB=N×hairT)에서 _AngelRingAngle만큼 회전.
                    //   기존엔 U 탄젠트 고정이라 UV에 따라 링이 세로로 보였음 → 각도로 세로↔가로 등 조절.
                    half3 hairB = SafeNormalize(cross(N, hairT));
                    half a = radians(_AngelRingAngle);
                    half3 ringT = SafeNormalize(hairT * cos(a) + hairB * sin(a));
                    half3 H = SafeNormalize(L + V);   // L==-V NaN 방지
                    half aniso = 1.0h - saturate(abs(dot(ringT, H)));
                    half ring = pow(aniso, _AngelRingPower) * _AngelRingIntensity;
                    // 4-6: 결 마스크(R) — 머리 결 따라 조절 + 꼭대기/바닥 등 안 보일 영역 제외. 기본 white=no-op.
                    ring *= SAMPLE_TEXTURE2D(_AngelRingMask, sampler_AngelRingMask, input.uv).r;
                    shaded += ring * _AngelRingColor.rgb * hqAmount;
                }
                #endif

                // M4(T4-3): Skin SSS (soft subsurface approximation).
                // 4-4 DEPRECATE(기본 OFF): 터미네이터 붉은 가산 밴드. 하드 셀에선 잘 안 읽혀 그림자 밴드 색으로 대체 권장.
                //   기능 자체는 유지(복구 쉬움) — 합동 시각 세션에서 불필요 확정 시 이 블록+프로퍼티+CBUFFER+GUI 제거.
                #if defined(_PART_SKIN) && defined(_USE_SSS)
                if (hqAmount > 0.0h)   // S2: 원거리면 SSS 스킵 + 페이드
                {
                    half border = 1.0h - saturate(abs(lightVal - _SkinSSSCenter) / _SkinSSSWidth);
                    shaded += border * _SkinSSSColor.rgb * _SkinSSSStrength * hqAmount;
                }
                #endif
                shaded += rimColor;

                // M4(T4-4) + 3-1: 눈 시차(parallax) 오버레이.
                //   미동작 1차 원인 = 키워드 `_USE_EYE_PARALLAX`에 UI 토글이 없어 블록이 스트립됨(2-4에서 토글 노출로 해소).
                //   2차 버그 = 구 코드가 `lerp(shaded, iris, 0.5)`로 눈이 아닌 영역까지 _EyeMap 색으로 치환 →
                //     기본맵 black(built-in 알파=1)일 때 전체가 어두워짐("아이맵 이상/플레인 검정"의 원인).
                //   교정: _EyeMap을 시선 방향으로 시차 이동 후 A 마스크로 가산 합성. 기본 black=가산 0(안전 no-op).
                //   _EyeMap: RGB=오버레이 색(홍채 디테일/캐치라이트), A=적용 마스크. (전체 홍채 치환형 깊이효과는 별도 눈 쉐이더 — Docs/06 3-1)
            #if defined(_USE_EYE_PARALLAX)
                half3 eyeT = (half3)GetUVTangentWS(input.positionWS, input.uv);
                half3 eyeB = cross(N, eyeT);
                half2 viewTS = half2(dot(V, eyeT), dot(V, eyeB));        // 시선 V를 표면 탄젠트 평면에 투영
                float2 eyeUV = input.uv + viewTS * _EyeParallaxStrength; // 시차 UV 이동(깊이감)
                half4 eyeSample = SAMPLE_TEXTURE2D(_EyeMap, sampler_EyeMap, eyeUV);
                shaded += eyeSample.rgb * eyeSample.a * _EyeHighlightStrength;  // A 마스크 가산 오버레이
            #endif

            // Emission (separate _EmissionMap, decision #11 hybrid). LOBBY_HQ 무관, 키워드 off 시 no-op.
            #if defined(_USE_EMISSION)
                half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                shaded += emission;
            #endif

                // URP fog 적용(키워드 off면 MixFog가 no-op). 발광 후·최종 출력 전.
                shaded = MixFog(shaded, input.fogFactor);
                return half4(shaded, baseColor.a);
            }
            ENDHLSL
        }

        // =========================================================
        // Outline (Inverted Hull, Cull Front)  계획서 6장, M3 최종화
        // =========================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            Blend [_SrcBlend] [_DstBlend]   // 2-3: Transparent 머티리얼에선 외곽선도 블렌드
            ZWrite [_ZWrite]
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _ _USE_ILM
            #pragma shader_feature_local _ _USE_OUTLINE   // 2-4: 아웃라인 Use 토글
            #pragma multi_compile_fog

            #include "../ShaderLibrary/ToonInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;   // WP-E: 외곽선 컬러맵 샘플용
                half   fogFactor  : TEXCOORD1;   // 외곽선도 안개에 정합
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
            #if !defined(_USE_OUTLINE)
                // 2-4: 아웃라인 비활성 — 세 정점을 프러스텀 밖 한 점으로 보내 삼각형 전체를 클립(픽셀 0).
                o.positionCS = float4(2.0, 2.0, 2.0, 1.0);
                return o;
            #endif
                o.uv = input.uv;

                // T3-1/T3-2: Use smooth normal from TANGENT.xyz (decision #3), with fallback to normalOS
                float3 smoothNormalOS = input.tangentOS.xyz;
                if (length(smoothNormalOS) < 1e-4)
                    smoothNormalOS = input.normalOS;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(smoothNormalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);

                // T3-5: ILM alpha suppresses outline locally (A channel, keyword _USE_ILM)
                float suppressionMask = 1.0;
            #if defined(_USE_ILM)
                float4 ilm = SAMPLE_TEXTURE2D_LOD(_ILMMap, sampler_ILMMap, input.uv, 0);
                suppressionMask = 1.0 - ilm.a;
            #endif
                // WP-E(요구 #6): 전용 아웃라인 마스크 R=폭 가중(0=해당 부위 외곽선 제거). 기본 white=영향 없음.
                suppressionMask *= SAMPLE_TEXTURE2D_LOD(_OutlineMask, sampler_OutlineMask, input.uv, 0).r;

                // T3-4: Decision #6 FINAL — screen-space thickness stable across distance, FOV, and non-uniform scale
                // Formula: widthWS = _OutlineWidth * 0.01 * positionCS.w / UNITY_MATRIX_P._m11
                //   - positionCS.w scales with distance (constant thickness vs distance)
                //   - / UNITY_MATRIX_P._m11 divides out FOV projection scaling (cot(fov/2))
                //   - TransformObjectToWorldNormal already handles non-uniform scale (inverse-transpose)
                //   Neutral constant 0.01 and normalization tuned so _OutlineWidth=1 ≈ 1-3px typical lobby view
                float fovScale = UNITY_MATRIX_P._m11;  // cot(fov/2); divide to remove FOV scaling
                // WP-E(요구 #7): 거리 굵기 페이드. fade=0이면 화면일정(기존). fade>0이면 FadeStart 너머에서
                //   폭이 얇아짐(원거리 가늘게). saturate로 근거리는 일정하게 유지.
                float distFade = lerp(1.0, saturate(_OutlineFadeStart / max(positionCS.w, 1e-3)), _OutlineDistanceFade);
                float widthWS = _OutlineWidth * 0.01 * positionCS.w / fovScale * distFade;

                // T3-6: _OutlineWidth = 0 fully collapses outline (LOD strategy: material preset width=0)
                positionWS += normalize(normalWS) * widthWS * suppressionMask;

                o.positionCS = TransformWorldToHClip(positionWS);
                o.fogFactor = (half)ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                // WP-E(요구 #6): 외곽선 컬러 텍스처. 기본 white=_OutlineColor 그대로.
                half3 outlineTex = SAMPLE_TEXTURE2D(_OutlineMap, sampler_OutlineMap, input.uv).rgb;
                half3 outCol = _OutlineColor.rgb * outlineTex;
                outCol = MixFog(outCol, input.fogFactor);   // 외곽선도 안개에 정합
                return half4(outCol, _OutlineColor.a);
            }
            ENDHLSL
        }

        // =========================================================
        // ShadowCaster (URP 표준 동작을 ToonInput 레이아웃으로 자체 구현)
        // =========================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "../ShaderLibrary/ToonInput.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionCS = GetShadowPositionHClip(input);
                return o;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // =========================================================
        // DepthOnly
        // =========================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "../ShaderLibrary/ToonInput.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }

        // =========================================================
        // DepthNormals (SSAO/DepthNormals 필요 시. 계획서 2장)
        // =========================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "../ShaderLibrary/ToonInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                return half4(normalize(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    CustomEditor "CharacterToon.Editor.CharacterToonShaderGUI"
    FallBack "Universal Render Pipeline/Lit"
}


