using UnityEngine;

namespace CharacterToon
{
    /// <summary>
    /// L1(갭): 퍼-캐릭터 툰 라이트. 방향(키 라이트 또는 이 Transform) + 선택적 색/세기 오버라이드를
    /// 글로벌 셰이더 프로퍼티로 주입한다(SRP Batcher 호환). [[CharacterToonManager]]에 등록되어
    /// 최고 우선순위 1개가 전역 리그로 반영된다(단일-활성 모델).
    ///
    /// 주입은 매니저가 수행한다: 에디트/플레이에서는 LateUpdate, 플레이/빌드 렌더 루프에서는
    /// CharacterToonRendererFeature(AddRenderPasses)가 PushActive를 호출 — 카메라별 타이밍 정확성 확보.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class CharacterToonLight : MonoBehaviour
    {
        [Tooltip("키 라이트. 비우면 아래 옵션에 따라 동작.")]
        [SerializeField] private Light _keyLight;

        [Tooltip("키 라이트가 없을 때 이 GameObject의 forward를 라이트 방향으로 사용. 끄면(기본) 키 라이트 없을 시 메인 라이트로 폴백(기존 동작 보존).")]
        [SerializeField] private bool _useTransformAsDirection = false;

        [Tooltip("여러 캐릭터 라이트 중 전역 반영될 우선순위(클수록 우선).")]
        [SerializeField] private int _priority = 0;

        [Tooltip("켜면 아래 색/세기로 캐릭터 라이트 색을 오버라이드. 끄면 키 라이트(있으면) 색, 없으면 메인 라이트 색.")]
        [SerializeField] private bool _overrideColor = false;
        [SerializeField] private Color _color = Color.white;
        [SerializeField, Min(0f)] private float _intensity = 1f;

        public int Priority => _priority;

        private void OnEnable()  { CharacterToonManager.Register(this); }
        private void OnDisable() { CharacterToonManager.Unregister(this); }
        private void LateUpdate(){ CharacterToonManager.PushActive(); }

        /// <summary>
        /// 이 라이트의 리그(방향/색)를 산출해 반환. 매니저가 정렬 후 1순위는 전역, 2순위 이하는 배열에 담는다.
        ///   dir   : xyz=방향(정규화), w=1=방향 유효 / w=0=방향 없음
        ///   color : rgb=색(linear), a=세기 (a&lt;=0 이면 색 소스 없음 → 1순위면 메인 라이트 색 폴백)
        ///   return: 명시 방향(키라이트/Transform)이 있으면 true. (2순위 가산광은 false면 스킵)
        /// </summary>
        public bool GetRig(out Vector4 dir, out Vector4 color)
        {
            bool hasDir;
            Vector3 d;
            if (_keyLight != null)             { d = -_keyLight.transform.forward; hasDir = true; }
            else if (_useTransformAsDirection) { d = transform.forward;            hasDir = true; }
            else                               { d = Vector3.up;                   hasDir = false; }

            if (hasDir)
            {
                d = d.sqrMagnitude > 1e-8f ? d.normalized : Vector3.up;
                dir = new Vector4(d.x, d.y, d.z, 1f);
            }
            else
            {
                dir = new Vector4(0f, 1f, 0f, 0f);   // w=0 → (1순위) 셰이더 메인 라이트 방향 폴백
            }

            if (_overrideColor)
            {
                Color c = _color.linear;
                color = new Vector4(c.r, c.g, c.b, Mathf.Max(_intensity, 0f));
            }
            else if (_keyLight != null)
            {
                Color c = _keyLight.color.linear;
                color = new Vector4(c.r, c.g, c.b, _keyLight.intensity);
            }
            else
            {
                color = Vector4.zero;   // a=0 → (1순위) 셰이더 메인 라이트 색 폴백
            }
            return hasDir;
        }
    }
}
