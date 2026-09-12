using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RunawayChimps.Surveillance.Editor
{
    public class SurveillanceStillCaptureWindow : EditorWindow
    {
        private const string DefaultOutputFolder = "Assets/Art/Surveillance/Captures";

        [SerializeField] private Camera captureCamera;
        [SerializeField] private string fileName = "Sector01_CageRoom_01";
        [SerializeField] private int captureWidth = 1024;

        [MenuItem("Tools/Runaway Chimps/Surveillance/Capture Still...")]
        public static void Open()
        {
            var window = GetWindow<SurveillanceStillCaptureWindow>("Surveillance Capture");
            window.minSize = new Vector2(390f, 205f);
            window.TryUseSelectedCamera();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Recorded Surveillance Still", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select a scene Camera, frame the surveillance view, then capture a 4:3 PNG. " +
                "The tool saves only the clean camera image; grain and the sector label stay dynamic on the Hub monitor.",
                MessageType.Info);

            captureCamera = (Camera)EditorGUILayout.ObjectField("Capture Camera", captureCamera, typeof(Camera), true);
            fileName = EditorGUILayout.TextField("File Name", fileName);
            captureWidth = Mathf.Clamp(EditorGUILayout.IntField("Width", captureWidth), 256, 4096);
            int captureHeight = Mathf.RoundToInt(captureWidth * 0.75f);
            EditorGUILayout.LabelField("Output", $"{captureWidth} × {captureHeight} PNG (4:3)");
            EditorGUILayout.LabelField("Folder", DefaultOutputFolder);

            using (new EditorGUI.DisabledScope(captureCamera == null || string.IsNullOrWhiteSpace(fileName)))
            {
                if (GUILayout.Button("Capture Surveillance Still", GUILayout.Height(34f)))
                {
                    Capture(captureHeight);
                }
            }
        }

        private void TryUseSelectedCamera()
        {
            if (Selection.activeGameObject == null)
            {
                return;
            }

            Camera selectedCamera = Selection.activeGameObject.GetComponent<Camera>();
            if (selectedCamera != null)
            {
                captureCamera = selectedCamera;
            }
        }

        private void Capture(int captureHeight)
        {
            if (captureCamera == null)
            {
                ShowNotification(new GUIContent("Choose a capture Camera first."));
                return;
            }

            string safeFileName = SanitizeFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
            {
                ShowNotification(new GUIContent("Enter a valid file name."));
                return;
            }

            EnsureAssetFolder(DefaultOutputFolder);
            string assetPath = $"{DefaultOutputFolder}/{safeFileName}.png";
            string absolutePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);

            RenderTexture previousTarget = captureCamera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var renderTexture = RenderTexture.GetTemporary(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);

            try
            {
                captureCamera.targetTexture = renderTexture;
                RenderTexture.active = renderTexture;
                captureCamera.Render();
                texture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0, false);
                texture.Apply(false, false);

                byte[] png = texture.EncodeToPNG();
                File.WriteAllBytes(absolutePath, png);
            }
            finally
            {
                captureCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTexture);
                DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            ConfigureImporter(assetPath);

            Texture2D captured = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            Selection.activeObject = captured;
            EditorGUIUtility.PingObject(captured);
            Debug.Log($"[SurveillanceCapture] Saved {captureWidth}x{captureHeight} still to {assetPath}", captured);
        }

        private static void ConfigureImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static string SanitizeFileName(string raw)
        {
            string trimmed = raw == null ? string.Empty : raw.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                trimmed = trimmed.Replace(invalid.ToString(), string.Empty);
            }

            return trimmed;
        }

        private static void EnsureAssetFolder(string assetFolder)
        {
            string[] parts = assetFolder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }
    }
}
