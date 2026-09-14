from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def write(path, text):
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(text, encoding="utf-8", newline="\n")


def replace_once(path, old, new):
    text = read(path)
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{path}: expected one replacement target, found {count}: {old[:100]!r}")
    write(path, text.replace(old, new, 1))


def append_once(path, marker, block):
    text = read(path)
    if marker in text:
        return
    write(path, text.rstrip() + "\n\n" + block.strip() + "\n")


# 1) Final reveal must retire the workstation instead of allowing LateUpdate to recreate it.
path = "Assets/Scripts/Loading/SecurityBootPresentation.cs"
replace_once(path,
    "        private bool startupMode;\n        private bool fading;\n        private bool readyLogged;",
    "        private bool startupMode;\n        private bool fading;\n        private bool workstationRetiredForReveal;\n        private bool readyLogged;")
replace_once(path,
    "        private void EnsureWorkstation()\n        {\n            if (!startupMode || workstation != null || boundCamera == null || presentationLayer < 0) return;",
    "        private void EnsureWorkstation()\n        {\n            if (!startupMode || workstationRetiredForReveal || workstation != null || boundCamera == null || presentationLayer < 0) return;")
replace_once(path,
    "                SetBackdropOpacity(1f - alpha);\n                if (alpha <= 0.001f) DestroyWorkstationForReveal();",
    "                SetBackdropOpacity(1f - alpha);\n                if (alpha <= 0.001f)\n                {\n                    workstationRetiredForReveal = true;\n                    DestroyWorkstationForReveal();\n                }")
replace_once(path,
    "        public void RestoreAfterInterruptedEntry()\n        {\n            fading = false;\n            if (workstation == null) EnsureWorkstation();",
    "        public void RestoreAfterInterruptedEntry()\n        {\n            fading = false;\n            workstationRetiredForReveal = false;\n            if (workstation == null) EnsureWorkstation();")

# 2) AppState exposes a narrow reset for a new Hub-room placement without disturbing level travel.
path = "Assets/Scripts/Bootstrap/AppState.cs"
replace_once(path,
    "    public void ResetPlayerReady()\n    {\n        IsReady = PhotonPlayerSpawned = PlayerVisualsReady = false;\n    }\n\n    // Stage markers",
    "    public void ResetPlayerReady()\n    {\n        IsReady = PhotonPlayerSpawned = PlayerVisualsReady = false;\n    }\n\n    public void ResetHubPlacementReady()\n    {\n        IsReady = false;\n        RigSnapped = false;\n    }\n\n    // Stage markers")

