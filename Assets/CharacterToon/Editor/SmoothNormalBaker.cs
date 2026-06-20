using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace CharacterToon.Editor
{
    /// <summary>
    /// Smooth Outline Normal Baker (M3, T3-3).
    /// 
    /// Bakes smoothed vertex normals (averaged from welded positions) into a mesh's TANGENT channel
    /// without modifying the source FBX. Smoothed normals survive skinning on SkinnedMeshRenderer.
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
                "Bakes smoothed vertex normals (welded by position) into a new mesh's TANGENT channel.\n\n" +
                "Decision #3: Smooth outline normals stored in tangent.xyz so they survive SkinnedMeshRenderer skinning.\n\n" +
                "Output: saves a new mesh asset (_SmoothOutline) with identical vertices/triangles but tangent = smoothed normal.",
                MessageType.Info);

            EditorGUILayout.Space();

            _sourceMesh = EditorGUILayout.ObjectField("Source Mesh", _sourceMesh, typeof(Mesh), false) as Mesh;

            EditorGUILayout.Space();

            if (GUILayout.Button("Bake Smooth Normals", GUILayout.Height(40)))
            {
                if (_sourceMesh != null)
                    BakeSmoothNormalsForMesh(_sourceMesh);
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

        private static void BakeSmoothNormalsForMesh(Mesh source)
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
            int[] sourceTriangles = source.triangles;
            int[] sourceTriangleIndices32 = source.triangles;
            
            // Compute smoothed normals by welding vertices at same position
            Dictionary<Vector3, List<int>> positionToIndices = new Dictionary<Vector3, List<int>>();
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 pos = sourceVertices[i];
                if (!positionToIndices.ContainsKey(pos))
                    positionToIndices[pos] = new List<int>();
                positionToIndices[pos].Add(i);
            }

            Vector3[] smoothedNormals = new Vector3[sourceVertices.Length];
            foreach (var kvp in positionToIndices)
            {
                Vector3 summedNormal = Vector3.zero;
                foreach (int idx in kvp.Value)
                    summedNormal += sourceNormals[idx];
                Vector3 smoothedNormal = summedNormal.normalized;
                foreach (int idx in kvp.Value)
                    smoothedNormals[idx] = smoothedNormal;
            }

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
