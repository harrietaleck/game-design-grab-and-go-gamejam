using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Genies.Sdk.ArtForge.Editor.Core.Infrastructure.Processing
{
    public class TransparencyTool : EditorWindow
    {
        private Texture2D sourceTexture;
        private bool autoTransparency = true;
        private string outputSuffix = "_transparent";
        private Vector2 scrollPosition;

        private bool sourceFoldout = true;
        private bool transparencyFoldout = true;
        private bool backgroundFoldout = true;
        private bool maskFoldout = true;
        private bool edgeFoldout = true;
        private bool outputFoldout = true;

        private readonly TransparencyProcessor.Settings settings = new TransparencyProcessor.Settings();

        [MenuItem("Assets/Transparency Tool", true)]
        private static bool ValidateShowWindowFromSelection()
        {
            return Selection.activeObject is Texture2D;
        }

        [MenuItem("Assets/Transparency Tool")]
        private static void ShowWindowFromSelection()
        {
            var window = GetWindow<TransparencyTool>("Transparency Tool");
            window.TryAssignSelectedTexture();
            window.Focus();
        }

        private void OnEnable()
        {
            TryAssignSelectedTexture();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawSourceSection();
            DrawTransparencySection();
            DrawBackgroundSection();
            DrawMaskSection();
            DrawEdgeSection();
            DrawOutputSection();

            EditorGUILayout.EndScrollView();

            GUILayout.Space(6);
            DrawBottomActionBar();
        }

        private void DrawSourceSection()
        {
            sourceFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(sourceFoldout, "Source");
            if (sourceFoldout)
            {
                sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source Texture", sourceTexture, typeof(Texture2D), false);

                if (GUILayout.Button("Use Selected Texture"))
                {
                    TryAssignSelectedTexture();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(6);
        }

        private void DrawTransparencySection()
        {
            transparencyFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(transparencyFoldout, "Transparency");
            if (transparencyFoldout)
            {
                autoTransparency = EditorGUILayout.Toggle("Auto Transparency", autoTransparency);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(6);
        }

        private void DrawBackgroundSection()
        {
            backgroundFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(backgroundFoldout, "Background Detection");
            if (backgroundFoldout)
            {
                settings.useCornerSampling = EditorGUILayout.Toggle("Use Corner Sampling", settings.useCornerSampling);

                using (new EditorGUI.DisabledScope(!settings.useCornerSampling))
                {
                    settings.cornerSampleSize = EditorGUILayout.IntSlider("Corner Sample Size", settings.cornerSampleSize, 1, 64);
                    settings.useNearestCornerMatch = EditorGUILayout.Toggle("Use Nearest Corner Match", settings.useNearestCornerMatch);
                }

                using (new EditorGUI.DisabledScope(settings.useCornerSampling))
                {
                    settings.manualBackgroundColor = EditorGUILayout.ColorField("Manual Background Color", settings.manualBackgroundColor);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(6);
        }

        private void DrawMaskSection()
        {
            maskFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(maskFoldout, "Threshold and Mask");
            if (maskFoldout)
            {
                settings.useAutomaticThreshold = EditorGUILayout.Toggle("Automatic Threshold", settings.useAutomaticThreshold);
                using (new EditorGUI.DisabledScope(settings.useAutomaticThreshold))
                {
                    settings.tolerance = EditorGUILayout.Slider("Tolerance", settings.tolerance, 0f, 1f);
                }
                using (new EditorGUI.DisabledScope(!settings.useAutomaticThreshold))
                {
                    settings.automaticThresholdMin = EditorGUILayout.Slider("Auto Threshold Min", settings.automaticThresholdMin, 0f, 1f);
                    settings.automaticThresholdMax = EditorGUILayout.Slider("Auto Threshold Max", settings.automaticThresholdMax, 0f, 1f);
                    settings.automaticThresholdBias = EditorGUILayout.Slider("Auto Threshold Bias", settings.automaticThresholdBias, 0.25f, 2f);
                }

                settings.useFloodFillFromEdges = EditorGUILayout.Toggle("Use Flood Fill From Edges", settings.useFloodFillFromEdges);
                settings.invertAlpha = EditorGUILayout.Toggle("Invert Alpha", settings.invertAlpha);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(6);
        }

        private void DrawEdgeSection()
        {
            edgeFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(edgeFoldout, "Edge Processing");
            if (edgeFoldout)
            {
                settings.useEdgeDecontamination = EditorGUILayout.Toggle("Use Edge Decontamination", settings.useEdgeDecontamination);
                using (new EditorGUI.DisabledScope(!settings.useEdgeDecontamination))
                {
                    settings.edgeDecontaminationBandPixels = EditorGUILayout.IntSlider(
                        "Decontaminate Band (px)",
                        settings.edgeDecontaminationBandPixels,
                        1,
                        32);
                    settings.edgeDecontaminationColorDistanceMax = EditorGUILayout.Slider(
                        "BG Similarity Distance Max",
                        settings.edgeDecontaminationColorDistanceMax,
                        0.02f,
                        1f);
                    settings.edgeDecontaminationStrength = EditorGUILayout.Slider(
                        "Decontaminate Strength",
                        settings.edgeDecontaminationStrength,
                        0f,
                        4f);
                    settings.edgeDecontaminationAlphaReduction = EditorGUILayout.Slider(
                        "Alpha Reduction",
                        settings.edgeDecontaminationAlphaReduction,
                        0f,
                        4f);
                    settings.edgeDecontaminationMinUnmatteAlpha = EditorGUILayout.Slider(
                        "Min Unmatte Alpha",
                        settings.edgeDecontaminationMinUnmatteAlpha,
                        0.001f,
                        0.5f);
                    settings.edgeDecontaminationBackgroundAlphaThreshold = EditorGUILayout.Slider(
                        "BG Seed Alpha Threshold",
                        settings.edgeDecontaminationBackgroundAlphaThreshold,
                        0f,
                        0.2f);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            GUILayout.Space(6);
        }

        private void DrawOutputSection()
        {
            outputFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(outputFoldout, "Output");
            if (outputFoldout)
            {
                outputSuffix = EditorGUILayout.TextField("Output Suffix", outputSuffix);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private void DrawBottomActionBar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUI.enabled = sourceTexture != null && autoTransparency;
                if (GUILayout.Button("Generate Transparent PNG", GUILayout.Height(28)))
                {
                    GenerateTransparentPng();
                }
                GUI.enabled = true;
            }
        }

        private void TryAssignSelectedTexture()
        {
            if (Selection.activeObject is Texture2D selectedTexture)
            {
                sourceTexture = selectedTexture;
                Repaint();
            }
        }

        private void GenerateTransparentPng()
        {
            string inputPath = AssetDatabase.GetAssetPath(sourceTexture);
            if (string.IsNullOrEmpty(inputPath))
            {
                Debug.LogError("Could not resolve source texture path.");
                return;
            }

            TransparencyProcessor.Result result = null;
            Texture2D originalResolutionTexture = null;

            try
            {
                string fullInputPath = Path.GetFullPath(inputPath);
                if (!File.Exists(fullInputPath))
                {
                    Debug.LogError("Source texture file not found on disk.");
                    return;
                }

                byte[] sourceBytes = File.ReadAllBytes(fullInputPath);
                originalResolutionTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!originalResolutionTexture.LoadImage(sourceBytes, false))
                {
                    Debug.LogError("Failed to decode source texture at original resolution.");
                    return;
                }

                int originalWidth = originalResolutionTexture.width;
                int originalHeight = originalResolutionTexture.height;

                result = TransparencyProcessor.Process(originalResolutionTexture, settings);
                if (result == null || result.texture == null)
                {
                    Debug.LogError("Transparency processing returned no output texture.");
                    return;
                }

                if (result.texture.width != originalWidth || result.texture.height != originalHeight)
                {
                    Debug.LogError("Output resolution does not match source resolution.");
                    return;
                }

                byte[] pngBytes = result.texture.EncodeToPNG();
                if (pngBytes == null || pngBytes.Length == 0)
                {
                    Debug.LogError("Failed to encode output PNG.");
                    return;
                }

                string directory = Path.GetDirectoryName(inputPath);
                string fileName = Path.GetFileNameWithoutExtension(inputPath);
                string safeSuffix = outputSuffix ?? string.Empty;
                string outputPath = Path.Combine(directory ?? "Assets", fileName + safeSuffix + ".png").Replace('\\', '/');

                if (string.Equals(outputPath, inputPath, StringComparison.OrdinalIgnoreCase))
                {
                    outputPath = Path.Combine(directory ?? "Assets", fileName + "_transparent.png").Replace('\\', '/');
                }

                string fullOutputPath = Path.GetFullPath(outputPath);
                File.WriteAllBytes(fullOutputPath, pngBytes);
                AssetDatabase.Refresh();

                TextureImporter outputImporter = AssetImporter.GetAtPath(outputPath) as TextureImporter;
                if (outputImporter != null)
                {
                    outputImporter.textureType = TextureImporterType.Default;
                    outputImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                    outputImporter.alphaIsTransparency = true;
                    outputImporter.npotScale = TextureImporterNPOTScale.None;
                    outputImporter.maxTextureSize = ResolveMaxTextureSize(originalWidth, originalHeight);
                    outputImporter.mipmapEnabled = false;
                    outputImporter.SaveAndReimport();
                }

                Debug.Log("Transparent PNG created at: " + outputPath + $" ({originalWidth}x{originalHeight})");

                if (result.cornerColors != null)
                {
                    Debug.Log(
                        "Corner colors:\n" +
                        "Bottom Left: " + result.cornerColors[0] + "\n" +
                        "Bottom Right: " + result.cornerColors[1] + "\n" +
                        "Top Left: " + result.cornerColors[2] + "\n" +
                        "Top Right: " + result.cornerColors[3]
                    );
                }
                else
                {
                    Debug.Log("Manual background color used: " + result.backgroundColor);
                }

                Debug.Log("Applied tolerance: " + result.appliedTolerance);

                var outputTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);
                if (outputTexture != null)
                {
                    EditorGUIUtility.PingObject(outputTexture);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to generate transparent PNG: " + ex.Message);
            }
            finally
            {
                if (result?.texture != null)
                {
                    DestroyImmediate(result.texture);
                }

                if (originalResolutionTexture != null)
                {
                    DestroyImmediate(originalResolutionTexture);
                }
            }
        }

        private static int ResolveMaxTextureSize(int width, int height)
        {
            int longestEdge = Mathf.Max(width, height);
            int[] supported = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };
            for (int i = 0; i < supported.Length; i++)
            {
                if (supported[i] >= longestEdge)
                {
                    return supported[i];
                }
            }

            return 16384;
        }
    }
}