# 3) Replace code-owned slot coordinates/polling with demand-driven Photon claims + authored marker prefab.
allocator = r'''using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using RunawayChimps.Travel;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunawayChimps.Multiplayer
{
    public sealed class HubSpawnSlotAllocator : MonoBehaviourPunCallbacks
    {
        public const int SlotCount = 10;
        public const string PlayerSlotProperty = "rcHubSpawnSlot";
        private const string RoomSlotPrefix = "rcHubSlot";
        private const string LayoutResourcePath = "HubSpawn/HubSpawnSlots";
        private const float ClaimRetrySeconds = 0.35f;
        private const float PendingClaimTimeoutSeconds = 0.8f;

        private int localSlot = -1;
        private int pendingSlot = -1;
        private float pendingSince;
        private float nextClaimAt;
        private GameObject slotLayoutPrefab;
        private bool layoutErrorLogged;

        public int LocalSlot => localSlot;
        public bool HasLocalSlot => localSlot >= 0 && localSlot < SlotCount;

        public static void AddInitialRoomProperties(Hashtable properties)
        {
            if (properties == null) return;
            for (int i = 0; i < SlotCount; i++)
            {
                string key = SlotKey(i);
                if (!properties.ContainsKey(key)) properties[key] = 0;
            }
        }

        public bool TryGetLocalSpawnPose(Transform hubSpawn, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (hubSpawn == null || !TryGetLocalSlot(out int slot) || !TryGetAuthoredMarker(slot, out Transform marker))
                return false;

            position = hubSpawn.TransformPoint(marker.localPosition);
            Quaternion authoredRotation = hubSpawn.rotation * marker.localRotation;
            rotation = Quaternion.Euler(0f, authoredRotation.eulerAngles.y, 0f);
            return true;
        }

        public bool TryGetLocalSlot(out int slot)
        {
            slot = -1;
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null)
                return false;

            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            if (HasLocalSlot)
            {
                if (ReadOwner(localSlot) == actorNumber)
                {
                    slot = localSlot;
                    return true;
                }

                // The room changed or ownership was reconciled underneath us. Do not leave a
                // stale +Infinity retry gate behind; recover or claim again on demand.
                localSlot = -1;
                nextClaimAt = 0f;
            }

            if (TryFindOwnedSlot(actorNumber, out int recoveredSlot))
            {
                FinalizeLocalClaim(recoveredSlot);
                slot = localSlot;
                return true;
            }

            if (pendingSlot >= 0)
            {
                int owner = ReadOwner(pendingSlot);
                if (owner == actorNumber)
                {
                    FinalizeLocalClaim(pendingSlot);
                    slot = localSlot;
                    return true;
                }
                if (owner != 0 || Time.unscaledTime - pendingSince >= PendingClaimTimeoutSeconds)
                    pendingSlot = -1;
                else
                    return false;
            }

            if (Time.unscaledTime < nextClaimAt) return false;
            EnsureRoomSlotsInitialized();
            TryClaimFirstFreeSlot();
            return false;
        }

        public override void OnJoinedRoom()
        {
            base.OnJoinedRoom();
            ResetLocalClaim();
            EnsureRoomSlotsInitialized();
            ClearPersistedPlayerSlot();
            nextClaimAt = 0f;
            RefreshHubPlacementForNewRoom();
        }

        public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
        {
            base.OnRoomPropertiesUpdate(propertiesThatChanged);
            if (pendingSlot < 0 || !PhotonNetwork.InRoom || PhotonNetwork.LocalPlayer == null) return;
            string key = SlotKey(pendingSlot);
            if (!propertiesThatChanged.ContainsKey(key)) return;
            int owner = ReadOwner(pendingSlot);
            if (owner == PhotonNetwork.LocalPlayer.ActorNumber)
                FinalizeLocalClaim(pendingSlot);
            else if (owner != 0)
                pendingSlot = -1;
        }

        public override void OnPlayerLeftRoom(Player otherPlayer)
        {
            base.OnPlayerLeftRoom(otherPlayer);
            if (PhotonNetwork.IsMasterClient && otherPlayer != null)
                ReleaseActorClaims(otherPlayer.ActorNumber);
        }

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            base.OnMasterClientSwitched(newMasterClient);
            if (PhotonNetwork.IsMasterClient)
            {
                EnsureRoomSlotsInitialized();
                ReconcileOrphanedClaims();
            }
        }

        public override void OnLeftRoom()
        {
            base.OnLeftRoom();
            ResetLocalClaim();
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            base.OnDisconnected(cause);
            ResetLocalClaim();
        }

        private bool TryGetAuthoredMarker(int slot, out Transform marker)
        {
            marker = null;
            if (slot < 0 || slot >= SlotCount) return false;
            if (slotLayoutPrefab == null)
                slotLayoutPrefab = Resources.Load<GameObject>(LayoutResourcePath);
            if (slotLayoutPrefab == null || slotLayoutPrefab.transform.childCount != SlotCount)
            {
                LogLayoutError("Hub spawn slot layout must contain exactly 10 authored markers.");
                return false;
            }

            marker = slotLayoutPrefab.transform.GetChild(slot);
            string expectedName = $"HubSpawnSlot_{slot + 1:00}";
            if (marker == null || marker.name != expectedName)
            {
                LogLayoutError($"Hub spawn marker {slot} must be named '{expectedName}'.");
                marker = null;
                return false;
            }
            return true;
        }

        private void LogLayoutError(string message)
        {
            if (layoutErrorLogged) return;
            layoutErrorLogged = true;
            Debug.LogError("[HubSpawnSlotAllocator] " + message, this);
        }

        private void RefreshHubPlacementForNewRoom()
        {
            var snapper = GetComponent<RigSpawnSnapper>();
            if (snapper == null) return;
            Scene hub = SceneManager.GetSceneByName(snapper.HubSceneName);
            if (!hub.isLoaded || (SectorTravelService.I != null && SectorTravelService.I.IsBusy)) return;

            AppState.I?.ResetHubPlacementReady();
            snapper.RetrySnap();
        }

        private void ClearPersistedPlayerSlot()
        {
            if (PhotonNetwork.LocalPlayer == null) return;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [PlayerSlotProperty] = -1 });
        }

        private void EnsureRoomSlotsInitialized()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || !PhotonNetwork.IsMasterClient) return;
            var missing = new Hashtable();
            for (int i = 0; i < SlotCount; i++)
            {
                string key = SlotKey(i);
                if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(key)) missing[key] = 0;
            }
            if (missing.Count > 0) PhotonNetwork.CurrentRoom.SetCustomProperties(missing);
        }

        private bool TryFindOwnedSlot(int actorNumber, out int slot)
        {
            slot = -1;
            if (actorNumber <= 0) return false;
            for (int i = 0; i < SlotCount; i++)
            {
                if (ReadOwner(i) != actorNumber) continue;
                slot = i;
                return true;
            }
            return false;
        }

        private void TryClaimFirstFreeSlot()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || PhotonNetwork.LocalPlayer == null) return;
            int actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            for (int i = 0; i < SlotCount; i++)
            {
                string key = SlotKey(i);
                if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(key) || ReadOwner(i) != 0) continue;
                var desired = new Hashtable { [key] = actorNumber };
                var expected = new Hashtable { [key] = 0 };
                if (PhotonNetwork.CurrentRoom.SetCustomProperties(desired, expected))
                {
                    pendingSlot = i;
                    pendingSince = Time.unscaledTime;
                    nextClaimAt = Time.unscaledTime + ClaimRetrySeconds;
                    return;
                }
            }
            if (PhotonNetwork.IsMasterClient) ReconcileOrphanedClaims();
            nextClaimAt = Time.unscaledTime + ClaimRetrySeconds;
        }

        private void FinalizeLocalClaim(int slot)
        {
            localSlot = slot;
            pendingSlot = -1;
            nextClaimAt = float.PositiveInfinity;
            if (PhotonNetwork.LocalPlayer != null)
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [PlayerSlotProperty] = slot });
            Debug.Log($"[HubSpawnSlotAllocator] Actor {PhotonNetwork.LocalPlayer?.ActorNumber} reserved Hub spawn slot {slot}.", this);
        }

        private void ReleaseActorClaims(int actorNumber)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || actorNumber <= 0) return;
            for (int i = 0; i < SlotCount; i++)
            {
                string key = SlotKey(i);
                if (ReadOwner(i) != actorNumber) continue;
                PhotonNetwork.CurrentRoom.SetCustomProperties(
                    new Hashtable { [key] = 0 },
                    new Hashtable { [key] = actorNumber });
            }
        }

        private void ReconcileOrphanedClaims()
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || !PhotonNetwork.IsMasterClient) return;
            for (int i = 0; i < SlotCount; i++)
            {
                int owner = ReadOwner(i);
                if (owner <= 0 || PhotonNetwork.CurrentRoom.Players.ContainsKey(owner)) continue;
                string key = SlotKey(i);
                PhotonNetwork.CurrentRoom.SetCustomProperties(
                    new Hashtable { [key] = 0 },
                    new Hashtable { [key] = owner });
            }
        }

        private int ReadOwner(int slot)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null || slot < 0 || slot >= SlotCount)
                return -1;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SlotKey(slot), out object raw))
                return -1;
            return raw is int actor ? actor : 0;
        }

        private void ResetLocalClaim()
        {
            localSlot = -1;
            pendingSlot = -1;
            pendingSince = 0f;
            nextClaimAt = 0f;
        }

        private static string SlotKey(int slot) => RoomSlotPrefix + slot;
    }
}
'''
current_allocator = read("Assets/Scripts/Bootstrap/HubSpawnSlotAllocator.cs")
if "private static readonly Vector3[] SlotOffsets" not in current_allocator or "private void Update()" not in current_allocator:
    raise RuntimeError("HubSpawnSlotAllocator no longer matches the reviewed pre-hardening shape.")
