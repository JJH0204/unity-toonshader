using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SceneToon.Editor
{
    /// <summary>
    /// B5: SceneToon/Scene 용 lilToon 식 ShaderGUI (결정 #14 UX 방향).
    ///   카테고리 폴드아웃 + 상단 원클릭 Surface 모드(Opaque/Cutout/Transparent → Blend/ZWrite/Queue/키워드 일괄).
    ///   프로퍼티 렌더는 드로어([Toggle]/[Enum]/[Normal])·툴팁을 그대로 살리도록 ShaderProperty 사용.
    /// </summary>
    public class SceneToonShaderGUI : ShaderGUI
    {
        private const string FoldoutPrefPrefix = "SceneToon.ShaderGUI.Foldout.";

        // 섹션 = (id, 라벨, 프로퍼티 이름 목록)
        private static readonly (string id, string label, string[] props)[] Sections =
        {
            ("Base",    "Base",            new[] { "_BaseColor", "_Cull" }),
            ("Normal",  "Normal Map",      new[] { "_UseNormalMap", "_BumpMap", "_BumpScale" }),
            ("Toon",    "Toon Shading",    new[] { "_UseILM", "_ILMMap", "_UseRamp", "_RampMap", "_RampRow", "_ShadowOffsetScale", "_ShadeFloor", "_AmbientStrength" }),
            ("Bands",   "Shadow Bands",    new[] { "_ShadowColor", "_ShadowBorder", "_ShadowBlur", "_Shadow2ndColor", "_Shadow2ndBorder", "_Shadow2ndBlur", "_ShadowStrength", "_ReceiveShadowStrength" }),
            ("GI",      "Baked GI Toonify",new[] { "_GIShadeColor", "_GIBandCount", "_GIBandSoftness" }),
            ("Rim",     "Rim Light",       new[] { "_UseRim", "_RimColor", "_RimThreshold", "_RimSoftness", "_RimIntensity" }),
            ("AddL",    "Additional Lights",new[] { "_UseAddLights", "_AdditionalLightStrength" }),
            ("Foliage", "Foliage",         new[] { "_UseWind", "_WindParams", "_WindStrength", "_WindSpeed", "_UseTranslucency", "_TranslucencyColor", "_TranslucencyStrength" }),
            ("Detail",  "Surface Detail",  new[] { "_UseTriplanar", "_TriplanarScale", "_TriplanarBlend", "_UseVertexBlend", "_LayerMap", "_LayerColor", "_LayerBlendChannel", "_UseOcclusion", "_OcclusionMap", "_OcclusionStrength" }),
            ("Outline", "Outline (suppress)", new[] { "_UseOutlineSuppress", "_OutlineSuppress" }),
        };

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            // ── 상단: Surface 모드 ──
            DrawSurfaceMode(materialEditor, properties);
            EditorGUILayout.Space();

            // Base Map(+타일링)은 별도로 위에 노출
            MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);
            if (baseMap != null)
                materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, FindProperty("_BaseColor", properties, false));

            foreach (var sec in Sections)
            {
                if (!Foldout(sec.id, sec.label)) continue;
                EditorGUI.indentLevel++;
                foreach (string name in sec.props)
                {
                    // Base 섹션의 _BaseColor 는 위에서 이미 그렸으니 생략
                    if (sec.id == "Base" && name == "_BaseColor") continue;
                    MaterialProperty p = FindProperty(name, properties, false);
                    if (p != null)
                        materialEditor.ShaderProperty(p, p.displayName);
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();
            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
            materialEditor.DoubleSidedGIField();
        }

        // ── Surface 모드 드롭다운 (Opaque/Cutout/Transparent) ──
        private static readonly string[] ModeNames = { "Opaque", "Cutout", "Transparent" };

        private void DrawSurfaceMode(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            MaterialProperty surface = FindProperty("_Surface", properties, false);
            if (surface == null) return;

            int current = GetCurrentMode(materialEditor.targets);
            EditorGUI.BeginChangeCheck();
            int newMode = EditorGUILayout.Popup(new GUIContent("Rendering Mode"), current, ModeNames);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object t in materialEditor.targets)
                {
                    if (t is Material m)
                    {
                        Undo.RecordObject(m, "Set Rendering Mode");
                        ApplySurfaceMode(m, newMode);
                        EditorUtility.SetDirty(m);
                    }
                }
            }

            // Cutout 일 때만 Cutoff 노출
            if (newMode == 1)
            {
                MaterialProperty cutoff = FindProperty("_Cutoff", properties, false);
                if (cutoff != null)
                    materialEditor.ShaderProperty(cutoff, cutoff.displayName);
            }
        }

        private static int GetCurrentMode(Object[] targets)
        {
            if (targets.Length == 0 || !(targets[0] is Material m)) return 0;
            if (m.GetFloat("_Surface") > 0.5f) return 2;                 // Transparent
            if (m.IsKeywordEnabled("_ALPHATEST_ON")) return 1;           // Cutout
            return 0;                                                    // Opaque
        }

        private static void ApplySurfaceMode(Material m, int mode)
        {
            switch (mode)
            {
                case 2: // Transparent
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    m.SetFloat("_ZWrite", 0f);
                    if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.SetOverrideTag("RenderType", "Transparent");
                    m.renderQueue = (int)RenderQueue.Transparent;        // 3000
                    break;
                case 1: // Cutout
                    m.SetFloat("_Surface", 0f);
                    m.SetFloat("_SrcBlend", (float)BlendMode.One);
                    m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    m.SetFloat("_ZWrite", 1f);
                    if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetOverrideTag("RenderType", "TransparentCutout");
                    m.renderQueue = (int)RenderQueue.AlphaTest;          // 2450
                    break;
                default: // Opaque
                    m.SetFloat("_Surface", 0f);
                    m.SetFloat("_SrcBlend", (float)BlendMode.One);
                    m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                    m.SetFloat("_ZWrite", 1f);
                    if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 0f);
                    m.DisableKeyword("_ALPHATEST_ON");
                    m.SetOverrideTag("RenderType", "Opaque");
                    m.renderQueue = (int)RenderQueue.Geometry;           // 2000
                    break;
            }
        }

        private static bool Foldout(string id, string label)
        {
            string key = FoldoutPrefPrefix + id;
            bool open = EditorPrefs.GetBool(key, true);
            bool now = EditorGUILayout.Foldout(open, label, true);
            if (now != open) EditorPrefs.SetBool(key, now);
            return now;
        }
    }
}
