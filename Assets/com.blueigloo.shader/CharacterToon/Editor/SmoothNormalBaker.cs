using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace CharacterToon.Editor
{
    /// <summary>
    /// Smooth Outline Normal Baker (M3, T3-3).
    /// 
    /// Bakes smoothed vertex normals into a mesh's TANGENT channel without modifying the source FBX.
    /// Welds vertices by a position TOLERANCE (not exact float equality) and accumulates ANGLE-WEIGHTED
    /// face normals, so seams/hard-edges/float-jitter no longer split the outline → outline connects
    /// smoothly. Smoothed normals survive skinning on SkinnedMeshRenderer.
    /// 
    /// Workflow:
    /// 1. Select a GameObject with MeshFilter/SkinnedMeshRenderer, or select a Mesh directly.
    /// 2. Window → CharacterToon → Smooth Normal Baker, or context menu "Bake Smooth Normals".
    /// 3. Inspect the baked mesh preview; assign it to your renderer.
    /// 
    /// Decision #3 FIXED: Outline smooth normals live in mesh TANGENT.xyz (tangent.w=1).
    /// Rationale: Unity skins TANGENT per-vertex, so smoothed normals survive SkinnedMeshRenderer deformation.
    /// </summary>
    public class SmoothNormalBaker : EditorWindow
    {
        private Mesh _sourceMesh;
        private Vector2 _scrollPos;
        private float _weldTolerance = 0.0001f;   // 같은 위치로 간주할 거리 허용치(월드 유닛)

        [MenuItem("Window/CharacterToon/Smooth Normal Baker")]
        public static void ShowWindow()
        {
            GetWindow<SmoothNormalBaker>("Smooth Normal Baker");
        }

        [MenuItem("Assets/CharacterToon/Bake Smooth Normals", priority = 20)]
        public static void BakeSmoothNormalsContext()
        {
            var obj = Selection.activeObject;
            if (obj is Mesh mesh)
            {
                BakeSmoothNormalsForMesh(mesh);
            }
            else if (obj is GameObject go)
            {
                var meshFilter = go.GetComponent<MeshFilter>();
                var skinnedMeshRenderer = go.GetComponent<SkinnedMeshRenderer>();
                Mesh source = meshFilter != null ? meshFilter.sharedMesh : (skinnedMeshRenderer != null ? skinnedMeshRenderer.sharedMesh : null);
                if (source != null)
                    BakeSmoothNormalsForMesh(source);
            }
        }

        private void OnGUI()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            
            GUILayout.Label("Smooth Normal Baker", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "권장: FBX 임포트 시 자동 베이크가 동작합니다 (SmoothNormalModelPostprocessor).\n" +
                "  → Assets/Sample/FBX/ 하위 모델은 임포트만 해도 TANGENT에 스무스 노멀이 자동 기록됩니다.\n" +
                "  → 일괄 적용: Window > CharacterToon > Reimport Models (Auto Smooth Normals).\n\n" +
                "이 창은 임의의 단일 Mesh를 수동으로 굽고 별도 _SmoothOutline 에셋으로 저장할 때만 사용하세요.\n" +
                "Decision #3: 스무스 노멀은 tangent.xyz 에 저장(스키닝에서 살아남음).",
                MessageType.Info);

            EditorGUILayout.Space();

            _sourceMesh = EditorGUILayout.ObjectField("Source Mesh", _sourceMesh, typeof(Mesh), false) as Mesh;

            _weldTolerance = EditorGUILayout.FloatField(
                new GUIContent("Weld Tolerance",
                    "같은 위치로 간주해 노멀을 합칠 거리 허용치(월드 유닛). 너무 작으면 시접/부동소수 미세차에서 노멀이 안 합쳐져 외곽선이 끊기고, 너무 크면 떨어진 정점까지 뭉쳐 뭉개진다. 보통 0.0001~0.001."),
                _weldTolerance);
            if (_weldTolerance < 1e-6f) _weldTolerance = 1e-6f;

            EditorGUILayout.Space();

            if (GUILayout.Button("Bake Smooth Normals", GUILayout.Height(40)))
            {
                if (_sourceMesh != null)
                    BakeSmoothNormalsForMesh(_sourceMesh, _weldTolerance);
                else
                    EditorUtility.DisplayDialog("Error", "No mesh selected.", "OK");
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "After baking:\n" +
                "1. Assign the baked mesh to your MeshFilter or SkinnedMeshRenderer.\n" +
                "2. The outline shader will read tangent.xyz as the smooth normal.",
                MessageType.Info);

            GUILayout.EndScrollView();
        }

        private static void BakeSmoothNormalsForMesh(Mesh source, float weldTolerance = 0.0001f)
        {
            if (source == null)
            {
                EditorUtility.DisplayDialog("Error", "Source mesh is null.", "OK");
                return;
            }

            // Read source data
            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector4[] sourceTangents = source.tangents;
            Vector2[] sourceUv = source.uv;
            Vector2[] sourceUv2 = source.uv2;
            Vector2[] sourceUv3 = source.uv3;
            Vector2[] sourceUv4 = source.uv4;
            Color[] sourceColors = source.colors;
            BoneWeight[] sourceBoneWeights = source.boneWeights;
            Matrix4x4[] sourceBindPoses = source.bindposes;

            // 스무스 노멀 계산 — 공용 유틸 사용(자동 임포트 베이크와 동일 알고리즘).
            //  허용치 용접 + 각도가중 면노멀 → 시접에서도 끊김 없이 이어지는 외곽선.
            Vector3[] smoothedNormals = SmoothNormalUtil.ComputeSmoothNormals(source, weldTolerance);

            // Create baked mesh
            Mesh bakedMesh = new Mesh();
            bakedMesh.name = source.name + "_SmoothOutline";
            // 65535 정점 초과 메시 지원 — 인덱스를 넣기 전에 포맷을 먼저 맞춘다 (Codex 지적).
            bakedMesh.indexFormat = source.indexFormat;

            bakedMesh.vertices = sourceVertices;
            bakedMesh.normals = sourceNormals;
            
            // Pack smoothed normal into tangent.xyz, preserve tangent.w=1
            Vector4[] bakedTangents = new Vector4[sourceVertices.Length];
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                bakedTangents[i] = new Vector4(
                    smoothedNormals[i].x,
                    smoothedNormals[i].y,
                    smoothedNormals[i].z,
                    1f);
            }
            bakedMesh.tangents = bakedTangents;

            // Copy all UV coordinates
            if (sourceUv != null && sourceUv.Length > 0)
                bakedMesh.uv = sourceUv;
            if (sourceUv2 != null && sourceUv2.Length > 0)
                bakedMesh.uv2 = sourceUv2;
            if (sourceUv3 != null && sourceUv3.Length > 0)
                bakedMesh.uv3 = sourceUv3;
            if (sourceUv4 != null && sourceUv4.Length > 0)
                bakedMesh.uv4 = sourceUv4;

            // Copy colors
            if (sourceColors != null && sourceColors.Length > 0)
                bakedMesh.colors = sourceColors;

            // Copy bone weights and bind poses (for SkinnedMeshRenderer compatibility)
            if (sourceBoneWeights != null && sourceBoneWeights.Length > 0)
                bakedMesh.boneWeights = sourceBoneWeights;
            if (sourceBindPoses != null && sourceBindPoses.Length > 0)
                bakedMesh.bindposes = sourceBindPoses;

            // 서브메시 보존 — Mesh.triangles 직접 대입은 subMeshCount를 1로 붕괴시킨다(멀티 머티리얼 캐릭터 깨짐).
            // subMeshCount 복사 후 서브메시별 SetTriangles로 토폴로지/머티리얼 구획 유지 (Codex 지적).
            bakedMesh.subMeshCount = source.subMeshCount;
            for (int s = 0; s < source.subMeshCount; s++)
                bakedMesh.SetTriangles(source.GetTriangles(s), s);
            
            // Blendshapes: straightforward copy if they exist
            if (source.blendShapeCount > 0)
            {
                for (int i = 0; i < source.blendShapeCount; i++)
                {
                    string shapeName = source.GetBlendShapeName(i);
                    int frameCount = source.GetBlendShapeFrameCount(i);
                    for (int f = 0; f < frameCount; f++)
                    {
                        Vector3[] deltaVertices = new Vector3[sourceVertices.Length];
                        Vector3[] deltaNormals = new Vector3[sourceVertices.Length];
                        Vector3[] deltaTangents = new Vector3[sourceVertices.Length];
                        source.GetBlendShapeFrameVertices(i, f, deltaVertices, deltaNormals, deltaTangents);
                        float weight = source.GetBlendShapeFrameWeight(i, f);
                        bakedMesh.AddBlendShapeFrame(shapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                    }
                }
            }

            // Save as asset
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string sourceDir = System.IO.Path.GetDirectoryName(sourcePath);
            string sourceFileName = System.IO.Path.GetFileNameWithoutExtension(sourcePath);
            string assetPath = System.IO.Path.Combine(sourceDir, sourceFileName + "_SmoothOutline.asset");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            AssetDatabase.CreateAsset(bakedMesh, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Select the baked mesh
            EditorGUIUtility.PingObject(bakedMesh);
            Selection.activeObject = bakedMesh;

            EditorUtility.DisplayDialog(
                "Success",
                $"Smooth normal baked to:\n{assetPath}\n\nAssign this mesh to your MeshFilter or SkinnedMeshRenderer.",
                "OK");
        }
    }
}
