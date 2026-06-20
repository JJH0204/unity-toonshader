using UnityEngine;
using UnityEditor;
using System.IO;

namespace CharacterToon.Editor
{
    /// <summary>
    /// ILM Packer (M0, Decision #11 hybrid).
    /// 
    /// Packs separate per-part grayscale masks into a single ILM RGBA texture.
    /// 
    /// ILM Channel Convention (LOCKED project-wide):
    ///   R = specular / MatCap strength     (source: MatCap mask, or hair angel-ring mask)
    ///   G = shadow-entry bias for Ramp U   (neutral default 0.5)
    ///   B = specular width / secondary ramp (neutral default 0.5)
    ///   A = inner-line / outline-suppression mask (neutral default 0)
    /// 
    /// Neutral fallback when a channel has no source: R=0, G=0.5, B=0.5, A=0.
    /// 
    /// Workflow:
    /// 1. Open Window → CharacterToon → ILM Packer.
    /// 2. For each output channel (R, G, B, A), optionally assign a source Texture2D and select a source channel.
    /// 3. Set Output Resolution (Auto, 256, 512, 1024, 2048).
    /// 4. Click "Pack ILM" to combine all channels into a single RGBA texture.
    /// 5. Optionally assign the result to a material's _ILMMap.
    /// 
    /// Robust packing logic:
    /// - Works regardless of source texture Read/Write settings (uses RenderTexture.Blit, not GetPixels on the source).
    /// - IMPORTANT: source masks must be imported LINEAR (sRGBTexture = false). ILM is data, not color.
    ///   If a source is imported as sRGB, Blit linearizes its RGB on sample (0.5 -> ~0.214), corrupting the packed value.
    ///   The packer detects sRGB sources and warns before packing (run the PSD import-settings pass first).
    /// - Uses RenderTexture.Blit to scale sources uniformly to output resolution.
    /// - Reads from Linear RenderTexture to ensure correct data representation.
    /// - Forces LINEAR import on the result (ILM is data, not color).
    /// - Cleans up temporary objects (Texture2D, RenderTexture) after packing.
    /// </summary>
    public class ILMPackerWindow : EditorWindow
    {
        private enum SourceChannel { R, G, B, A, Grayscale }
        private enum ResolutionOption { Auto, _256, _512, _1024, _2048 }

        private struct ChannelConfig
        {
            public Texture2D source;
            public SourceChannel sourceChannel;
            public bool invert;
            public float defaultValue;
        }

        private ChannelConfig _rChannel = new ChannelConfig { sourceChannel = SourceChannel.R, defaultValue = 0f };
        private ChannelConfig _gChannel = new ChannelConfig { sourceChannel = SourceChannel.G, defaultValue = 0.5f };
        private ChannelConfig _bChannel = new ChannelConfig { sourceChannel = SourceChannel.B, defaultValue = 0.5f };
        private ChannelConfig _aChannel = new ChannelConfig { sourceChannel = SourceChannel.A, defaultValue = 0f };

        private ResolutionOption _resolutionOption = ResolutionOption.Auto;
        private string _outputPath = "";
        private string _outputFileName = "";

        private Material _targetMaterial;
        private bool _assignToMaterial;

        private Vector2 _scrollPos;

        [MenuItem("Window/CharacterToon/ILM Packer")]
        public static void ShowWindow()
        {
            GetWindow<ILMPackerWindow>("ILM Packer");
        }

        private void OnGUI()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            GUILayout.Label("ILM Packer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(
                "Pack separate per-part grayscale masks into a single ILM RGBA texture.\n\n" +
                "ILM Channel Convention (LOCKED):\n" +
                "  R = specular / MatCap strength\n" +
                "  G = shadow-entry bias for Ramp U (neutral 0.5)\n" +
                "  B = specular width / secondary ramp (neutral 0.5)\n" +
                "  A = inner-line / outline-suppression mask\n\n" +
                "For each channel, assign a source texture and select which channel to read.\n" +
                "Neutral fallback: R=0, G=0.5, B=0.5, A=0.",
                MessageType.Info);

