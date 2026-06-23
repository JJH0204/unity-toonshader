Shader "com.blueigloo/SceneToon/Scene"
{
    // 배경 전용 툰 셰이더 — B0 스켈레톤 (Docs/10).
    //   공유 음영 코어(ToonShared, D-3)로 캐릭터와 동일 셀 톤. 4패스 + UnityPerMaterial 단일 레이아웃.
    //   D-1: ForwardOnly 확정(렌더러 구성 캐릭터와 동일). 라이트맵 GI 툰화/노멀맵/식생 바람은 B1~B2.
    Properties
    {
        [Header(Surface)]
        [Enum(Opaque,0,Transparent,1)] _Surface ("Rendering Mode", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull (Off=양면/식생)", Float) = 2
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip (Foliage)", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5

        [Header(Base)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        [Header(Normal)]
        [Toggle(_USE_NORMALMAP)] _UseNormalMap ("Use Normal Map (B1)", Float) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0,2)) = 1.0

        [Header(Toon Shading)]
        [Toggle(_USE_ILM)] _UseILM ("Use ILM (G=shadow bias, A=outline)", Float) = 0
        _ILMMap ("ILM Map (RGBA)", 2D) = "black" {}
        [Toggle(_USE_RAMP)] _UseRamp ("Use Ramp LUT (optional)", Float) = 0
        _RampMap ("Ramp Map", 2D) = "white" {}
        _RampRow ("Ramp Row (V)", Range(0,1)) = 0.5
        _ShadowOffsetScale ("Shadow Offset Scale", Range(0,1)) = 0.2
        _ShadeFloor ("Shade Floor", Range(0,1)) = 0.2
        _AmbientStrength ("Ambient Strength", Range(0,2)) = 1.0

        [Header(Shadow Bands)]
        _ShadowColor ("Shadow Color 1st", Color) = (0.72,0.74,0.82,1)
        _ShadowBorder ("Shadow Border 1st", Range(0,1)) = 0.5
        _ShadowBlur ("Shadow Blur 1st", Range(0,1)) = 0.1
        _Shadow2ndColor ("Shadow Color 2nd", Color) = (0.55,0.57,0.66,1)
        _Shadow2ndBorder ("Shadow Border 2nd", Range(0,1)) = 0.25
        _Shadow2ndBlur ("Shadow Blur 2nd", Range(0,1)) = 0.1
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 1.0
        _ReceiveShadowStrength ("Receive Cast Shadow", Range(0,1)) = 1.0

        [Header(Baked GI Toonify (B1))]
        [HDR] _GIShadeColor ("GI Shade Color", Color) = (0.6,0.62,0.72,1)
        _GIBandCount ("GI Band Count", Range(1,6)) = 3
        _GIBandSoftness ("GI Band Softness", Range(0,0.5)) = 0.1

        [Header(Rim)]
        [Toggle(_USE_RIM)] _UseRim ("Use Rim Light", Float) = 0
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimThreshold ("Rim Threshold", Range(0,1)) = 0.6
        _RimSoftness ("Rim Softness", Range(0,0.5)) = 0.05
        _RimIntensity ("Rim Intensity", Range(0,4)) = 1.0

        [Header(Additional Lights)]
        [Toggle(_USE_ADD_LIGHTS)] _UseAddLights ("Use Additional Lights", Float) = 1
        _AdditionalLightStrength ("Additional Light Strength", Range(0,2)) = 1.0

        [Header(Outline (screenspace renderer feature))]
        [Toggle(_USE_OUTLINE_SUPPRESS)] _UseOutlineSuppress ("Outline Suppress (B3)", Float) = 0
        _OutlineSuppress ("Outline Suppress Amount", Range(0,1)) = 0.0

        [Header(Foliage (B2))]
        [Toggle(_USE_WIND)] _UseWind ("Use Wind Sway", Float) = 0
        _WindParams ("Wind Params (freq,turbulence,flutter,_)", Vector) = (1,0.5,0.3,0)
        _WindStrength ("Wind Strength", Range(0,2)) = 0.0
        _WindSpeed ("Wind Speed", Range(0,5)) = 1.0
        [Toggle(_USE_TRANSLUCENCY)] _UseTranslucency ("Use Leaf Translucency", Float) = 0
        [HDR] _TranslucencyColor ("Translucency Color", Color) = (0.3,0.5,0.2,1)
        _TranslucencyStrength ("Translucency Strength", Range(0,2)) = 0.0

        [Header(Surface Detail (B4))]
        [Toggle(_USE_TRIPLANAR)] _UseTriplanar ("Use Triplanar (rock/cliff)", Float) = 0
        _TriplanarScale ("Triplanar Scale", Range(0.01,4)) = 0.5
        _TriplanarBlend ("Triplanar Blend Sharpness", Range(1,16)) = 4.0
        [Toggle(_USE_VERTEXCOLOR_BLEND)] _UseVertexBlend ("Use Vertex Color 2-Albedo Blend", Float) = 0
        _LayerMap ("Layer 2 Albedo (moss/snow/dirt)", 2D) = "white" {}
        [HDR] _LayerColor ("Layer 2 Tint", Color) = (1,1,1,1)
        [Enum(R,0,G,1,B,2,A,3)] _LayerBlendChannel ("Blend Vertex Channel", Float) = 0
        [Toggle] _UseOcclusion ("Use Occlusion (AO)", Float) = 0
        _OcclusionMap ("Occlusion (R)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        // =========================================================
        // SceneForward — D-1: UniversalForwardOnly (Forward/Forward+/Deferred 전 경로 안전)
        // =========================================================
        Pass
        {
            Name "SceneForward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull [_Cull]
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED

            #pragma shader_feature_local _ _USE_NORMALMAP
            #pragma shader_feature_local _ _ALPHATEST_ON
            #pragma shader_feature_local _ _USE_ILM
            #pragma shader_feature_local _ _USE_RAMP
            #pragma shader_feature_local _ _USE_RIM
            #pragma shader_feature_local _ _USE_ADD_LIGHTS
            #pragma shader_feature_local _ _USE_WIND
            #pragma shader_feature_local _ _USE_TRANSLUCENCY
            #pragma shader_feature_local _ _USE_TRIPLANAR
            #pragma shader_feature_local _ _USE_VERTEXCOLOR_BLEND

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "../ShaderLibrary/SceneToonInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float4 color      : COLOR;       // B4: 정점컬러(2-알베도 블렌딩)
                float2 uv         : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                half   fogFactor  : TEXCOORD3;
                half4  tangentWS  : TEXCOORD4;   // xyz=tangent(WS), w=bitangent 부호
                half4  vertexColor : TEXCOORD6;  // B4
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                posWS = ApplyWindWS(posWS, input.positionOS.xyz);          // 식생 바람(키워드 off=no-op)
                VertexNormalInputs   n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS   = n.normalWS;
                half tsign = (half)(input.tangentOS.w * GetOddNegativeScale());
                o.tangentWS = half4(n.tangentWS, tsign);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.vertexColor = (half4)input.color;
                o.fogFactor = (half)ComputeFogFactor(o.positionCS.z);
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, o.staticLightmapUV);
                OUTPUT_SH(o.normalWS, o.vertexSH);
                return o;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE cullFace : FRONT_FACE_SEMANTIC) : SV_TARGET
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

            #if defined(_USE_TRIPLANAR)
                // 월드 트라이플래너로 알베도 대체(바위/절벽/메시 지형 UV 스트레치 제거). 지오메트릭 노멀로 블렌드.
                baseColor.rgb = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap),
                                input.positionWS, normalize(input.normalWS), _TriplanarScale, _TriplanarBlend) * _BaseColor.rgb;
            #endif
            #if defined(_USE_VERTEXCOLOR_BLEND)
                // 정점컬러 채널로 2차 레이어(이끼/눈/오염) 블렌딩.
                half3 layer = SAMPLE_TEXTURE2D(_LayerMap, sampler_LayerMap, input.uv).rgb * _LayerColor.rgb;
                half vblend = saturate(SelectVertexChannel(input.vertexColor, _LayerBlendChannel));
                baseColor.rgb = lerp(baseColor.rgb, layer, vblend);
            #endif
            #if defined(_ALPHATEST_ON)
                clip(baseColor.a - _Cutoff);
            #endif

                half facing = IS_FRONT_VFACE(cullFace, 1.0h, -1.0h);   // 양면(식생) 뒷면 노멀 뒤집기
                half3 N = normalize(input.normalWS) * facing;
            #if defined(_USE_NORMALMAP)
                // 배경 정적 메시는 메시 탄젠트가 있으므로 표준 TBN 노멀매핑(캐릭터의 미분 방식과 달리 탄젠트 사용).
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 Tn = normalize(input.tangentWS.xyz);
                half3 Bn = normalize(cross(N, Tn) * input.tangentWS.w);
                N = normalize(mul(normalTS, half3x3(Tn, Bn, N)));
            #endif
                half3 V = normalize(GetWorldSpaceViewDir(input.positionWS));

                // 메인 라이트 + 그림자 수신(배경 1순위)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 L = mainLight.direction;

                // ILM.G 그림자 편향(배경은 G/A만 사용). 누락 시 중립값.
                half4 ilm = half4(0.0h, 0.5h, 0.5h, 0.0h);
            #if defined(_USE_ILM)
                ilm = SAMPLE_TEXTURE2D(_ILMMap, sampler_ILMMap, input.uv);
            #endif

                half hl = ToonHalfLambert(dot(N, L));                 // ToonShared/ToonRamp
                half shadowBias = (ilm.g - 0.5h) * _ShadowOffsetScale;
                half lightVal = ToonRampU(hl, shadowBias);
                half rcvShadow = lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadowStrength);
                lightVal *= rcvShadow;

                // 단일 음영 단계: Ramp LUT(옵션) 또는 파라메트릭 1·2차 밴드(공유 코어)
                half3 shadedAlbedo;
                half  shadeMask;   // 0=빛, 1=그림자
            #if defined(_USE_RAMP)
                half3 ramp = SampleToonRamp(TEXTURE2D_ARGS(_RampMap, sampler_RampMap), lightVal, _RampRow);
                shadedAlbedo = baseColor.rgb * ramp;
                shadeMask = 1.0h - saturate(lightVal);
            #else
                shadedAlbedo = ToonShadeBands(
                    baseColor.rgb, lightVal,
                    _ShadowBorder,    _ShadowBlur,    _ShadowColor.rgb,
                    _Shadow2ndBorder, _Shadow2ndBlur, _Shadow2ndColor.rgb,
                    _ShadowStrength, shadeMask);                       // ToonShared/ToonBandShadow
            #endif

                half3 shaded = shadedAlbedo * mainLight.color;

                // 베이크 GI 툰화 (D-2): 라이트맵/SH irradiance 를 밴드로 양자화해 셀 톤화.
                //   SAMPLE_GI 3-arg = LIGHTMAP_ON 이면 라이트맵, 아니면 per-vertex SH. 휘도만 계단화, 색조 보존.
                half3 bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, N);
                half giLum  = max(dot(bakedGI, half3(0.2126h, 0.7152h, 0.0722h)), 1e-4h);
                half giBand = ToonPosterize(giLum, _GIBandCount, _GIBandSoftness);   // ToonShared/ToonBandShadow
                half3 toonGI = bakedGI * (giBand / giLum);                           // 휘도 계단화, 색조 유지
                toonGI *= lerp(_GIShadeColor.rgb, half3(1.0h, 1.0h, 1.0h), giBand);  // 그림자 단계 틴트
                // B4: AO(간접광에만) — 키워드 아닌 float 분기(off면 페치 스킵).
                half occ = 1.0h;
                if (_UseOcclusion > 0.5h)
                    occ = lerp(1.0h, SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r, _OcclusionStrength);
                half3 gi = toonGI * baseColor.rgb * _AmbientStrength;
                shaded += gi * (1.0h - shadeMask) * occ;                             // 그림자 영역엔 줄여 더해 셀 대비 보존
                shaded = max(shaded, baseColor.rgb * _ShadeFloor);                   // 그림자 최소 밝기 하한

                // 추가광(point/spot) 셀셰이딩 — Forward+ 클러스터 포함. 가산광이라 그림자 영역은 안 어둡게.
            #if defined(_ADDITIONAL_LIGHTS) && defined(_USE_ADD_LIGHTS)
                {
                    half addBlur = max(_ShadowBlur, 1e-4h);
                    half3 addSum = (half3)0.0h;
                    uint addCount = GetAdditionalLightsCount();
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    LIGHT_LOOP_BEGIN(addCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS);
                        half nl  = saturate(dot(N, light.direction) * 0.5h + 0.5h);
                        half lit = smoothstep(_ShadowBorder - addBlur, _ShadowBorder + addBlur, nl);
                        half atten = light.distanceAttenuation * light.shadowAttenuation;
                        addSum += baseColor.rgb * light.color * (atten * lit);
                    LIGHT_LOOP_END
                    shaded += addSum * _AdditionalLightStrength;
                }
            #endif

            #if defined(_USE_TRANSLUCENCY)
                // 잎 투과(backlight): 빛이 잎 뒤에서 비칠 때 통과광. 셀 톤이라 부드러운 SSS 대신 단순 밴드.
                half vdl = saturate(dot(-V, L));
                half trans = smoothstep(0.3h, 1.0h, vdl) * rcvShadow;
                shaded += _TranslucencyColor.rgb * (trans * _TranslucencyStrength) * mainLight.color;
            #endif

            #if defined(_USE_RIM)
                shaded += ToonRim(N, V, _RimThreshold, _RimSoftness, _RimColor.rgb, _RimIntensity); // ToonShared/ToonRim
            #endif

                shaded = MixFog(shaded, input.fogFactor);
                return half4(shaded, baseColor.a);
            }
            ENDHLSL
        }

        // =========================================================
        // ShadowCaster
        // =========================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma shader_feature_local _ _ALPHATEST_ON
            #pragma shader_feature_local _ _USE_WIND

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "../ShaderLibrary/SceneToonInput.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 GetShadowClip(float3 positionWS, float3 normalWS)
            {
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return cs;
            }

            Varyings ShadowVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                posWS = ApplyWindWS(posWS, input.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionCS = GetShadowClip(posWS, nrmWS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
            #if defined(_ALPHATEST_ON)
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(a - _Cutoff);
            #endif
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ _ALPHATEST_ON
            #pragma shader_feature_local _ _USE_WIND

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/SceneToonInput.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                posWS = ApplyWindWS(posWS, input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 DepthFrag(Varyings input) : SV_TARGET
            {
            #if defined(_ALPHATEST_ON)
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        // =========================================================
        // DepthNormals — 스크린스페이스 아웃라인(풀스크린 피처)이 배경 엣지를 잡는 핵심 (Docs/10 §6)
        // =========================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma shader_feature_local _ _ALPHATEST_ON
            #pragma shader_feature_local _ _USE_WIND
            #pragma shader_feature_local _ _USE_OUTLINE_SUPPRESS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../ShaderLibrary/SceneToonInput.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                posWS = ApplyWindWS(posWS, input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_TARGET
            {
            #if defined(_ALPHATEST_ON)
                half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a;
                clip(a - _Cutoff);
            #endif
                // w 채널 = 스크린스페이스 아웃라인 per-object 억제(B3). 기본 0=외곽선 그림, 1=제거.
                half supp = 0.0h;
            #if defined(_USE_OUTLINE_SUPPRESS)
                supp = saturate(_OutlineSuppress);
            #endif
                return half4(normalize(input.normalWS), supp);   // xyz=월드 노멀, w=외곽선 억제
            }
            ENDHLSL
        }

        // =========================================================
        // Meta — 라이트맵/GI 베이크용 알베도(+emission) 출력. 정적 배경 GI 의 전제.
        // =========================================================
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma vertex UniversalVertexMeta
            #pragma fragment SceneMetaFrag
            #pragma shader_feature EDITOR_VISUALIZATION
            #pragma shader_feature_local _ _ALPHATEST_ON

            // SceneToonInput 가 _BaseMap/_BaseMap_ST 를 먼저 선언해야 UniversalVertexMeta 의 TRANSFORM_TEX 가 성립.
            #include "../ShaderLibrary/SceneToonInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

            half4 SceneMetaFrag(Varyings input) : SV_TARGET
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(baseColor.a - _Cutoff);
            #endif
                MetaInput metaInput = (MetaInput)0;
                metaInput.Albedo = baseColor.rgb;
                metaInput.Emission = half3(0.0h, 0.0h, 0.0h);
                return UniversalFragmentMeta(input, metaInput);
            }
            ENDHLSL
        }
    }

    CustomEditor "SceneToon.Editor.SceneToonShaderGUI"
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
