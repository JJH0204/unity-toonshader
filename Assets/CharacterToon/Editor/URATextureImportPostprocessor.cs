// using UnityEditor;
// using UnityEngine;
//
// namespace CharacterToon.Editor
// {
//     /// <summary>
//     /// AssetPostprocessor for enforcing correct texture import settings on URA character sample textures.
//     ///
//     /// 스코프 가드:
//     ///  - OnPreprocessTexture() 는 모든 텍스처 임포트 시 호출되지만,
//     ///    "Assets/Sample/Texture/URA/" 경로 내 텍스처만 처리한다.
//     ///    다른 샘플이나 프로젝트 자산에는 영향을 주지 않는다.
//     ///
//     /// sRGB vs Linear 정책:
//     ///  - ALBEDO (컬러): sRGB=true (인지적 색상 공간에서 임포트)
//     ///  - MASK/ILM (데이터): sRGB=false (선형 데이터로, 레이어 팩킹/쉐이딩 수학 보존)
//     ///  - SDF (거리장): sRGB=false (선형 데이터로, 임계값 정확도 확보)
//     ///
//     /// 현재 PSD→FBX 파이프라인이 마스크/SDF 맵을 wrongly sRGB=1로 임포트하여
//     /// 셰이딩과 후처리 ILM 팩커가 손상되므로, 이 postprocessor 는 재임포트 시점에 수정한다.
//     /// </summary>
//     public class URATextureImportPostprocessor : AssetPostprocessor
//     {
//         private void OnPreprocessTexture()
//         {
//             // 정규화: Windows 백슬래시를 포워드슬래시로 통일 (AssetDatabase 표준)
//             string normalizedPath = assetPath.Replace("\\", "/").ToLowerInvariant();
//             
//             // 스코프 가드: URA 샘플 텍스처만 처리
//             if (!normalizedPath.Contains("assets/sample/texture/ura/"))
//             {
//                 return;
//             }
//
//             // 파일명 추출
//             string fileName = System.IO.Path.GetFileNameWithoutExtension(normalizedPath).ToLowerInvariant();
//
//             // 분류: 파일명과 경로로부터 텍스처 타입 판단
//             bool isSDF = normalizedPath.Contains("/sdf/") || fileName.Contains("sdf");
//             bool isMask = normalizedPath.Contains("/mask/") || 
//                           fileName.Contains("matcap") || 
//                           fileName.Contains("emissive") || 
//                           fileName.Contains("_mask");
//
//             TextureImporter importer = (TextureImporter)assetImporter;
//
//             // 우선순위: SDF -> Mask -> Albedo
//             if (isSDF)
//             {
//                 ApplySDFSettings(importer);
//             }
//             else if (isMask)
//             {
//                 ApplyMaskSettings(importer);
//             }
//             else
//             {
//                 ApplyAlbedoSettings(importer);
//             }
//         }
//
//         private void ApplyAlbedoSettings(TextureImporter importer)
//         {
//             importer.textureType = TextureImporterType.Default;
//             importer.sRGBTexture = true;  // 컬러 공간 (인지적 색상)
//             importer.mipmapEnabled = true;
//         }
//
//         private void ApplyMaskSettings(TextureImporter importer)
//         {
//             importer.textureType = TextureImporterType.Default;
//             importer.sRGBTexture = false;  // 선형 데이터 (ILM 팩킹 보존)
//             importer.alphaIsTransparency = false;
//             importer.mipmapEnabled = true;
//             importer.textureCompression = TextureImporterCompression.Compressed;
//         }
//
//         private void ApplySDFSettings(TextureImporter importer)
//         {
//             importer.textureType = TextureImporterType.Default;
//             importer.sRGBTexture = false;  // 선형 데이터 (거리장 임계값 정확도)
//             importer.alphaIsTransparency = false;
//             importer.mipmapEnabled = true;
//             importer.filterMode = FilterMode.Bilinear;   // 텍셀 사이 매끈 보간(Point면 확대 시 계단)
//             importer.wrapMode = TextureWrapMode.Clamp;  // flip-sampling 안전성
//             // 무압축 필수: SDF는 그래디언트를 '샤프하게 임계화'하므로 BC7(HQ)도 4x4 블록 양자화가
//             //   픽셀/블록 노이즈로 드러난다(가까이서 지저분). 셰이더 AA로는 못 지우는 데이터 노이즈 →
//             //   무압축(R8/RGBA32)으로 그래디언트 원본을 보존해야 경계가 깔끔해진다.
//             importer.textureCompression = TextureImporterCompression.Uncompressed;
//         }
//
//         /// <summary>
//         /// 메뉴 명령: 이미 임포트된 URA 텍스처 자산에 정책을 재적용한다.
//         /// Window > CharacterToon > Reimport URA Textures 에서 실행 가능.
//         /// </summary>
//         [MenuItem("Window/CharacterToon/Reimport URA Textures")]
//         public static void ReimportURATextures()
//         {
//             string uraFolder = "Assets/Sample/Texture/URA";
//
//             // 폴더 존재 확인
//             if (!AssetDatabase.IsValidFolder(uraFolder))
//             {
//                 EditorUtility.DisplayDialog(
//                     "URA Texture Reimport",
//                     $"폴더를 찾을 수 없습니다: {uraFolder}",
//                     "OK"
//                 );
//                 return;
//             }
//
//             // 폴더 내 모든 Texture2D 자산 찾기
//             string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { uraFolder });
//
//             if (guids.Length == 0)
//             {
//                 EditorUtility.DisplayDialog(
//                     "URA Texture Reimport",
//                     $"{uraFolder} 내에서 텍스처를 찾을 수 없습니다.",
//                     "OK"
//                 );
//                 return;
//             }
//
//             // 배치 편집 시작
//             AssetDatabase.StartAssetEditing();
//
//             try
//             {
//                 foreach (string guid in guids)
//                 {
//                     string path = AssetDatabase.GUIDToAssetPath(guid);
//                     AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
//                 }
//             }
//             finally
//             {
//                 AssetDatabase.StopAssetEditing();
//             }
//
//             // 데이터베이스 새로고침
//             AssetDatabase.Refresh();
//
//             // 완료 메시지
//             EditorUtility.DisplayDialog(
//                 "URA Texture Reimport",
//                 $"완료: {guids.Length}개 텍스처가 정책에 맞게 재임포트되었습니다.",
//                 "OK"
//             );
//
//             Debug.Log($"[URATextureImportPostprocessor] {guids.Length}개 URA 텍스처 재임포트 완료.");
//         }
//     }
// }