write("Assets/Scripts/Bootstrap/HubSpawnSlotAllocator.cs", allocator)

# Authored Hub-relative marker prefab. Designers can move/rotate these markers in Unity without editing networking code.
folder_meta = """fileFormatVersion: 2\nguid: 6f27c4a2f98c4bbfb52b6e3e2a24f3c1\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"""
prefab_meta = """fileFormatVersion: 2\nguid: b9f2d7c3a1e84bb79a35d119c80e4a62\nPrefabImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"""
positions = [
    (0.0, 0.0, 0.0),
    (-1.10, 0.0, 0.55),
    (1.10, 0.0, 0.55),
    (-0.55, 0.0, 1.55),
    (0.55, 0.0, 1.55),
    (-1.65, 0.0, 1.55),
    (1.65, 0.0, 1.55),
    (-1.10, 0.0, 2.55),
    (0.0, 0.0, 2.55),
    (1.10, 0.0, 2.55),
]
root_go = 100100000
root_tr = 400100000
child_go_ids = [100100001 + i for i in range(10)]
child_tr_ids = [400100001 + i for i in range(10)]
parts = ["%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"]
parts.append(f"""--- !u!1 &{root_go}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n  - component: {{fileID: {root_tr}}}\n  m_Layer: 0\n  m_Name: HubSpawnSlots\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n--- !u!4 &{root_tr}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {root_go}}}\n  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: 0, y: 0, z: 0}}\n  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children:\n""")
for tr_id in child_tr_ids:
    parts.append(f"  - {{fileID: {tr_id}}}\n")
