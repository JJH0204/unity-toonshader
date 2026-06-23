using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace CharacterToon
{
    /// <summary>
    /// S1(갭): NiloToon식 all-in-one ScriptableRendererFeature.
    ///  1) 카메라별 렌더 루프에서 퍼-캐릭터 툰 라이트 글로벌 통합 주입([[CharacterToonManager]]).
    ///  2) O1(갭): 깊이/노멀 엣지 기반 스크린스페이스 아웃라인 풀스크린 패스(불투명 이후).
    ///
    /// 사용법: URP 렌더러 에셋(PC_Renderer / Mobile_Renderer) Inspector에서
    /// "Add Renderer Feature → Character Toon Renderer Feature" 추가 후, Outline 섹션에
    /// Hidden/CharacterToon/ScreenSpaceOutline 셰이더를 지정하고 Enabled 체크.
    /// (렌더러의 RendererFeature는 서브에셋 → 코드 자동 등록하지 않음. 사용자 1회 추가.)
    /// </summary>
    [DisallowMultipleRendererFeature("Character Toon Renderer Feature")]
    public class CharacterToonRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class OutlineSettings
        {
            public bool enabled = false;
            [Tooltip("Hidden/CharacterToon/ScreenSpaceOutline 셰이더를 지정.")]
            public Shader shader;
            [ColorUsage(true, true)] public Color color = Color.black;
            [Min(0f)] public float thickness = 1.0f;
            [Min(0f)] public float depthThreshold = 0.2f;
            [Min(0f)] public float normalThreshold = 0.4f;
            [Range(0f, 1f), Tooltip("0=절대 깊이차(기존). 1=뷰깊이 비례 — 배경의 큰 깊이폭에서 원/근 외곽선 일관(B3).")]
            public float depthScale = 0f;
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public OutlineSettings outline = new OutlineSettings();

        private Material _outlineMaterial;
        private ScreenSpaceOutlinePass _outlinePass;

        public override void Create()
        {
            if (outline.shader != null)
            {
                _outlineMaterial = CoreUtils.CreateEngineMaterial(outline.shader);
                _outlinePass = new ScreenSpaceOutlinePass(_outlineMaterial);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // S1: 카메라별로 퍼-캐릭터 라이트 리그를 전역 반영(플레이/빌드 타이밍 정확성).
            CharacterToonManager.PushActive();

            // O1: 스크린스페이스 아웃라인
            if (outline.enabled && _outlinePass != null && _outlineMaterial != null)
            {
                // 게임/씬 카메라에만 적용(프리뷰/리플렉션 제외).
                var cameraType = renderingData.cameraData.cameraType;
                if (cameraType == CameraType.Game || cameraType == CameraType.SceneView)
                {
                    _outlineMaterial.SetColor(ShaderIds.OutlineSSColor, outline.color);
                    _outlineMaterial.SetFloat(ShaderIds.OutlineSSThickness, outline.thickness);
                    _outlineMaterial.SetFloat(ShaderIds.OutlineSSDepthThreshold, outline.depthThreshold);
                    _outlineMaterial.SetFloat(ShaderIds.OutlineSSNormalThreshold, outline.normalThreshold);
                    _outlineMaterial.SetFloat(ShaderIds.OutlineSSDepthScale, outline.depthScale);
                    _outlinePass.renderPassEvent = outline.renderPassEvent;
                    renderer.EnqueuePass(_outlinePass);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_outlineMaterial);
            _outlineMaterial = null;
            _outlinePass = null;
        }

        private static class ShaderIds
        {
            public static readonly int OutlineSSColor           = Shader.PropertyToID("_OutlineSSColor");
            public static readonly int OutlineSSThickness       = Shader.PropertyToID("_OutlineSSThickness");
            public static readonly int OutlineSSDepthThreshold  = Shader.PropertyToID("_OutlineSSDepthThreshold");
            public static readonly int OutlineSSNormalThreshold = Shader.PropertyToID("_OutlineSSNormalThreshold");
            public static readonly int OutlineSSDepthScale       = Shader.PropertyToID("_OutlineSSDepthScale");
        }

        /// <summary>O1: 깊이/노멀을 읽어 풀스크린 엣지 합성. 카메라 컬러를 임시 텍스처로 블릿 후 스왑.</summary>
        private sealed class ScreenSpaceOutlinePass : ScriptableRenderPass
        {
            private readonly Material _material;

            public ScreenSpaceOutlinePass(Material material)
            {
                _material = material;
                profilingSampler = new ProfilingSampler("CharacterToon SS Outline");
                // blit-and-swap 패턴은 중간 컬러 텍스처가 필요(백버퍼 직행 금지) — Codex 지적.
                requiresIntermediateTexture = true;
                // 깊이/노멀 텍스처 생성·바인딩 요청(_CameraDepthTexture / _CameraNormalsTexture).
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer) return; // 중간 컬러 텍스처 필요(백버퍼 직행 시 스킵)

                TextureHandle source = resourceData.activeColorTexture;

                TextureDesc desc = source.GetDescriptor(renderGraph);
                desc.name = "_CharacterToonOutlineTemp";
                desc.clearBuffer = false;
                desc.depthBufferBits = 0;
                TextureHandle dest = renderGraph.CreateTexture(desc);

                // 소스를 _BlitTexture로 바인딩하여 아웃라인 머티리얼로 풀스크린 블릿 → dest.
                RenderGraphUtils.BlitMaterialParameters blit =
                    new RenderGraphUtils.BlitMaterialParameters(source, dest, _material, 0);
                renderGraph.AddBlitPass(blit, "CharacterToon SS Outline");

                // 카피백 없이 활성 컬러를 결과로 스왑(URP 권장 패턴).
                resourceData.cameraColor = dest;
            }
        }
    }
}
