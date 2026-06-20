using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace CharacterToon.Editor
{
    /// <summary>
    /// M5 (T5-1, 결정 #10): CharacterToon/Character 셰이더의 빌드 변형(variant) 스트리퍼.
    ///
    /// 현재 정책:
    ///  - _DEBUG_FACELIT 은 에디터 진단 전용 -> 플레이어 빌드에서 항상 제거.
    ///  - (TODO) 출하 material preset 조합이 확정되면, 실제로 쓰지 않는
    ///    _PART_* / _USE_MATCAP / _USE_EYE_PARALLAX / _USE_HAIR_SHADOW / _USE_ILM / _USE_EMISSION
    ///    조합을 아래 allowlist 로직에 추가해 변형 수를 더 줄인다.
    ///    (LOBBY_HQ 품질-티어 키워드는 결정 #15로 제거됨 — 단일 고품질)
    ///
    /// 빌드 전 변형 수 확인은 URP 설정(Graphics > URP Global Settings 의 Shader Stripping)과
    /// 이 콜백 로그를 함께 본다. (T5-2)
    /// </summary>
    class CharacterToonShaderStripper : IPreprocessShaders
    {
        // 낮을수록 먼저 실행. URP 기본 스트리퍼(order 0)보다 먼저 돌도록 음수.
        public int callbackOrder => -100;

        private const string TargetShaderName = "CharacterToon/Character";

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || shader.name != TargetShaderName)
                return;

            // 로컬 키워드는 셰이더 스코프로 조회해야 정확.
            var debugFaceLit = new ShaderKeyword(shader, "_DEBUG_FACELIT");

            int removed = 0;
            for (int i = data.Count - 1; i >= 0; i--)
            {
                if (data[i].shaderKeywordSet.IsEnabled(debugFaceLit))
                {
                    data.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Debug.Log($"[CharacterToonShaderStripper] '{shader.name}' pass '{snippet.passName}' " +
                          $"({snippet.shaderType}): _DEBUG_FACELIT 변형 {removed}개 제거.");
            }
        }
    }
}