parts.append("  m_Father: {fileID: 0}\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n")
for i, ((x, y, z), go_id, tr_id) in enumerate(zip(positions, child_go_ids, child_tr_ids), start=1):
    parts.append(f"""--- !u!1 &{go_id}\nGameObject:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  serializedVersion: 6\n  m_Component:\n  - component: {{fileID: {tr_id}}}\n  m_Layer: 0\n  m_Name: HubSpawnSlot_{i:02d}\n  m_TagString: Untagged\n  m_Icon: {{fileID: 0}}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: 1\n--- !u!4 &{tr_id}\nTransform:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {{fileID: {go_id}}}\n  serializedVersion: 2\n  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}\n  m_LocalPosition: {{x: {x}, y: {y}, z: {z}}}\n  m_LocalScale: {{x: 1, y: 1, z: 1}}\n  m_ConstrainProportionsScale: 0\n  m_Children: []\n  m_Father: {{fileID: {root_tr}}}\n  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}\n""")
write("Assets/Resources/HubSpawn.meta", folder_meta)
write("Assets/Resources/HubSpawn/HubSpawnSlots.prefab", "".join(parts))
write("Assets/Resources/HubSpawn/HubSpawnSlots.prefab.meta", prefab_meta)

# 4) Quest Android build validation now checks the complete active path, not just texture references.
launch_settings = r'''using UnityEditor;
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
'''
current_launch_settings = read("Assets/Scripts/Editor/LaunchPresentationSettings.cs")
if "IPreprocessBuildWithReport" not in current_launch_settings or "AssignOpenXRMetaSplash" not in current_launch_settings:
    raise RuntimeError("LaunchPresentationSettings no longer matches the reviewed source shape.")
write("Assets/Scripts/Editor/LaunchPresentationSettings.cs", launch_settings)

