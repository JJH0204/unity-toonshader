using System.Collections.Generic;
using UnityEngine;

namespace CharacterToon.Editor
{
    /// <summary>
    /// 외곽선용 스무스 노멀 계산 공용 유틸.
    /// 수동 베이커(SmoothNormalBaker)와 FBX 임포트 자동 베이크(SmoothNormalModelPostprocessor)가
    /// '동일 알고리즘'을 공유하도록 한 곳에 모은다.
    ///
    /// 알고리즘(외곽선이 끊김 없이 이어지도록):
    ///  (1) 위치 허용치 용접: 정점을 정확 일치가 아니라 weldTolerance 격자로 양자화해 묶는다
    ///      → 시접/하드엣지/부동소수 미세차로 갈렸던 정점이 합쳐져 노멀이 연속이 된다.
    ///  (2) 각도가중 면노멀 누적: 코너 각도로 가중한 면노멀을 모은다(면 크기·정점 분할 수 무관)
    ///      → 코너에서도 두께가 균일한 매끈한 외곽선.
    ///
    /// 저장: 결정 #3 — 스무스 노멀은 mesh TANGENT.xyz(w=1)에 보관(스키닝에서 살아남음).
    /// 포워드 패스 노멀맵은 픽셀 미분 코탄젠트 프레임을 쓰므로 tangent를 점유해도 충돌 없음.
    /// </summary>
    public static class SmoothNormalUtil
    {
        public const float DefaultWeldTolerance = 0.0001f;

        public static Vector3[] ComputeSmoothNormals(Mesh source, float weldTolerance = DefaultWeldTolerance)
        {
            Vector3[] verts = source.vertices;
            Vector3[] srcNormals = source.normals;
            int vc = verts.Length;
            var smoothed = new Vector3[vc];
            if (vc == 0) return smoothed;

            float invTol = 1f / Mathf.Max(weldTolerance, 1e-6f);
            System.Func<Vector3, Vector3Int> keyOf = p => new Vector3Int(
                Mathf.RoundToInt(p.x * invTol),
                Mathf.RoundToInt(p.y * invTol),
                Mathf.RoundToInt(p.z * invTol));

            var accum = new Dictionary<Vector3Int, Vector3>();
            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] tris = source.GetTriangles(s);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                    Vector3 p0 = verts[i0], p1 = verts[i1], p2 = verts[i2];
                    Vector3 faceN = Vector3.Cross(p1 - p0, p2 - p0);
                    if (faceN.sqrMagnitude < 1e-20f) continue;   // 퇴화 삼각형 스킵
                    faceN.Normalize();
                    float a0 = Vector3.Angle(p1 - p0, p2 - p0);   // 코너 각도 가중
                    float a1 = Vector3.Angle(p2 - p1, p0 - p1);
                    float a2 = Vector3.Angle(p0 - p2, p1 - p2);
                    AccumAdd(accum, keyOf(p0), faceN * a0);
                    AccumAdd(accum, keyOf(p1), faceN * a1);
                    AccumAdd(accum, keyOf(p2), faceN * a2);
                }
            }

            bool hasSrc = srcNormals != null && srcNormals.Length == vc;
            for (int i = 0; i < vc; i++)
            {
                if (accum.TryGetValue(keyOf(verts[i]), out var n) && n.sqrMagnitude > 1e-20f)
                    smoothed[i] = n.normalized;
                else
                    smoothed[i] = hasSrc ? srcNormals[i] : Vector3.up;   // 폴백
            }
            return smoothed;
        }

        /// <summary>스무스 노멀을 메시 TANGENT.xyz(w=1)에 in-place 기록.</summary>
        public static void BakeIntoTangents(Mesh mesh, float weldTolerance = DefaultWeldTolerance)
        {
            if (mesh == null || mesh.vertexCount == 0) return;
            Vector3[] n = ComputeSmoothNormals(mesh, weldTolerance);
            var tangents = new Vector4[n.Length];
            for (int i = 0; i < n.Length; i++)
                tangents[i] = new Vector4(n[i].x, n[i].y, n[i].z, 1f);
            mesh.tangents = tangents;
        }

        // 누적 헬퍼: 양자화된 위치 키에 가중 노멀을 더한다(C# Dictionary는 += 가 안 됨).
        private static void AccumAdd(Dictionary<Vector3Int, Vector3> d, Vector3Int k, Vector3 v)
        {
            if (d.TryGetValue(k, out var cur)) d[k] = cur + v;
            else d[k] = v;
        }
    }
}
