using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RunawayChimps.EditorTools
{
    internal static class LaunchPresentationSettings
    {
        internal const string SplashAssetPath = "Assets/Branding/RunawayChimps_SystemSplash.png";
        private const string OpenXRSettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";
        private const string OculusProjectConfigPath = "Assets/Oculus/OculusProjectConfig.asset";
        private const string XRGeneralSettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
        private const string OpenXRLoaderPath = "Assets/XR/Loaders/OpenXRLoader.asset";
        private const string MetaAndroidFeatureName = "MetaXRFeature Android";
        private const string AndroidProvidersName = "Android Providers";

        [MenuItem("Tools/Runaway Chimps/Launch Presentation/Apply Quest System Splash")]
        private static void ApplyFromMenu()
        {
            if (Apply(saveAssets: true, logSuccess: true))
                EditorUtility.DisplayDialog("Runaway Chimps", "Quest system splash is wired for Meta OpenXR builds.", "OK");
        }

        [MenuItem("Tools/Runaway Chimps/Launch Presentation/Validate Quest System Splash")]
        private static void ValidateFromMenu()
        {
            bool valid = Validate(logSuccess: true);
            if (!valid)
                EditorUtility.DisplayDialog("Runaway Chimps", "Quest launch presentation is not fully configured. Check the Console for details.", "OK");
        }

        internal static bool Apply(bool saveAssets, bool logSuccess)
        {
            Texture2D splash = AssetDatabase.LoadAssetAtPath<Texture2D>(SplashAssetPath);
            if (splash == null)
            {
                Debug.LogError($"[LaunchPresentation] Missing Quest system splash texture at {SplashAssetPath}.");
                return false;
            }

            bool openXrAssigned = AssignOpenXRMetaSplash(splash);
            bool projectConfigAssigned = AssignOculusProjectConfigSplash(splash);
            if (!openXrAssigned || !projectConfigAssigned)
                return false;

            if (saveAssets)
                AssetDatabase.SaveAssets();

            if (PlayerSettings.SplashScreen.show)
            {
                Debug.LogWarning(
                    "[LaunchPresentation] Unity's built-in splash is still enabled. Meta recommends using the system splash plus the custom startup scene. " +
                    "Disable the Unity splash in Player Settings when the active Unity license permits it; Unity 2022 Personal may enforce Unity branding.");
            }

            if (logSuccess)
                Debug.Log("[LaunchPresentation] Quest system splash assigned to Meta OpenXR + Oculus project config. Background remains black for this VR title.");
            return true;
        }

        internal static bool Validate(bool logSuccess)
        {
            Texture2D splash = AssetDatabase.LoadAssetAtPath<Texture2D>(SplashAssetPath);
            if (splash == null)
            {
                Debug.LogError($"[LaunchPresentation] Missing splash asset: {SplashAssetPath}.");
                return false;
            }

            bool openXrOk = IsAssigned(OpenXRSettingsPath, MetaAndroidFeatureName, "systemSplashScreen", splash);
            bool metaFeatureEnabled = IsBoolValue(OpenXRSettingsPath, MetaAndroidFeatureName, "m_enabled", true);
            bool projectConfigOk = IsAssigned(OculusProjectConfigPath, "OculusProjectConfig", "systemSplashScreen", splash);
            bool blackBackgroundOk = IsIntValue(OculusProjectConfigPath, "OculusProjectConfig", "_systemLoadingScreenBackground", 0);
            bool androidOpenXrOk = IsAndroidOpenXRLoaderConfigured();
            bool valid = openXrOk && metaFeatureEnabled && projectConfigOk && blackBackgroundOk && androidOpenXrOk;
            if (valid && logSuccess)
                Debug.Log("[LaunchPresentation] Quest launch configuration is valid: Android OpenXR + enabled Meta XR Feature + system splash + black compositor background.");
            return valid;
        }

        private static bool AssignOpenXRMetaSplash(Texture2D splash)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(OpenXRSettingsPath);
            foreach (Object asset in assets)
            {
                if (asset == null || asset.name != MetaAndroidFeatureName)
                    continue;

                SerializedObject serialized = new SerializedObject(asset);
                SerializedProperty property = serialized.FindProperty("systemSplashScreen");
                if (property == null)
                {
                    Debug.LogError("[LaunchPresentation] Meta OpenXR Android feature no longer exposes systemSplashScreen; update the launch setup for the installed Meta XR SDK.");
                    return false;
                }

                if (property.objectReferenceValue != splash)
                {
                    property.objectReferenceValue = splash;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                }
                return true;
            }

            Debug.LogError("[LaunchPresentation] Could not find MetaXRFeature Android in the OpenXR package settings.");
            return false;
        }

        private static bool AssignOculusProjectConfigSplash(Texture2D splash)
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(OculusProjectConfigPath);
            if (asset == null)
            {
                Debug.LogError("[LaunchPresentation] OculusProjectConfig.asset is missing.");
                return false;
            }

            SerializedObject serialized = new SerializedObject(asset);
            SerializedProperty splashProperty = serialized.FindProperty("systemSplashScreen");
            SerializedProperty backgroundProperty = serialized.FindProperty("_systemLoadingScreenBackground");
            if (splashProperty == null)
            {
                Debug.LogError("[LaunchPresentation] OculusProjectConfig no longer exposes systemSplashScreen.");
                return false;
            }

            if (splashProperty.objectReferenceValue != splash)
                splashProperty.objectReferenceValue = splash;
            if (backgroundProperty != null)
                backgroundProperty.intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return true;
        }

        private static bool IsAssigned(string path, string objectName, string propertyName, Object expected)
        {
            Object asset = FindNamedAsset(path, objectName);
            if (asset == null)
            {
                Debug.LogError($"[LaunchPresentation] Could not find {objectName} in {path}.");
                return false;
            }

            SerializedProperty property = new SerializedObject(asset).FindProperty(propertyName);
            if (property != null && property.objectReferenceValue == expected)
                return true;

            Debug.LogError($"[LaunchPresentation] {objectName}.{propertyName} is not assigned to {SplashAssetPath}.");
            return false;
        }

        private static bool IsBoolValue(string path, string objectName, string propertyName, bool expected)
        {
            Object asset = FindNamedAsset(path, objectName);
            SerializedProperty property = asset != null ? new SerializedObject(asset).FindProperty(propertyName) : null;
            if (property != null && property.boolValue == expected) return true;
            Debug.LogError($"[LaunchPresentation] {objectName}.{propertyName} must be {expected}.");
            return false;
        }

        private static bool IsIntValue(string path, string objectName, string propertyName, int expected)
        {
            Object asset = FindNamedAsset(path, objectName);
            SerializedProperty property = asset != null ? new SerializedObject(asset).FindProperty(propertyName) : null;
            if (property != null && property.intValue == expected) return true;
            Debug.LogError($"[LaunchPresentation] {objectName}.{propertyName} must be {expected}.");
            return false;
        }

        private static bool IsAndroidOpenXRLoaderConfigured()
        {
            Object expectedLoader = AssetDatabase.LoadAssetAtPath<Object>(OpenXRLoaderPath);
            Object providers = FindNamedAsset(XRGeneralSettingsPath, AndroidProvidersName);
            if (expectedLoader == null || providers == null)
            {
                Debug.LogError("[LaunchPresentation] Android XR settings or OpenXRLoader.asset is missing.");
                return false;
            }

            SerializedProperty loaders = new SerializedObject(providers).FindProperty("m_Loaders");
            if (loaders != null && loaders.isArray)
            {
                for (int i = 0; i < loaders.arraySize; i++)
                    if (loaders.GetArrayElementAtIndex(i).objectReferenceValue == expectedLoader)
                        return true;
            }

            Debug.LogError("[LaunchPresentation] Android XR Plug-in Management must include the project's OpenXRLoader.");
            return false;
        }

        private static Object FindNamedAsset(string path, string objectName)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset != null && asset.name == objectName)
                    return asset;
            return null;
        }
    }

    internal sealed class LaunchPresentationBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;

            if (!LaunchPresentationSettings.Validate(logSuccess: false))
                throw new BuildFailedException("Runaway Chimps Quest launch presentation is not configured correctly.");
        }
    }
}