# 5) Avoid allocating renderer material arrays each startup frame.
visual_ready = r'''using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class PlayerVisualReadyReporter : MonoBehaviour
{
    [Tooltip("How many frames the renderers must remain unchanged before we signal ready.")]
    public int stableFramesRequired = 20;

    [Tooltip("Extra delay after stability (helps shader upload settle).")]
    public float extraSecondsAfterStable = 0.1f;

    private Renderer[] _renderers;
    private PhotonView ownerView;
    private Room reportingRoom;
    private readonly List<Material> materialScratch = new List<Material>(8);

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        ownerView = GetComponentInParent<PhotonView>();
    }

    private void OnEnable() => BeginReporting();

    public void BeginReporting()
    {
        StopAllCoroutines();
        reportingRoom = PhotonNetwork.CurrentRoom;
        if (!CanReportReady()) return;
        StartCoroutine(CoWaitForVisualsToSettle());
    }

    private bool CanReportReady() => isActiveAndEnabled && ownerView != null && ownerView.IsMine &&
        PhotonNetwork.InRoom && ReferenceEquals(reportingRoom, PhotonNetwork.CurrentRoom);

    private void MarkReady()
    {
        if (!CanReportReady()) return;
        AppState.I?.MarkPlayerVisualsReady();
        AppState.I?.TryMarkReady();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        reportingRoom = null;
        materialScratch.Clear();
    }

    private IEnumerator CoWaitForVisualsToSettle()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        if (!CanReportReady()) yield break;

        if (_renderers == null || _renderers.Length == 0)
        {
            MarkReady();
            yield break;
        }

        int stable = 0;
        int lastHash = ComputeMaterialsHash();
        float deadline = Time.realtimeSinceStartup + 8f;

        while (stable < stableFramesRequired)
        {
            yield return null;
            if (!CanReportReady()) yield break;

            if (Time.realtimeSinceStartup >= deadline)
            {
                Debug.LogWarning("Player materials continue changing; finishing visual setup after the settle limit.", this);
                break;
            }

            int hash = ComputeMaterialsHash();
            if (hash == lastHash)
            {
                stable++;
            }
            else
            {
                stable = 0;
                lastHash = hash;
            }
        }

        if (extraSecondsAfterStable > 0f)
            yield return new WaitForSecondsRealtime(extraSecondsAfterStable);

        if (!CanReportReady()) yield break;
        Debug.Log("[PlayerVisualReadyReporter] Visuals stable. Marking ready.");
        MarkReady();
    }

    private int ComputeMaterialsHash()
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null) continue;

                materialScratch.Clear();
                renderer.GetSharedMaterials(materialScratch);
                h = h * 31 + materialScratch.Count;

                for (int m = 0; m < materialScratch.Count; m++)
                {
                    Material material = materialScratch[m];
                    h = h * 31 + (material != null ? material.GetInstanceID() : 0);
                    if (material != null && material.mainTexture != null)
                        h = h * 31 + material.mainTexture.GetInstanceID();
                }
            }
            return h;
        }
    }
}
'''
current_visual_ready = read("Assets/Scripts/PlayerScripts/PlayerVisualReadyReporter.cs")
if "var mats = r.sharedMaterials;" not in current_visual_ready:
    raise RuntimeError("PlayerVisualReadyReporter no longer contains the reviewed allocation hotspot.")
write("Assets/Scripts/PlayerScripts/PlayerVisualReadyReporter.cs", visual_ready)

# 6) Strengthen source contracts around the new lifecycle/authoring/build behavior.
path = "Tools/validate_launch_presentation.py"
replace_once(path,
    'SPLASH_GUID = "73af3b98a31d49a6a0b674b50ed8d20c"\n',
    'SPLASH_GUID = "73af3b98a31d49a6a0b674b50ed8d20c"\nSLOT_PREFAB = ROOT / "Assets/Resources/HubSpawn/HubSpawnSlots.prefab"\n')
replace_once(path,
    '        "_systemLoadingScreenBackground",\n    ]:',
    '        "_systemLoadingScreenBackground",\n        "IsBoolValue",\n        "IsIntValue",\n        "IsAndroidOpenXRLoaderConfigured",\n        "Android Providers",\n        "OpenXRLoader.asset",\n    ]:')
replace_once(path,
    '        "SetBackdropOpacity(1f - alpha)",\n    ]:',
    '        "SetBackdropOpacity(1f - alpha)",\n        "workstationRetiredForReveal",\n        "workstationRetiredForReveal = true",\n        "workstationRetiredForReveal = false",\n    ]:')
