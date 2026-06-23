using UnityEditor;
using UnityEngine;

namespace CharacterToon.Editor
{
    /// <summary>
    /// Minimal Face SDF validation editor tool. Allows real-time adjustment of light azimuth,
    /// face orientation vectors, and visual pass/fail assessment in the Scene view.
    /// Expected pass criterion: rotating the light left->right must move the shaded boundary
    /// smoothly with no left/right flip popping. Guides decision #5 (SDF black/white, UV symmetry, RdotL flip).
    /// </summary>
    public class FaceSdfValidatorWindow : EditorWindow
    {
        private float _lightAzimuth = 0f;
        private Vector3 _faceForward = Vector3.forward;
        private Vector3 _faceRight = Vector3.right;

        private static readonly int CharacterLightDirWSId = Shader.PropertyToID("_CharacterLightDirWS");
        private static readonly int FaceForwardWSId = Shader.PropertyToID("_FaceForwardWS");
        private static readonly int FaceRightWSId = Shader.PropertyToID("_FaceRightWS");

        [MenuItem("Window/CharacterToon/Face SDF Validator")]
        public static void ShowWindow()
        {
            GetWindow<FaceSdfValidatorWindow>("Face SDF Validator");
        }

        // 창이 열릴 때 즉시 모든 글로벌을 적용한다. 얼굴 벡터가 (0,0,0)으로 남아
        // 셰이더에서 NaN/무반응이 되는 것을 막는다("Apply Face Vectors" 미클릭 방어).
        private void OnEnable()
        {
            ApplyFaceVectors();
            ApplyLightDirection();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Face SDF Validation Tool", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Expected pass criterion: rotating the light left->right must move the shaded boundary smoothly.\n" +
                "If the lit side is mirrored, decision #5 (flip direction / UV symmetry) needs inverting.\n" +
                "주의: 검증 중 CharacterToonLight/CharacterToonFace 컴포넌트는 비활성화. 머티리얼 Part=Face + FaceSDF 텍스처 필요.",
                MessageType.Info
            );

            if (GUILayout.Button("Apply All Now (라이트+얼굴벡터 강제 재적용)"))
            {
                ApplyFaceVectors();
                ApplyLightDirection();
            }

            EditorGUILayout.Space();

            // Light Azimuth slider
            EditorGUILayout.LabelField("Light Control", EditorStyles.boldLabel);
            float newAzimuth = EditorGUILayout.Slider("Light Azimuth (°)", _lightAzimuth, -180f, 180f);
            if (!Mathf.Approximately(newAzimuth, _lightAzimuth))
            {
                _lightAzimuth = newAzimuth;
                ApplyLightDirection();
            }

            EditorGUILayout.Space();

            // Preset buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Front (0°)"))
            {
                _lightAzimuth = 0f;
                ApplyLightDirection();
            }
            if (GUILayout.Button("Left (-90°)"))
            {
                _lightAzimuth = -90f;
                ApplyLightDirection();
            }
            if (GUILayout.Button("Right (90°)"))
            {
                _lightAzimuth = 90f;
                ApplyLightDirection();
            }
            if (GUILayout.Button("Back (180°)"))
            {
                _lightAzimuth = 180f;
                ApplyLightDirection();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // Face orientation vectors
            EditorGUILayout.LabelField("Face Orientation", EditorStyles.boldLabel);
            _faceForward = EditorGUILayout.Vector3Field("Face Forward", _faceForward);
            _faceRight = EditorGUILayout.Vector3Field("Face Right", _faceRight);

            if (GUILayout.Button("Apply Face Vectors"))
            {
                ApplyFaceVectors();
            }

            if (GUILayout.Button("Reset to Default"))
            {
                _faceForward = Vector3.forward;
                _faceRight = Vector3.right;
                ApplyFaceVectors();
            }

            // 현재 셰이더에 바인딩된 글로벌 값 표시.
            // 이 값이 슬라이더대로 바뀌는데 화면이 안 변하면 -> 툴은 정상, 원인은 머티리얼(Part=Face/SDF)/뷰.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Currently Bound Globals", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("_CharacterLightDirWS", Shader.GetGlobalVector(CharacterLightDirWSId).ToString("F2"));
            EditorGUILayout.LabelField("_FaceForwardWS", Shader.GetGlobalVector(FaceForwardWSId).ToString("F2"));
            EditorGUILayout.LabelField("_FaceRightWS", Shader.GetGlobalVector(FaceRightWSId).ToString("F2"));
            Repaint();
        }

        private void ApplyLightDirection()
        {
            // Rotate light around Y axis: elevation ~20°, azimuth from slider
            Vector3 direction = Quaternion.Euler(20f, _lightAzimuth, 0f) * Vector3.forward;
            // Consistent with CharacterToonLight convention: direction is surface -> light
            Shader.SetGlobalVector(CharacterLightDirWSId, new Vector4(direction.x, direction.y, direction.z, 1f));
            SceneView.RepaintAll();
        }

        private void ApplyFaceVectors()
        {
            Shader.SetGlobalVector(FaceForwardWSId, new Vector4(_faceForward.x, _faceForward.y, _faceForward.z, 1f));
            Shader.SetGlobalVector(FaceRightWSId, new Vector4(_faceRight.x, _faceRight.y, _faceRight.z, 1f));
            SceneView.RepaintAll();
        }
    }
}
