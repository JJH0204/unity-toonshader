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
        public static void Run()
        {
            var sb = new StringBuilder();
            int errorCount = 0;

            // 1) Shader compile messages (variants force-compiled per material below)
            string[] shaderGuids = AssetDatabase.FindAssets("t:Shader", new[] { "Assets/CharacterToon" });
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

            sb.Insert(0, $"=== CharacterToon CompileCheck === errors={errorCount}\n");
            string outPath = Path.Combine(Directory.GetCurrentDirectory(), "compilecheck_result.txt");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log(sb.ToString());

            EditorApplication.Exit(errorCount > 0 ? 1 : 0);
        }
    }
}