replace_once(path,
    '    allocator = read("Assets/Scripts/Bootstrap/HubSpawnSlotAllocator.cs")\n    for token in ["SlotCount = 10", "SetCustomProperties(desired, expected)", "OnPlayerLeftRoom",\n                  "OnMasterClientSwitched", "TryGetLocalSpawnPose"]:\n        if token not in allocator:\n            errors.append(f"Hub spawn allocator missing {token!r}.")',
    '    allocator = read("Assets/Scripts/Bootstrap/HubSpawnSlotAllocator.cs")\n    for token in ["SlotCount = 10", "SetCustomProperties(desired, expected)", "OnPlayerLeftRoom",\n                  "OnMasterClientSwitched", "TryGetLocalSpawnPose", "LayoutResourcePath",\n                  "Resources.Load<GameObject>", "TryFindOwnedSlot", "ResetHubPlacementReady"]:\n        if token not in allocator:\n            errors.append(f"Hub spawn allocator missing {token!r}.")\n    if "private void Update()" in allocator or "SlotOffsets" in allocator:\n        errors.append("Hub spawn claims/layout must stay demand-driven and authored outside C#.")\n    if not SLOT_PREFAB.exists():\n        errors.append("Authored Hub spawn-slot prefab is missing.")\n    else:\n        slot_prefab = SLOT_PREFAB.read_text(encoding="utf-8-sig")\n        marker_names = re.findall(r"m_Name: HubSpawnSlot_\\d{2}", slot_prefab)\n        if len(marker_names) != 10 or len(set(marker_names)) != 10:\n            errors.append("HubSpawnSlots.prefab must contain exactly ten uniquely named authored markers.")')
replace_once(path,
    '    player_spawner = read("Assets/Resources/PhotonVR/Scripts/Player/PlayerSpawner.cs")\n    if "!AppState.I.RigSnapped" not in player_spawner:\n        errors.append("Photon avatar spawning must wait for the local Hub rig slot snap.")',
    '    player_spawner = read("Assets/Resources/PhotonVR/Scripts/Player/PlayerSpawner.cs")\n    if "!AppState.I.RigSnapped" not in player_spawner:\n        errors.append("Photon avatar spawning must wait for the local Hub rig slot snap.")\n    app_state = read("Assets/Scripts/Bootstrap/AppState.cs")\n    if "ResetHubPlacementReady" not in app_state or "RigSnapped = false" not in app_state:\n        errors.append("AppState must support invalidating Hub placement on a new Photon-room session.")\n    visuals = read("Assets/Scripts/PlayerScripts/PlayerVisualReadyReporter.cs")\n    if "GetSharedMaterials(materialScratch)" not in visuals or ".sharedMaterials" in visuals:\n        errors.append("Player visual readiness must reuse a shared-material list instead of allocating arrays each frame.")')

path = "Tools/validate_security_boot.py"
replace_once(path,
    '                 "SetBackdropOpacity(1f - alpha)", "DestroyWorkstationForReveal()",\n                 "legacyStatus.enabled = true"], "presentation")',
    '                 "SetBackdropOpacity(1f - alpha)", "DestroyWorkstationForReveal()",\n                 "workstationRetiredForReveal", "workstationRetiredForReveal = true",\n                 "workstationRetiredForReveal = false", "legacyStatus.enabled = true"], "presentation")')

# 7) Record the final hardening in maintained + focused docs.
design_block = r'''
## September 14 launch/session hardening after review

**Implemented on `feature/launch-presentation-polish` / PR #21:** the startup presentation now has an explicit final-reveal retirement state. Once the workstation is fully covered by black and removed, normal `LateUpdate` camera rebinding cannot reconstruct it while the Hub fade is underway. Only an actual interrupted entry/retry clears that retirement state and permits the local workstation to be rebuilt. This protects the confirmed workstation -> black -> workstation gone -> Hub reveal sequence.

**Implemented multiplayer lifecycle correction:** Hub placement is invalidated on each newly joined Photon room only when the Hub is loaded and ordinary sector travel is not in progress. `RigSpawnSnapper` then reserves/grounds against the new room's slot before the Hub avatar may spawn. Hub slot reservation is demand-driven by actual Hub placement rather than polled in every Photon room, so clients in Level 1/Level 2 do not reserve unused Hub slots. A client recovers an already-owned room slot before attempting another CAS claim, stale local slot state reopens its retry gate, and the persisted player slot property is cleared on a new room session.

**Implemented authored slot layout:** the ten Hub-relative cold-start poses now live as editable marker transforms in `Assets/Resources/HubSpawn/HubSpawnSlots.prefab` (`HubSpawnSlot_01` through `_10`) instead of coordinate constants in networking code. The current positions preserve the first-pass compact layout around the existing `HubSpawn`; their final physical clearance remains pending Unity/headset review.

**Implemented launch/build hardening:** Android launch validation now requires the committed splash assignment, enabled `MetaXRFeature Android`, black Meta loading background, and the Android OpenXR loader. `PlayerVisualReadyReporter` also reuses a `List<Material>` with `Renderer.GetSharedMaterials` during startup settling instead of allocating a fresh `sharedMaterials` array every frame.

**Pending validation:** Unity 2022.3.55f1 import/compile; Play Mode proof that the workstation never reappears during Hub reveal or retry; two-client same-time cold start; Hub room switch/rejoin/reconnect placement; slot reuse/Master handoff; authored marker floor/wall/prop clearance; and later Quest Android validation/device behavior. Source checks remain separate from those runtime results.
'''
append_once("docs/design-and-lore.md", "## September 14 launch/session hardening after review", design_block)

