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
    ///  - 파트 불일치 '불가능 조합' 제거(셰이더 #if로 이미 dead라 안전, fallback 동일 픽셀):
    ///      _USE_HAIR_SHADOW ∧ ¬_PART_FACE / _USE_ANGELRING ∧ ¬_PART_HAIR / _USE_SSS ∧ ¬_PART_SKIN.
    ///  - (TODO) 출하 material preset 조합이 확정되면 추가 allowlist(MatCap/Eye/Emission 등)로 더 줄인다.
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
            var kDebug      = new ShaderKeyword(shader, "_DEBUG_FACELIT");
            var kPartFace   = new ShaderKeyword(shader, "_PART_FACE");
            var kPartHair   = new ShaderKeyword(shader, "_PART_HAIR");
            var kPartSkin   = new ShaderKeyword(shader, "_PART_SKIN");
            var kHairShadow = new ShaderKeyword(shader, "_USE_HAIR_SHADOW");
            var kAngelRing  = new ShaderKeyword(shader, "_USE_ANGELRING");
            var kSSS        = new ShaderKeyword(shader, "_USE_SSS");

            int removed = 0;
            for (int i = data.Count - 1; i >= 0; i--)
            {
                var ks = data[i].shaderKeywordSet;

                // (1) 에디터 진단 전용 — 플레이어 빌드 항상 제거.
                bool strip = ks.IsEnabled(kDebug);

                // (2) 불가능/잉여 조합 — 셰이더 #if 가드로 이미 dead 코드인 변형.
                //   _USE_HAIR_SHADOW 는 _PART_FACE 안, _USE_ANGELRING 는 _PART_HAIR 안, _USE_SSS 는 _PART_SKIN 안에서만 동작.
                //   파트 불일치 변형은 기능이 컴파일 제거된 상태라, 빌드에서 빼도 런타임 fallback(기능 OFF 변형)이
                //   '동일 픽셀'을 내므로 마젠타/룩 변화 없이 안전하게 변형 수만 줄인다.
                if (!strip && ks.IsEnabled(kHairShadow) && !ks.IsEnabled(kPartFace)) strip = true;
                if (!strip && ks.IsEnabled(kAngelRing)  && !ks.IsEnabled(kPartHair)) strip = true;
                if (!strip && ks.IsEnabled(kSSS)        && !ks.IsEnabled(kPartSkin)) strip = true;

                if (strip)
                {
                    data.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                Debug.Log($"[CharacterToonShaderStripper] '{shader.name}' pass '{snippet.passName}' " +
                          $"({snippet.shaderType}): 변형 {removed}개 제거(디버그 + 파트 불일치 조합).");
            }
        }
    }
}
