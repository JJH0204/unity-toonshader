using System.Collections.Generic;
using UnityEngine;

namespace CharacterToon
{
    /// <summary>
    /// S1(갭): 퍼-캐릭터 툰 라이트(L1)의 중앙 레지스트리 + 글로벌 주입 관리.
    /// 흩어져 있던 Shader.SetGlobalVector 호출을 한 곳으로 모은다(NiloToon식 all-in-one의 컴포넌트판).
    ///
    /// 모델: **단일-활성(highest priority)**. 글로벌 주입 특성상 한 프레임에 하나의 캐릭터 라이트 리그만
    /// 전역 반영된다. 1인칭 로비 쇼케이스(포커스 캐릭터 1)와 쿼터뷰(대표 라이트)에 충분.
    /// 다수 캐릭터 동시 퍼-렌더러 오버라이드는 향후 MaterialPropertyBlock/퍼-드로우 글로벌 스왑으로 확장(백로그).
    ///
    /// 글로벌(UnityPerMaterial 밖, SRP Batcher 호환):
    ///   _CharacterLightDirWS  (xyz=방향, w&gt;0.5=캐릭터 라이트 사용)
    ///   _CharacterLightColor  (rgb=색(linear), a=세기. a&lt;=0 이면 셰이더가 메인 라이트 색 사용)
    /// </summary>
    public static class CharacterToonManager
    {
        private static readonly List<CharacterToonLight> _lights = new List<CharacterToonLight>();

        private static readonly int IdLightDir   = Shader.PropertyToID("_CharacterLightDirWS");
        private static readonly int IdLightColor = Shader.PropertyToID("_CharacterLightColor");

        public static void Register(CharacterToonLight light)
        {
            if (light != null && !_lights.Contains(light))
                _lights.Add(light);
        }

        public static void Unregister(CharacterToonLight light)
        {
            _lights.Remove(light);
            if (_lights.Count == 0)
                ClearGlobals();
        }

        /// <summary>최고 우선순위의 활성 라이트 리그를 글로벌로 push. 없으면 메인 라이트 폴백으로 초기화.</summary>
        public static void PushActive()
        {
            CharacterToonLight best = null;
            int bestPriority = int.MinValue;
            for (int i = 0; i < _lights.Count; i++)
            {
                CharacterToonLight l = _lights[i];
                if (l == null || !l.isActiveAndEnabled) continue;
                if (l.Priority > bestPriority) { bestPriority = l.Priority; best = l; }
            }

            if (best == null) { ClearGlobals(); return; }
            best.ApplyGlobals(IdLightDir, IdLightColor);
        }

        private static void ClearGlobals()
        {
            Shader.SetGlobalVector(IdLightDir, new Vector4(0f, 1f, 0f, 0f));  // w=0 → 셰이더가 메인 라이트로 폴백
            Shader.SetGlobalVector(IdLightColor, Vector4.zero);              // a=0 → 메인 라이트 색 사용
        }
    }
}
