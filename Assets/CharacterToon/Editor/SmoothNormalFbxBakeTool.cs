using UnityEditor;
using UnityEngine;

namespace CharacterToon.Editor
{
    /// <summary>
    /// FBX(모델)를 등록하고 Weld Tolerance를 직접 설정해 외곽선 스무스 노멀을 베이크하는 단일 툴.
    ///
    /// 동작:
    ///  - Bake → 모델 임포터 userData 에 허용치를 등록하고 재임포트.
    ///    재임포트 중 SmoothNormalModelPostprocessor 가 '등록된 허용치'로 메시 TANGENT에 자동 베이크.
    ///  - 등록된 FBX는 폴더 스코프(Assets/Sample/FBX/) 밖이어도 이후 임포트마다 같은 허용치로 자동 적용.
    ///  - 결과는 FBX 임포트 메시에 직접 반영 → 별도 _SmoothOutline 에셋/재할당 불필요.
    /// </summary>
    public class SmoothNormalFbxBakeTool : EditorWindow
    {
        private GameObject _model;
        private float _weldTolerance = SmoothNormalUtil.DefaultWeldTolerance;

        [MenuItem("Window/CharacterToon/Smooth Normal FBX Bake (single)")]
        public static void ShowWindow()
        {
            GetWindow<SmoothNormalFbxBakeTool>("Smooth Normal FBX Bake");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Smooth Normal — FBX 단일 베이크", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "FBX(모델)를 등록하고 Weld Tolerance를 정해 Bake 하세요.\n" +
                "허용치는 모델 임포터에 저장되어 이후 재임포트에도 같은 값으로 자동 적용됩니다.\n" +
                "결과는 FBX 메시 TANGENT.xyz 에 직접 반영(별도 에셋/재할당 불필요).",
                MessageType.Info);
            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
            _model = EditorGUILayout.ObjectField("FBX (Model)", _model, typeof(GameObject), false) as GameObject;
            if (EditorGUI.EndChangeCheck())
                LoadExistingTolerance();

            ModelImporter mi = GetImporter();

            using (new EditorGUI.DisabledScope(mi == null))
            {
                _weldTolerance = EditorGUILayout.FloatField(
                    new GUIContent("Weld Tolerance",
                        "같은 위치로 간주해 노멀을 합칠 거리 허용치(월드 유닛). 외곽선이 끊기면 ↑(0.0005~0.001), 떨어진 정점까지 뭉치면 ↓."),
                    _weldTolerance);
                if (_weldTolerance < 1e-6f) _weldTolerance = 1e-6f;

                EditorGUILayout.Space();
                if (GUILayout.Button("Bake (등록 + 재임포트)", GUILayout.Height(36)))
                    Bake();
                if (GUILayout.Button("Clear Registration (등록 해제)"))
                    ClearRegistration();
            }

            if (_model != null && mi == null)
                EditorGUILayout.HelpBox("선택한 오브젝트가 모델(FBX) 자산이 아닙니다.", MessageType.Warning);
            else if (mi != null && SmoothNormalModelPostprocessor.TryGetRegisteredTolerance(mi.userData, out float reg))
                EditorGUILayout.HelpBox($"등록됨: Weld Tolerance = {reg}", MessageType.None);
        }

        private ModelImporter GetImporter()
        {
            if (_model == null) return null;
            string path = AssetDatabase.GetAssetPath(_model);
            if (string.IsNullOrEmpty(path)) return null;
            return AssetImporter.GetAtPath(path) as ModelImporter;
        }

        private void LoadExistingTolerance()
        {
            ModelImporter mi = GetImporter();
            if (mi != null && SmoothNormalModelPostprocessor.TryGetRegisteredTolerance(mi.userData, out float t))
                _weldTolerance = t;
        }

        private void Bake()
        {
            ModelImporter mi = GetImporter();
            if (mi == null)
            {
                EditorUtility.DisplayDialog("Error", "모델(FBX) 자산을 먼저 등록하세요.", "OK");
                return;
            }
            SmoothNormalModelPostprocessor.SetRegisteredTolerance(mi, _weldTolerance);
            mi.SaveAndReimport();   // → OnPostprocessModel 이 등록 허용치로 자동 베이크
            EditorUtility.DisplayDialog("Smooth Normal FBX Bake",
                $"'{System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(_model))}' 베이크 완료 (tol={_weldTolerance}).\n" +
                "이 허용치가 등록되어 이후 재임포트에도 자동 적용됩니다.", "OK");
        }

        private void ClearRegistration()
        {
            ModelImporter mi = GetImporter();
            if (mi == null) return;
            SmoothNormalModelPostprocessor.ClearRegisteredTolerance(mi);
            mi.SaveAndReimport();
            EditorUtility.DisplayDialog("Smooth Normal FBX Bake",
                "등록을 해제했습니다. (폴더 스코프 내 FBX라면 기본 허용치로 계속 자동 베이크됩니다.)", "OK");
        }
    }
}
