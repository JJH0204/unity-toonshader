using System.Collections.Generic;
using UnityEngine;

namespace CharacterToon
{
    /// <summary>
    /// S1(갭): 퍼-캐릭터 툰 라이트(L1)의 중앙 레지스트리 + 글로벌 주입 관리.
    /// 흩어져 있던 Shader.SetGlobalVector 호출을 한 곳으로 모은다(NiloToon식 all-in-one의 컴포넌트판).
    ///
    /// 모델: **다중 라이트, 결정적 순서로 겹쳐 적용**. priority 내림차순으로 정렬해
    ///   1순위 = cel/SDF 구동 키라이트(전역 1개), 2순위 이하 = 가산 셀 기여 배열(최대 MAX)로 합산.
    ///   동률 priority는 name→instanceID 폴백으로 결정적 정렬하고, 동률 발견 시 경고를 1회 남긴다.
    ///   (다수 캐릭터별 서로 다른 리그 동시 적용은 글로벌 주입 한계로 여전히 백로그.)
    ///
    /// 글로벌(UnityPerMaterial 밖, SRP Batcher 호환):
    ///   _CharacterLightDirWS / _CharacterLightColor  — 1순위 키라이트(메인 라이트 폴백 규칙)
    ///   _CharacterExtraLightDir[] / _CharacterExtraLightColor[] / _CharacterExtraLightCount — 2순위 이하 가산광
    /// </summary>
    public static class CharacterToonManager
    {
        // 셰이더 CHARACTER_EXTRA_LIGHT_MAX 와 동일해야 함(2순위 이하 가산 라이트 최대 개수).
        public const int CharacterExtraLightMax = 3;

        private static readonly List<CharacterToonLight> _lights  = new List<CharacterToonLight>();
        private static readonly List<CharacterToonLight> s_active = new List<CharacterToonLight>();
        private static readonly Vector4[] s_extraDir   = new Vector4[CharacterExtraLightMax];
        private static readonly Vector4[] s_extraColor = new Vector4[CharacterExtraLightMax];
        private static bool _tieWarned;

        private static readonly int IdLightDir    = Shader.PropertyToID("_CharacterLightDirWS");
        private static readonly int IdLightColor  = Shader.PropertyToID("_CharacterLightColor");
        private static readonly int IdExtraDir    = Shader.PropertyToID("_CharacterExtraLightDir");
        private static readonly int IdExtraColor  = Shader.PropertyToID("_CharacterExtraLightColor");
        private static readonly int IdExtraCount  = Shader.PropertyToID("_CharacterExtraLightCount");

        public static void Register(CharacterToonLight light)
        {
            if (light != null && !_lights.Contains(light))
            {
                _lights.Add(light);
                _tieWarned = false;   // 구성 변경 → 동률 재평가
            }
        }

        public static void Unregister(CharacterToonLight light)
        {
            _lights.Remove(light);
            _tieWarned = false;
            if (_lights.Count == 0)
                ClearGlobals();
        }

        /// <summary>
        /// 활성 라이트를 '결정적 순서'로 정렬해 적용:
        ///   1순위 → cel/SDF 구동 키라이트(전역 _CharacterLightDirWS/Color, 메인 라이트 폴백 규칙 유지)
        ///   2순위 이하 → 가산 셀 기여 배열(_CharacterExtraLight*)로 순서대로 겹쳐 적용(최대 MAX).
        /// 정렬: priority 내림차순 → name 오름차순 → instanceID 오름차순(완전 결정적). 동률 시 1회 경고.
        /// </summary>
        public static void PushActive()
        {
            s_active.Clear();
            for (int i = 0; i < _lights.Count; i++)
            {
                CharacterToonLight l = _lights[i];
                if (l != null && l.isActiveAndEnabled) s_active.Add(l);
            }
            if (s_active.Count == 0) { ClearGlobals(); return; }

            s_active.Sort(CompareLights);

            // 동률 경고: priority가 같은 라이트가 있으면 순서가 모호 → 구성/상태 바뀔 때만 1회 로그(스팸 방지).
            bool hasTie = false;
            for (int i = 1; i < s_active.Count; i++)
                if (s_active[i].Priority == s_active[i - 1].Priority) { hasTie = true; break; }
            if (hasTie && !_tieWarned)
            {
                _tieWarned = true;
                Debug.LogWarning("[CharacterToonManager] 같은 Priority의 CharacterToonLight가 있어 적용/겹침 순서가 모호합니다. " +
                    "결정적 폴백(name→instanceID)으로 정렬되지만, 의도한 순서를 위해 서로 다른 Priority를 지정하세요.");
            }
            else if (!hasTie)
            {
                _tieWarned = false;
            }

            // 1순위: 기존 동작 보존(방향 무효 w=0 이면 셰이더가 메인 라이트로 폴백).
            s_active[0].GetRig(out Vector4 dir0, out Vector4 col0);
            Shader.SetGlobalVector(IdLightDir, dir0);
            Shader.SetGlobalVector(IdLightColor, col0);

            // 2순위 이하: 방향+색(세기>0)이 유효한 것만 결정적 순서로 가산 배열에.
            int extra = 0;
            for (int i = 1; i < s_active.Count && extra < CharacterExtraLightMax; i++)
            {
                bool hasDir = s_active[i].GetRig(out Vector4 d, out Vector4 c);
                if (!hasDir || c.w <= 0f) continue;   // 방향/세기 없으면 가산 의미 없음 → 스킵
                s_extraDir[extra]   = new Vector4(d.x, d.y, d.z, 1f);
                s_extraColor[extra] = new Vector4(c.x * c.w, c.y * c.w, c.z * c.w, 1f);  // rgb × 세기
                extra++;
            }
            for (int i = extra; i < CharacterExtraLightMax; i++)   // 미사용 슬롯 0(스테일 방지)
            {
                s_extraDir[i]   = Vector4.zero;
                s_extraColor[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(IdExtraDir, s_extraDir);
            Shader.SetGlobalVectorArray(IdExtraColor, s_extraColor);
            Shader.SetGlobalFloat(IdExtraCount, extra);
        }

        // 결정적 정렬: priority 내림차순 → name 오름차순 → instanceID 오름차순.
        private static int CompareLights(CharacterToonLight a, CharacterToonLight b)
        {
            int c = b.Priority.CompareTo(a.Priority);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.name, b.name);
            if (c != 0) return c;
            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        }

        private static void ClearGlobals()
        {
            Shader.SetGlobalVector(IdLightDir, new Vector4(0f, 1f, 0f, 0f));  // w=0 → 셰이더가 메인 라이트로 폴백
            Shader.SetGlobalVector(IdLightColor, Vector4.zero);              // a=0 → 메인 라이트 색 사용
            Shader.SetGlobalFloat(IdExtraCount, 0f);                         // 추가 캐릭터 라이트 없음
        }
    }
}