            EditorGUILayout.Space();
            GUILayout.Label("Channel Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawChannelRow("R (Specular / MatCap)", ref _rChannel);
            EditorGUILayout.Space();

            DrawChannelRow("G (Shadow Bias)", ref _gChannel);
            EditorGUILayout.Space();

            DrawChannelRow("B (Specular Width)", ref _bChannel);
            EditorGUILayout.Space();

            DrawChannelRow("A (Inner-line / Outline)", ref _aChannel);
            EditorGUILayout.Space();

            EditorGUILayout.Separator();
            EditorGUILayout.Space();
            GUILayout.Label("Output Settings", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _resolutionOption = (ResolutionOption)EditorGUILayout.EnumPopup("Resolution", _resolutionOption);

            EditorGUILayout.LabelField("Output Path");
            EditorGUILayout.BeginHorizontal();
            _outputPath = EditorGUILayout.TextField(_outputPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70)))
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "Save ILM Texture",
                    GetDefaultFileName(),
                    "png",
                    "Select output location for ILM texture");
                if (!string.IsNullOrEmpty(path))
                {
                    _outputPath = path;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.Separator();
            EditorGUILayout.Space();
            GUILayout.Label("Material Assignment (Optional)", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _targetMaterial = EditorGUILayout.ObjectField("Target Material", _targetMaterial, typeof(Material), false) as Material;
            _assignToMaterial = EditorGUILayout.Toggle("Assign to Material", _assignToMaterial);

            EditorGUILayout.Space();
            EditorGUILayout.Separator();
            EditorGUILayout.Space();

            if (GUILayout.Button("Pack ILM", GUILayout.Height(50)))
            {
                PackILM();
            }

            GUILayout.EndScrollView();
        }

        private void DrawChannelRow(string label, ref ChannelConfig config)
        {
            EditorGUILayout.BeginVertical("box");
            GUILayout.Label(label, EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Source:", GUILayout.Width(60));
            config.source = EditorGUILayout.ObjectField(config.source, typeof(Texture2D), false) as Texture2D;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Channel:", GUILayout.Width(60));
            config.sourceChannel = (SourceChannel)EditorGUILayout.EnumPopup(config.sourceChannel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Invert:", GUILayout.Width(60));
            config.invert = EditorGUILayout.Toggle(config.invert);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Default:", GUILayout.Width(60));
            config.defaultValue = EditorGUILayout.Slider(config.defaultValue, 0f, 1f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private string GetDefaultFileName()
        {
            if (_rChannel.source != null)
                return _rChannel.source.name + "_ILM.png";
            if (_gChannel.source != null)
                return _gChannel.source.name + "_ILM.png";
            if (_bChannel.source != null)
                return _bChannel.source.name + "_ILM.png";
            if (_aChannel.source != null)
                return _aChannel.source.name + "_ILM.png";
            return "ILM.png";
        }

        private int GetResolutionSize()
        {
            switch (_resolutionOption)
            {
                case ResolutionOption._256:
                    return 256;
                case ResolutionOption._512:
                    return 512;
                case ResolutionOption._1024:
                    return 1024;
                case ResolutionOption._2048:
                    return 2048;
                case ResolutionOption.Auto:
                default:
                    return GetAutoResolution();
            }
        }

        private int GetAutoResolution()
        {
            int maxSize = 0;

            if (_rChannel.source != null)
                maxSize = Mathf.Max(maxSize, Mathf.Max(_rChannel.source.width, _rChannel.source.height));
            if (_gChannel.source != null)
                maxSize = Mathf.Max(maxSize, Mathf.Max(_gChannel.source.width, _gChannel.source.height));
            if (_bChannel.source != null)
                maxSize = Mathf.Max(maxSize, Mathf.Max(_bChannel.source.width, _bChannel.source.height));
            if (_aChannel.source != null)
                maxSize = Mathf.Max(maxSize, Mathf.Max(_aChannel.source.width, _aChannel.source.height));

            if (maxSize <= 0)
                return 1024;

            // 올림해서 2의 거듭제곱으로 맞춘다
            int size = 1;
            while (size < maxSize)
                size *= 2;
            return size;
        }

        private void PackILM()
        {
            bool hasAnySource = _rChannel.source != null || _gChannel.source != null || 
                                _bChannel.source != null || _aChannel.source != null;

            if (!hasAnySource)
            {
                EditorUtility.DisplayDialog(
                    "Warning",
                    "No source textures assigned. Packing a neutral fallback texture (0, 0.5, 0.5, 0).",
                    "OK");
            }

            // 소스가 sRGB로 임포트되어 있으면 Blit 샘플 시 감마 디코드되어 값이 왜곡된다 (Codex 지적 #2).
            // ILM은 데이터 맵이므로 선형이어야 한다. sRGB 소스를 감지해 경고/중단한다.
            if (!WarnIfSourcesAreSRGB())
                return;

            string outputPath = _outputPath;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = GetDefaultOutputPath();
            }

            if (string.IsNullOrWhiteSpace(outputPath) || !outputPath.Replace('\\', '/').StartsWith("Assets/"))
            {
                EditorUtility.DisplayDialog("Error", "Output path must be a project-relative path under 'Assets/'. Please set the output location.", "OK");
                return;
            }

            int size = GetResolutionSize();

            Texture2D result = null;
            try
            {
                result = PackChannels(size);

                // 인코드 및 저장
                byte[] pngBytes = result.EncodeToPNG();
                File.WriteAllBytes(outputPath, pngBytes);

                // 오프라인 변환: 비선형 임포트 강제 + 데이터로서 처리
                AssetDatabase.ImportAsset(outputPath);
                TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
                if (importer != null)
                {
                    importer.sRGBTexture = false;
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency = false;
                    importer.mipmapEnabled = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }

                // 머티리얼에 할당 (옵션)
                if (_assignToMaterial && _targetMaterial != null)
                {
                    Texture2D loadedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                    _targetMaterial.SetTexture("_ILMMap", loadedTexture);
                    _targetMaterial.SetFloat("_UseILM", 1f);
                    _targetMaterial.EnableKeyword("_USE_ILM");
                    EditorUtility.SetDirty(_targetMaterial);
                }

                // 리로드 및 선택
                AssetDatabase.Refresh();
                Texture2D finalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                if (finalTexture != null)
                {
                    EditorGUIUtility.PingObject(finalTexture);
                    Selection.activeObject = finalTexture;
                }

                EditorUtility.DisplayDialog(
                    "Success",
                    $"ILM texture packed to:\n{outputPath}\n\n" +
                    $"Resolution: {size}x{size}\n" +
                    ((_assignToMaterial && _targetMaterial != null) ? $"Assigned to: {_targetMaterial.name}" : "Not assigned to material"),
                    "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to pack ILM texture:\n{ex.Message}", "OK");
            }
            finally
            {
                // 예외 경로 포함, 임시 결과 텍스처를 항상 해제 (Codex 지적 #3)
                if (result != null)
                    DestroyImmediate(result);
            }
        }

        /// <summary>
        /// 할당된 소스 중 sRGB로 임포트된 것이 있으면 경고 다이얼로그를 띄운다.
        /// 반환값: 계속 진행하면 true, 취소면 false.
        /// </summary>
        private bool WarnIfSourcesAreSRGB()
        {
            var offenders = new System.Collections.Generic.List<string>();
            CheckSRGB(_rChannel.source, offenders);
            CheckSRGB(_gChannel.source, offenders);
            CheckSRGB(_bChannel.source, offenders);
            CheckSRGB(_aChannel.source, offenders);

            if (offenders.Count == 0)
                return true;

            return EditorUtility.DisplayDialog(
                "sRGB sources detected",
                "These source textures are imported as sRGB (color), but ILM is DATA and must be linear.\n" +
                "Blitting them linearizes RGB on sample, so packed values will be gamma-wrong " +
                "(e.g. 0.5 -> ~0.214).\n\n" +
                "Set their import to Linear (sRGB off) first — see the PSD import-settings pass.\n\n" +
                string.Join("\n", offenders) +
                "\n\nPack anyway?",
                "Pack anyway",
                "Cancel");
        }

        private static void CheckSRGB(Texture2D source, System.Collections.Generic.List<string> offenders)
        {
            if (source == null)
                return;
            string path = AssetDatabase.GetAssetPath(source);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.sRGBTexture)
                offenders.Add("  • " + source.name);
        }

        private Texture2D PackChannels(int size)
        {
            Texture2D outputTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            Color[] outputPixels = new Color[size * size];

            // 각 채널 소스를 RenderTexture로 읽는다 (Read/Write 불필요)
            Color[] rPixels = ReadTextureChannel(_rChannel, size);
            Color[] gPixels = ReadTextureChannel(_gChannel, size);
            Color[] bPixels = ReadTextureChannel(_bChannel, size);
            Color[] aPixels = ReadTextureChannel(_aChannel, size);

            // 각 픽셀을 조합한다
            for (int i = 0; i < size * size; i++)
            {
                float rValue = rPixels != null ? rPixels[i].r : _rChannel.defaultValue;
                float gValue = gPixels != null ? gPixels[i].r : _gChannel.defaultValue;
                float bValue = bPixels != null ? bPixels[i].r : _bChannel.defaultValue;
                float aValue = aPixels != null ? aPixels[i].r : _aChannel.defaultValue;

                outputPixels[i] = new Color(rValue, gValue, bValue, aValue);
            }

            outputTexture.SetPixels(outputPixels);
            outputTexture.Apply();

            return outputTexture;
        }

        private Color[] ReadTextureChannel(ChannelConfig config, int targetSize)
        {
            if (config.source == null)
                return null;

            RenderTexture rt = null;
            Texture2D temp = null;
            RenderTexture prevActive = RenderTexture.active;

            try
            {
                // 소스를 선형 RenderTexture로 블릿 (타겟 크기로 스케일)
                rt = RenderTexture.GetTemporary(targetSize, targetSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Graphics.Blit(config.source, rt);

                // Linear RenderTexture에서 픽셀 읽기
                RenderTexture.active = rt;
                temp = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false, true);
                temp.ReadPixels(new Rect(0, 0, targetSize, targetSize), 0, 0);
                temp.Apply();

                Color[] pixels = temp.GetPixels();

                // 채널 선택 및 반전 처리
                for (int i = 0; i < pixels.Length; i++)
                {
                    float value = 0f;
                    switch (config.sourceChannel)
                    {
                        case SourceChannel.R:
                            value = pixels[i].r;
                            break;
                        case SourceChannel.G:
                            value = pixels[i].g;
                            break;
                        case SourceChannel.B:
                            value = pixels[i].b;
                            break;
                        case SourceChannel.A:
                            value = pixels[i].a;
                            break;
                        case SourceChannel.Grayscale:
                            value = (pixels[i].r + pixels[i].g + pixels[i].b) / 3f;
                            break;
                    }

                    if (config.invert)
                        value = 1f - value;

                    pixels[i] = new Color(value, value, value, value);
                }

                return pixels;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
                if (temp != null)
                    DestroyImmediate(temp);
            }
        }

        private string GetDefaultOutputPath()
        {
            // 첫 번째 할당된 소스의 경로 폴더 사용
            string folderPath = null;

            if (_rChannel.source != null)
                folderPath = AssetDatabase.GetAssetPath(_rChannel.source);
            else if (_gChannel.source != null)
                folderPath = AssetDatabase.GetAssetPath(_gChannel.source);
            else if (_bChannel.source != null)
                folderPath = AssetDatabase.GetAssetPath(_bChannel.source);
            else if (_aChannel.source != null)
                folderPath = AssetDatabase.GetAssetPath(_aChannel.source);

            if (string.IsNullOrEmpty(folderPath))
                folderPath = "Assets";
            else
                folderPath = Path.GetDirectoryName(folderPath);

            string fileName = GetDefaultFileName();
            return Path.Combine(folderPath, fileName).Replace('\\', '/');
        }
    }
}