plan_block = r'''
## September 14 PR #21 post-review hardening

**Implemented:** final reveal now retires the temporary security workstation until an explicit interrupted-entry recovery, preventing the presentation update loop from reconstructing the desk after it was intentionally removed under black. New Photon-room sessions invalidate Hub placement only while the Hub is actually loaded, then require a fresh slot snap before local avatar instantiation. Slot acquisition is demand-driven; there is no allocator `Update` poll while players are in non-Hub sectors. Existing ownership is recovered before a new claim, stale claim state becomes retryable, and the carried player slot property is cleared for each room session.

**Implemented level-authoring boundary:** `HubSpawnSlotAllocator` no longer owns ten Vector3 offsets. `Assets/Resources/HubSpawn/HubSpawnSlots.prefab` is the editable source for the ten ordered Hub-relative marker transforms, while Photon room properties remain the synchronized ownership source. This separates level-layout tuning from network allocation logic.

**Implemented platform/performance hardening:** Android build preprocessing validates the enabled Meta Android OpenXR feature, configured OpenXR loader, committed system splash and black compositor background without mutating project settings. Startup avatar visual settling now calls `Renderer.GetSharedMaterials(List<Material>)` with a reused list instead of allocating material arrays every frame.

**Source validation required after this change:** rerun the full Source Integrity suite on the clean final PR head. **Runtime validation remains pending:** Unity import/compile, startup/retry/reveal, room switch/reconnect, simultaneous two-client slot claims, slot reuse/Master handoff, authored marker clearance, black-only sector travel, and Quest build/headset behavior.
'''
append_once("docs/repository-improvement-plan.md", "## September 14 PR #21 post-review hardening", plan_block)

focused_block = r'''
## September 14 post-review hardening

**Implemented:** the workstation enters a one-way `retired for reveal` state when the final terminal fade reaches black, so the presentation's normal camera-binding loop cannot recreate it during the Hub fade. An interrupted entry explicitly clears that state before rebuilding the local workstation.

Hub-room placement is now session-aware: each new Photon room invalidates the old Hub snap only when the Hub is loaded, then waits for a fresh room-owned slot before spawning the local network avatar. Slot claims are demand-driven, recover an existing local ownership first, and no longer run from an allocator `Update` loop in Level 1/Level 2.

The ten layout poses are authored as `Resources/HubSpawn/HubSpawnSlots.prefab` marker transforms rather than C# coordinates. Android build validation now verifies Meta Android feature enablement, black compositor background, and Android OpenXR loader in addition to the splash references. Player visual-settle hashing reuses a material list to avoid per-frame `sharedMaterials` array allocations.

**Pending validation:** Unity/Play Mode reveal and retry, Hub room switch/reconnect, simultaneous two-client claims, slot reuse/Master handoff, marker clearance, and Quest build/headset behavior.
'''
append_once("docs/launch-presentation.md", "## September 14 post-review hardening", focused_block)
append_once("docs/launch-review-resolution.md", "## September 14 post-review hardening", focused_block)
append_once("docs/launch-workstation-vignette.md", "## September 14 post-review hardening", focused_block)

print("Applied launch/session hardening patches.")
