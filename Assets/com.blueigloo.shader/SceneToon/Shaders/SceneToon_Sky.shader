Shader "com.blueigloo/SceneToon/Sky"
{
    // 배경막 스카이 돔/평면 — B2 초안 (Docs/10 §7.4, D-7).
    //   절차적 2~3색 그라데이션 unlit. 돔/구 메시 안쪽에 적용해 사용. 깊이 최후단(ZWrite Off, Queue Background),
    //   DepthNormals 미출력 → 스크린스페이스 아웃라인 비대상. 기존 URP 스카이박스와 병행/대체 모두 가능.
    Properties
    {
        [HDR] _SkyZenithColor  ("Zenith Color (위)",   Color) = (0.35,0.55,0.85,1)
        [HDR] _SkyHorizonColor ("Horizon Color (수평)", Color) = (0.8,0.85,0.9,1)
        [HDR] _SkyGroundColor  ("Ground Color (아래)",  Color) = (0.5,0.48,0.45,1)
        _SkyHorizonSharp ("Horizon Sharpness", Range(0.1,8)) = 2.0
        _SkyExposure ("Exposure", Range(0,4)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Background" "Queue" = "Background" }

        Pass
        {
            Name "SceneSky"
            Tags { "LightMode" = "UniversalForwardOnly" }

            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyZenithColor;
                half4 _SkyHorizonColor;
                half4 _SkyGroundColor;
                half  _SkyHorizonSharp;
                half  _SkyExposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 dirWS : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                o.dirWS = posWS - _WorldSpaceCameraPos;   // 카메라→정점 방향(돔 표면)
                return o;
            }

            half4 frag(Varyings input) : SV_TARGET
            {
                half3 dir = normalize(input.dirWS);
                half h = dir.y;                                              // -1(아래)..1(위)
                // 수평(0) 기준 위/아래 그라데이션. sharpness 로 수평 띠 폭 제어.
                half up   = saturate(pow(saturate( h), 1.0h / _SkyHorizonSharp));
                half down = saturate(pow(saturate(-h), 1.0h / _SkyHorizonSharp));
                half3 col = _SkyHorizonColor.rgb;
                col = lerp(col, _SkyZenithColor.rgb, up);
                col = lerp(col, _SkyGroundColor.rgb, down);
                return half4(col * _SkyExposure, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
