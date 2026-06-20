Shader "Hidden/CharacterToon/ScreenSpaceOutline"
{
    // O1(갭): 깊이/노멀 엣지 기반 스크린스페이스 아웃라인. CharacterToonRendererFeature가 풀스크린 블릿으로 구동.
    // _BlitTexture(소스 컬러)를 그대로 통과시키되, 깊이/노멀 불연속(엣지)에 _OutlineSSColor를 합성한다.
    // 깊이/노멀 텍스처는 패스가 ConfigureInput(Depth|Normal)로 생성·바인딩한다.
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "CharacterToonScreenSpaceOutline"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _OutlineSSColor;          // rgb=색, a=합성 강도
            float  _OutlineSSThickness;      // 픽셀 단위 샘플 간격
            float  _OutlineSSDepthThreshold; // 깊이 엣지 임계(뷰 공간 거리)
            float  _OutlineSSNormalThreshold;// 노멀 엣지 임계

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);   // 싱글패스 인스턴스 VR 슬라이스 정합
                float2 uv = input.texcoord;
                half4 srcColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float2 t = _OutlineSSThickness / _ScreenParams.xy;

                // 깊이 엣지(Roberts 근사) — 뷰 공간 선형 깊이 차
                float dC  = LinearEyeDepth(SampleSceneDepth(uv),                  _ZBufferParams);
                float dX  = LinearEyeDepth(SampleSceneDepth(uv + float2(t.x, 0)), _ZBufferParams);
                float dY  = LinearEyeDepth(SampleSceneDepth(uv + float2(0, t.y)), _ZBufferParams);
                float dXY = LinearEyeDepth(SampleSceneDepth(uv + t),             _ZBufferParams);
                float depthEdge = abs(dC - dX) + abs(dC - dY) + abs(dC - dXY);

                // 노멀 엣지 — 월드 노멀 차
                half3 nC = SampleSceneNormals(uv);
                half3 nX = SampleSceneNormals(uv + float2(t.x, 0));
                half3 nY = SampleSceneNormals(uv + float2(0, t.y));
                float normalEdge = distance(nC, nX) + distance(nC, nY);

                float edge = saturate(step(_OutlineSSDepthThreshold,  depthEdge)
                                    + step(_OutlineSSNormalThreshold, normalEdge));

                return lerp(srcColor, _OutlineSSColor, edge * _OutlineSSColor.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
