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

        /// <summary>매니저가 호출 — 이 라이트의 리그를 전달된 글로벌 프로퍼티 ID로 push.</summary>
        public void ApplyGlobals(int idLightDir, int idLightColor)
        {
            // 방향 소스 결정. 키 라이트도 없고 Transform 방향도 안 쓰면 기존 동작(메인 라이트 폴백) 보존.
            Vector3 dir;
            if (_keyLight != null)            dir = -_keyLight.transform.forward;
            else if (_useTransformAsDirection) dir = transform.forward;
            else
            {
                Shader.SetGlobalVector(idLightDir, new Vector4(0f, 1f, 0f, 0f));  // w=0 → 메인 라이트 폴백
                Shader.SetGlobalVector(idLightColor, Vector4.zero);
                return;
            }

            dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : Vector3.up;
            Shader.SetGlobalVector(idLightDir, new Vector4(dir.x, dir.y, dir.z, 1f));

            if (_overrideColor)
            {
                Color c = _color.linear;
                Shader.SetGlobalVector(idLightColor, new Vector4(c.r, c.g, c.b, Mathf.Max(_intensity, 0f)));
            }
            else if (_keyLight != null)
            {
                Color c = _keyLight.color.linear;
                Shader.SetGlobalVector(idLightColor, new Vector4(c.r, c.g, c.b, _keyLight.intensity));
            }
            else
            {
                Shader.SetGlobalVector(idLightColor, Vector4.zero); // a=0 → 셰이더가 메인 라이트 색 사용
            }
        }
    }
}
