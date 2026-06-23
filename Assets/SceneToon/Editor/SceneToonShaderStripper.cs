using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace SceneToon.Editor
{
    /// <summary>
    /// B5: SceneToon/Scene 셰이더 빌드 변형(variant) 스트리퍼. (CharacterToonShaderStripper 패턴)
    ///
    /// 현재 정책(안전 — fallback 픽셀 동일):
    ///  - `DIRLIGHTMAP_COMBINED ∧ ¬LIGHTMAP_ON`: 방향 라이트맵은 라이트맵이 켜져야만 의미.
    ///    라이트맵 off 변형에선 SH 경로라 DIRLIGHTMAP_COMBINED 가 무시됨 → 빼도 동일 픽셀.
    ///  - (TODO) 출하 material preset 조합(StaticOpaque/FoliageCutout/Rock_Triplanar/Terrain…)이 확정되면
    ///    allowlist 로 _USE_TRIPLANAR/_USE_WIND/_USE_TRANSLUCENCY/_USE_VERTEXCOLOR_BLEND 미사용 조합을 추가 제거.
    ///
    /// 빌드 전 변형 수는 URP Global Settings 의 Shader Stripping + 이 콜백 로그를 함께 본다.
    /// </summary>
    class SceneToonShaderStripper : IPreprocessShaders
    {
        public int callbackOrder => -100; // URP 기본 스트리퍼(0)보다 먼저.

        private const string TargetShaderName = "SceneToon/Scene";

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || shader.name != TargetShaderName)
                return;

            var kLightmap = new ShaderKeyword(shader, "LIGHTMAP_ON");
            var kDirLm    = new ShaderKeyword(shader, "DIRLIGHTMAP_COMBINED");

            int removed = 0;
            for (int i = data.Count - 1; i >= 0; i--)
            {
                var ks = data[i].shaderKeywordSet;

                // 방향 라이트맵 키워드가 켜졌는데 라이트맵 자체가 꺼진 불가능/잉여 변형 제거.
                bool strip = ks.IsEnabled(kDirLm) && !ks.IsEnabled(kLightmap);

                if (strip)
                {
                    data.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Debug.Log($"[SceneToonShaderStripper] '{shader.name}' pass '{snippet.passName}' " +
                          $"({snippet.shaderType}): 변형 {removed}개 제거(DIRLIGHTMAP∧¬LIGHTMAP).");
            }
        }
    }
}
