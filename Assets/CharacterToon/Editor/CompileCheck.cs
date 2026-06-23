// TEMP batch-mode compile/validation harness. Delete after testing.
// Invoked headless via: Unity.exe -batchmode -quit -executeMethod CharacterToon.Editor.CompileCheck.Run
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace CharacterToon.Editor
{
    public static class CompileCheck
    {
        // 열린 에디터에서 SceneToon(배경) + 공유 라이브러리 + CharacterToon 셰이더 오류를 Console 로 검증.
        //   배치 컴파일이 에디터 락과 충돌할 때 사용. 결과는 Console + compilecheck_result.txt.
        [MenuItem("Tools/Toon/Validate Shaders (Scene + Character)")]
        public static void ValidateMenu()
        {
            RunInternal(new[] { "Assets/SceneToon", "Assets/ToonShared", "Assets/CharacterToon" }, exitOnDone: false);
        }

        public static void Run()
        {
            RunInternal(new[] { "Assets/CharacterToon" }, exitOnDone: true);
        }

        // 헤드리스 배치: SceneToon(배경) + 공유 라이브러리 + CharacterToon 전부 검증 후 종료.
        public static void RunAll()
        {
            RunInternal(new[] { "Assets/SceneToon", "Assets/ToonShared", "Assets/CharacterToon" }, exitOnDone: true);
        }

        private static void RunInternal(string[] roots, bool exitOnDone)
        {
            var sb = new StringBuilder();
            int errorCount = 0;

            // 1) Shader compile messages (variants force-compiled per material below)
            string[] shaderGuids = AssetDatabase.FindAssets("t:Shader", roots);
            foreach (var g in shaderGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) continue;

                bool hasErr = ShaderUtil.ShaderHasError(shader);
                var msgs = ShaderUtil.GetShaderMessages(shader);
                sb.AppendLine($"[SHADER] {path}  hasError={hasErr}  messages={msgs.Length}");
                foreach (var m in msgs)
                {
                    string tag = m.severity == ShaderCompilerMessageSeverity.Error ? "ERROR" : "warn";
                    if (m.severity == ShaderCompilerMessageSeverity.Error) errorCount++;
                    sb.AppendLine($"   [{tag}] {m.message} | {m.messageDetails} (platform={m.platform})");
                }
            }

            // 2) Material validation — confirm each sample mat keeps the CharacterToon shader (not fallback)
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Sample/Material" });
            foreach (var g in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == null) continue;
                string keywords = string.Join(",", mat.shaderKeywords);
                sb.AppendLine($"[MAT] {Path.GetFileName(path)}  shader='{mat.shader.name}'  keywords=[{keywords}]");
            }

            sb.Insert(0, $"=== Toon CompileCheck === roots=[{string.Join(",", roots)}] errors={errorCount}\n");
            string outPath = Path.Combine(Directory.GetCurrentDirectory(), "compilecheck_result.txt");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log(sb.ToString());

            if (exitOnDone)
                EditorApplication.Exit(errorCount > 0 ? 1 : 0);
        }
    }
}
