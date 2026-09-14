#!/usr/bin/env python3
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


def write(path, text):
    (ROOT / path).write_text(text, encoding="utf-8")


def replace_once(text, old, new, name):
    count = text.count(old)
    if count != 1:
        raise RuntimeError(f"{name}: expected one match, found {count}")
    return text.replace(old, new, 1)

# SecurityWorkstationVignette: follow XROrigin root relocation and use referenced material.
p = "Assets/Scripts/Loading/SecurityWorkstationVignette.cs"
s = read(p)
s = replace_once(s, "using UnityEngine.SceneManagement;\n", "using Unity.XR.CoreUtils;\n", "workstation using")
s = replace_once(s,
'''        private readonly List<Material> runtimeMaterials = new List<Material>();\n        private Camera startupCamera;''',
'''        private const string BaseMaterialResourcePath = "LaunchPresentation/WorkstationBase";\n        private readonly List<Material> runtimeMaterials = new List<Material>();\n        private Camera startupCamera;\n        private Material baseMaterial;''', "workstation fields")
s = replace_once(s,
'''        public static SecurityWorkstationVignette Create(\n            Scene scene,\n            Camera camera,\n            TMP_FontAsset fontAsset,\n            int layer)\n        {\n            if (camera == null) return null;\n\n            var root = new GameObject("Security Workstation Vignette");\n            root.layer = layer;\n            SceneManager.MoveGameObjectToScene(root, scene);\n\n            var vignette = root.AddComponent<SecurityWorkstationVignette>();\n            vignette.Build(camera, fontAsset, layer);\n            return vignette;\n        }''',
'''        public static SecurityWorkstationVignette Create(\n            Camera camera,\n            TMP_FontAsset fontAsset,\n            int layer)\n        {\n            if (camera == null) return null;\n            var origin = camera.GetComponentInParent<XROrigin>();\n            if (origin == null) return null;\n\n            var root = new GameObject("Security Workstation Vignette");\n            root.layer = layer;\n            DontDestroyOnLoad(root);\n            root.transform.SetParent(origin.transform, true);\n\n            var vignette = root.AddComponent<SecurityWorkstationVignette>();\n            vignette.Build(camera, fontAsset, layer);\n            if (vignette.MonitorCanvasRoot != null) return vignette;\n            Destroy(root);\n            return null;\n        }''', "workstation create")
s = replace_once(s,
'''            startupCamera = camera;\n            font = fontAsset;\n            contentLayer = layer;\n\n            Vector3 forward''',
'''            startupCamera = camera;\n            font = fontAsset;\n            contentLayer = layer;\n            baseMaterial = Resources.Load<Material>(BaseMaterialResourcePath);\n            if (baseMaterial == null)\n            {\n                Debug.LogError($"Security workstation is missing Resources/{BaseMaterialResourcePath}.mat.", this);\n                return;\n            }\n\n            Vector3 forward''', "workstation material load")
s = replace_once(s,
'''            // Place once from the initial tracked pose. The desk is intentionally world-stationary after creation.\n            transform.position = camera.transform.position + forward * 1.95f + Vector3.down * 0.14f;\n            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);''',
'''            // Head motion does not move the desk, but hidden XROrigin relocation does because this root is parented to the origin.\n            transform.position = camera.transform.position + forward * 1.95f + Vector3.down * 0.14f;\n            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);''', "workstation anchor comment")
start = s.index("        private Material CreateMaterial")
end = s.index("        private GameObject Box", start)
s = s[:start] + '''        private Material CreateMaterial(string name, Color color)\n        {\n            if (baseMaterial == null) return null;\n            var material = new Material(baseMaterial)\n            {\n                name = name,\n                color = color,\n                hideFlags = HideFlags.DontSave\n            };\n            runtimeMaterials.Add(material);\n            return material;\n        }\n\n''' + s[end:]
write(p, s)

# SecurityBootPresentation: preserve legacy error fallback; destroy vignette under black and rebuild on interrupted entry.
p = "Assets/Scripts/Loading/SecurityBootPresentation.cs"
s = read(p)
s = replace_once(s, "        private Canvas hostCanvas;\n", "        private Canvas hostCanvas;\n        private TMP_Text legacyStatus;\n", "boot legacy field")
s = replace_once(s, "            hostCanvas = canvas;\n", "            hostCanvas = canvas;\n            legacyStatus = legacyStatusText;\n", "boot legacy assign")
s = replace_once(s,
'''            foreach (GameObject root in scene.GetRootGameObjects())\n            {\n                foreach (LoadingDebugText debug in root.GetComponentsInChildren<LoadingDebugText>(true))\n                    debug.enabled = false;\n                foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))\n                    label.enabled = false;\n            }\n\n            // Normal sector travel intentionally stops here: black authored backdrop only, no workstation/boot/audio.\n            if (!startupMode) return;''',
'''            foreach (GameObject root in scene.GetRootGameObjects())\n                foreach (LoadingDebugText debug in root.GetComponentsInChildren<LoadingDebugText>(true))\n                    debug.enabled = false;\n\n            // Normal sector travel intentionally stops here: black authored backdrop only, no workstation/boot/audio.\n            if (!startupMode)\n            {\n                DisableLoadingSceneText();\n                return;\n            }''', "boot defer labels")
s = replace_once(s,
'''            workstation = SecurityWorkstationVignette.Create(\n                gameObject.scene,\n                boundCamera,\n                font,\n                presentationLayer);\n            if (workstation == null || workstation.MonitorCanvasRoot == null) return;\n\n            BuildTerminal(workstation.MonitorCanvasRoot);\n            // Camera clear is black and only the dedicated presentation layer is visible, so the desk floats in safe darkness.\n            SetBackdropOpacity(0f);''',
'''            workstation = SecurityWorkstationVignette.Create(boundCamera, font, presentationLayer);\n            if (workstation == null || workstation.MonitorCanvasRoot == null)\n            {\n                if (legacyStatus != null) legacyStatus.enabled = true;\n                return;\n            }\n\n            BuildTerminal(workstation.MonitorCanvasRoot);\n            DisableLoadingSceneText();\n            // Camera clear is black and only the dedicated presentation layer is visible, so the desk floats in safe darkness.\n            SetBackdropOpacity(0f);''', "boot create workstation")
s = replace_once(s,
'''        public void Present(AppState state, bool hubLoaded, bool inRoom, string error, bool reviewHeld)\n        {\n            if (!startupMode || terminal == null) return;''',
'''        public void Present(AppState state, bool hubLoaded, bool inRoom, string error, bool reviewHeld)\n        {\n            if (!startupMode) return;\n            if (terminal == null)\n            {\n                if (legacyStatus != null)\n                {\n                    legacyStatus.enabled = true;\n                    string fallback = !string.IsNullOrEmpty(error) ? error :\n                        !hubLoaded ? "Loading security sector..." : state != null ? state.Status : "Waiting for Bootstrap...";\n                    if (string.IsNullOrEmpty(fallback)) fallback = "Waiting for startup services...";\n                    legacyStatus.text = string.IsNullOrEmpty(error) ? fallback : fallback + "\\nPress either trigger to retry. (Desktop: R)";\n                }\n                return;\n            }''', "boot legacy present")
s = replace_once(s,
'''                if (alpha <= 0.001f) workstation.gameObject.SetActive(false);\n                else if (!workstation.gameObject.activeSelf) workstation.gameObject.SetActive(true);''',
'''                if (alpha <= 0.001f) DestroyWorkstationForReveal();\n                else if (workstation != null && !workstation.gameObject.activeSelf) workstation.gameObject.SetActive(true);''', "boot destroy reveal")
s = replace_once(s,
'''        public void RestoreAfterInterruptedEntry()\n        {\n            fading = false;\n            if (workstation != null)''',
'''        public void RestoreAfterInterruptedEntry()\n        {\n            fading = false;\n            if (workstation == null) EnsureWorkstation();\n            if (workstation != null)''', "boot rebuild interrupt")
s = replace_once(s,
'''        private void OnDestroy()\n        {\n            RestoreCameraForReveal();\n            if (bootAudio != null) bootAudio.Stop();\n            if (tick != null) Destroy(tick);\n            if (staticTexture != null) Destroy(staticTexture);\n            if (workstation != null) Destroy(workstation.gameObject);\n        }\n\n        private static void SetLayerRecursively''',
'''        private void OnDestroy()\n        {\n            RestoreCameraForReveal();\n            if (bootAudio != null) bootAudio.Stop();\n            if (tick != null) Destroy(tick);\n            DestroyWorkstationForReveal();\n        }\n\n        private void DestroyWorkstationForReveal()\n        {\n            if (workstation != null) Destroy(workstation.gameObject);\n            workstation = null;\n            terminal = null;\n            terminalGroup = null;\n            headline = detail = instruction = null;\n            cursor = null;\n            staticNoise = null;\n            interferenceLine = null;\n            if (staticTexture != null) Destroy(staticTexture);\n            staticTexture = null;\n            staticPixels = null;\n            for (int i = 0; i < stageLabels.Length; i++)\n            {\n                stageLabels[i] = null;\n                stageLights[i] = null;\n            }\n        }\n\n        private void DisableLoadingSceneText()\n        {\n            foreach (GameObject root in gameObject.scene.GetRootGameObjects())\n                foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))\n                    label.enabled = false;\n        }\n\n        private static void SetLayerRecursively''', "boot cleanup helpers")
write(p, s)

# RigSpawnSnapper: wait for a unique Photon room slot and snap to its pose.
p = "Assets/Scripts/Bootstrap/RigSpawnSnapper.cs"
s = read(p)
s = replace_once(s, "using UnityEngine.SceneManagement;\n", "using UnityEngine.SceneManagement;\nusing RunawayChimps.Multiplayer;\n", "rig using")
s = replace_once(s,
'''    [Min(1)] public int FixedSettleSteps = 3;''',
'''    [Min(1)] public int FixedSettleSteps = 3;\n    [Min(1f)] public float SpawnSlotWaitSeconds = 12f;''', "rig slot timeout")
s = replace_once(s, "    private bool _snapping;\n", "    private bool _snapping;\n    private HubSpawnSlotAllocator _spawnSlots;\n", "rig allocator field")
s = replace_once(s,
'''        if (xrOrigin == null)\n            xrOrigin = GetComponent<XROrigin>();''',
'''        if (xrOrigin == null)\n            xrOrigin = GetComponent<XROrigin>();\n        _spawnSlots = GetComponent<HubSpawnSlotAllocator>();\n        if (_spawnSlots == null) _spawnSlots = gameObject.AddComponent<HubSpawnSlotAllocator>();''', "rig allocator awake")
needle = '''        if (gorillaBodyCapsule == null)\n        {\n            Debug.LogError("[RigSpawnSnapper] Missing GorillaPlayer body capsule.");\n            AppState.I?.Fail("The player body collider is missing.");\n            _snapping = false;\n            yield break;\n        }\n\n        // GorillaPlayer is currently also the XROrigin CameraFloorOffsetObject.'''
repl = '''        if (gorillaBodyCapsule == null)\n        {\n            Debug.LogError("[RigSpawnSnapper] Missing GorillaPlayer body capsule.");\n            AppState.I?.Fail("The player body collider is missing.");\n            _snapping = false;\n            yield break;\n        }\n\n        Vector3 spawnPosition = default;\n        Quaternion spawnRotation = Quaternion.identity;\n        float slotDeadline = Time.realtimeSinceStartup + Mathf.Max(1f, SpawnSlotWaitSeconds);\n        while (_spawnSlots == null || !_spawnSlots.TryGetLocalSpawnPose(spawnGo.transform, out spawnPosition, out spawnRotation))\n        {\n            if (Time.realtimeSinceStartup >= slotDeadline)\n            {\n                AppState.I?.Fail("Could not reserve a multiplayer Hub spawn slot.");\n                _snapping = false;\n                yield break;\n            }\n            yield return null;\n        }\n\n        // GorillaPlayer is currently also the XROrigin CameraFloorOffsetObject.'''
s = replace_once(s, needle, repl, "rig wait slot")
s = replace_once(s, "        float targetYaw = spawnGo.transform.rotation.eulerAngles.y;", "        float targetYaw = spawnRotation.eulerAngles.y;", "rig slot yaw")
s = replace_once(s, "        Vector3 cameraShift = spawnGo.transform.position - cam.position;", "        Vector3 cameraShift = spawnPosition - cam.position;", "rig slot position")
s = s.replace("GroundCorrect(spawnGo.transform.position, hubScene, locomotionPlayer)", "GroundCorrect(spawnPosition, hubScene, locomotionPlayer)")
write(p, s)

# PlayerSpawner: do not instantiate the network avatar in Hub until the local rig has reached its unique slot.
p = "Assets/Resources/PhotonVR/Scripts/Player/PlayerSpawner.cs"
s = read(p)
s = replace_once(s,
'''            var manager = PhotonVRManager.Manager;\n            if (manager != null && manager.Head != null)''',
'''            var manager = PhotonVRManager.Manager;\n            if (hub.isLoaded && AppState.I != null && !AppState.I.RigSnapped) return false;\n            if (manager != null && manager.Head != null)''', "spawner wait rig")
write(p, s)

# PhotonVRManager: initialize ten slot-owner room properties without adding them to random matchmaking filters.
p = "Assets/Resources/PhotonVR/Scripts/PhotonVRManager.cs"
s = read(p)
s = replace_once(s, "using RunawayChimps.Zones;\n", "using RunawayChimps.Zones;\nusing RunawayChimps.Multiplayer;\n", "manager using")
s = replace_once(s,
'''            var roomProps = new ExitGames.Client.Photon.Hashtable\n            {\n                { "queue", queue },\n                { "version", Application.version }\n            };\n\n            var roomOptions = new RoomOptions\n            {\n                MaxPlayers = (byte)maxPlayers,\n                IsVisible = true,\n                IsOpen = true,\n                CustomRoomProperties = roomProps,\n                CustomRoomPropertiesForLobby = new[] { "queue", "version" }\n            };''',
'''            var matchmakingProps = new ExitGames.Client.Photon.Hashtable\n            {\n                { "queue", queue },\n                { "version", Application.version }\n            };\n            var creationProps = new ExitGames.Client.Photon.Hashtable\n            {\n                { "queue", queue },\n                { "version", Application.version }\n            };\n            HubSpawnSlotAllocator.AddInitialRoomProperties(creationProps);\n\n            var roomOptions = new RoomOptions\n            {\n                MaxPlayers = (byte)maxPlayers,\n                IsVisible = true,\n                IsOpen = true,\n                CustomRoomProperties = creationProps,\n                CustomRoomPropertiesForLobby = new[] { "queue", "version" }\n            };''', "manager room props")
s = replace_once(s,
'''            bool started = PhotonNetwork.JoinRandomRoom(roomProps, (byte)maxPlayers, MatchmakingMode.RandomMatching, null, null, null);''',
'''            bool started = PhotonNetwork.JoinRandomRoom(matchmakingProps, (byte)maxPlayers, MatchmakingMode.RandomMatching, null, null, null);''', "manager matchmaking props")
s = replace_once(s,
'''                var roomProps = new ExitGames.Client.Photon.Hashtable\n                {\n                    { "queue", PublicQueue },\n                    { "version", Application.version }\n                };\n\n                _lastMatchmakingOptions = new RoomOptions''',
'''                var roomProps = new ExitGames.Client.Photon.Hashtable\n                {\n                    { "queue", PublicQueue },\n                    { "version", Application.version }\n                };\n                HubSpawnSlotAllocator.AddInitialRoomProperties(roomProps);\n\n                _lastMatchmakingOptions = new RoomOptions''', "manager fallback props")
s = replace_once(s,
'''            Manager._state = ConnectionState.JoiningRoom;\n            bool started = PhotonNetwork.JoinOrCreateRoom(\n                roomId,\n                new RoomOptions\n                {\n                    IsVisible = false,\n                    IsOpen = true,\n                    MaxPlayers = (byte)maxPlayers\n                },''',
'''            Manager._state = ConnectionState.JoiningRoom;\n            var privateProps = new ExitGames.Client.Photon.Hashtable();\n            HubSpawnSlotAllocator.AddInitialRoomProperties(privateProps);\n            bool started = PhotonNetwork.JoinOrCreateRoom(\n                roomId,\n                new RoomOptions\n                {\n                    IsVisible = false,\n                    IsOpen = true,\n                    MaxPlayers = (byte)maxPlayers,\n                    CustomRoomProperties = privateProps\n                },''', "manager private props")
write(p, s)

# Quest build guard becomes validation-only; project settings must already be committed.
p = "Assets/Scripts/Editor/LaunchPresentationSettings.cs"
s = read(p)
s = replace_once(s,
'''            if (!LaunchPresentationSettings.Apply(saveAssets: true, logSuccess: false) ||\n                !LaunchPresentationSettings.Validate(logSuccess: false))\n            {''',
'''            if (!LaunchPresentationSettings.Validate(logSuccess: false))\n            {''', "build validation only")
write(p, s)

# Commit Meta OpenXR splash assignment instead of mutating it during build.
p = "Assets/XR/Settings/OpenXR Package Settings.asset"
s = read(p)
marker = "  m_Name: MetaXRFeature Android"
pos = s.find(marker)
if pos < 0: raise RuntimeError("OpenXR MetaXRFeature Android block missing")
end = s.find("--- !u!114", pos + len(marker))
if end < 0: end = len(s)
block = s[pos:end]
old = "systemSplashScreen: {fileID: 0}"
new = "systemSplashScreen: {fileID: 2800000, guid: 73af3b98a31d49a6a0b674b50ed8d20c, type: 3}"
if old not in block and new not in block: raise RuntimeError("Meta Android systemSplashScreen field shape changed")
block = block.replace(old, new, 1)
s = s[:pos] + block + s[end:]
write(p, s)

# Validators: protect architecture, not exact art coordinates.
p = "Tools/validate_launch_presentation.py"
s = read(p)
s = s.replace('        "MonitorCanvasScale = 0.00082f",\n', '')
s = s.replace('        "new Vector3(-0.535f, 0.16f, -0.055f)",\n        "new Vector3(0.535f, -0.02f, -0.055f)",\n', '')
s = replace_once(s,
'''        "Shader.Find(\\\"Unlit/Color\\\")",\n        "Shader.Find(\\\"Standard\\\")",''',
'''        "BaseMaterialResourcePath = \\\"LaunchPresentation/WorkstationBase\\\"",\n        "Resources.Load<Material>",''', "validator material tokens")
s = replace_once(s,
'''    for token in workstation_tokens:\n        if token not in workstation:\n            errors.append(f"Security workstation vignette missing {token!r}.")''',
'''    for token in workstation_tokens:\n        if token not in workstation:\n            errors.append(f"Security workstation vignette missing {token!r}.")\n    scale = re.search(r"MonitorCanvasScale\\s*=\\s*([0-9.]+)f", workstation)\n    if not scale or not (0.0006 <= float(scale.group(1)) <= 0.0011):\n        errors.append("Security workstation monitor scale must remain within a reviewed VR-readable range.")''', "validator scale range")
s = replace_once(s,
'''    oculus = read("Assets/Oculus/OculusProjectConfig.asset")''',
'''    openxr_settings = read("Assets/XR/Settings/OpenXR Package Settings.asset")\n    if f"systemSplashScreen: {{fileID: 2800000, guid: {SPLASH_GUID}, type: 3}}" not in openxr_settings:\n        errors.append("MetaXRFeature Android system splash is not committed to the splash asset.")\n\n    oculus = read("Assets/Oculus/OculusProjectConfig.asset")''', "validator openxr splash")
s = replace_once(s,
'''    flow = read("Assets/Scripts/Bootstrap/LoadingFlow.cs")''',
'''    allocator = read("Assets/Scripts/Bootstrap/HubSpawnSlotAllocator.cs")\n    for token in ["SlotCount = 10", "SetCustomProperties(desired, expected)", "OnPlayerLeftRoom",\n                  "OnMasterClientSwitched", "TryGetLocalSpawnPose"]:\n        if token not in allocator:\n            errors.append(f"Hub spawn allocator missing {token!r}.")\n    rig_snapper = read("Assets/Scripts/Bootstrap/RigSpawnSnapper.cs")\n    for token in ["HubSpawnSlotAllocator", "TryGetLocalSpawnPose", "SpawnSlotWaitSeconds"]:\n        if token not in rig_snapper:\n            errors.append(f"RigSpawnSnapper multiplayer slot integration missing {token!r}.")\n    manager = read("Assets/Resources/PhotonVR/Scripts/PhotonVRManager.cs")\n    if "HubSpawnSlotAllocator.AddInitialRoomProperties" not in manager or "matchmakingProps" not in manager:\n        errors.append("Photon room creation must initialize Hub slot properties without filtering matchmaking on slot occupancy.")\n    player_spawner = read("Assets/Resources/PhotonVR/Scripts/Player/PlayerSpawner.cs")\n    if "!AppState.I.RigSnapped" not in player_spawner:\n        errors.append("Photon avatar spawning must wait for the local Hub rig slot snap.")\n\n    flow = read("Assets/Scripts/Bootstrap/LoadingFlow.cs")''', "validator spawn contracts")
write(p, s)

# Security boot validator also checks legacy fallback and XROrigin-root anchoring.
p = "Tools/validate_security_boot.py"
s = read(p)
s = replace_once(s,
'''                 "SetBackdropOpacity(1f - alpha)", "workstation.gameObject.SetActive(false)"], "presentation")''',
'''                 "SetBackdropOpacity(1f - alpha)", "DestroyWorkstationForReveal()",\n                 "legacyStatus.enabled = true"], "presentation")''', "security validator cleanup")
s = replace_once(s,
'''    require(workstation, ["Security Workstation Vignette", "SceneManager.MoveGameObjectToScene", "RenderMode.WorldSpace",''',
'''    require(workstation, ["Security Workstation Vignette", "GetComponentInParent<XROrigin>", "DontDestroyOnLoad(root)", "RenderMode.WorldSpace",''', "security validator anchor")
write(p, s)

# Update focused docs; maintained docs get the same implemented status through this guarded patch.
for p in ["docs/launch-presentation.md", "docs/launch-workstation-vignette.md", "docs/design-and-lore.md", "docs/repository-improvement-plan.md"]:
    s = read(p)
    heading = "## September 14 implementation — review findings and multiplayer Hub slots"
    if heading not in s:
        s = s.rstrip() + "\n\n" + heading + "\n\n" + (
            "**Implemented on `feature/launch-presentation-polish` / PR #21:** the temporary security workstation is now parented to the persistent `XROrigin` root, so hidden startup rig relocation carries the local vignette while ordinary head/room-scale motion does not. The vignette is fully covered by black and destroyed before Hub reveal; interrupted entry can rebuild it. Legacy Loading status/error text stays available until the physical monitor terminal is successfully constructed.\n\n"
            "Cold-start multiplayer now uses ten Photon room-owned Hub spawn slots. Public/private room creation initializes slot-owner properties; clients claim free slots with room-property compare-and-swap, the Master Client releases/reconciles orphan claims, `RigSpawnSnapper` waits for the local slot before grounding, and the network avatar waits for `RigSnapped`. The slot poses are compact offsets from the existing authored `HubSpawn`; their exact spacing/clearance remains a Unity/headset validation item. Level-return arrival markers are unchanged.\n\n"
            "The workstation now clones a referenced Standard material from `Resources/LaunchPresentation/WorkstationBase` instead of using runtime `Shader.Find`. The Meta Android OpenXR `systemSplashScreen` assignment is committed in project settings, and Android build preprocessing validates rather than mutates that configuration. Source validators protect these architecture contracts without locking exact sticky-note coordinates or one exact monitor scale.\n\n"
            "**Pending validation:** Unity 2022.3.55f1 import/compile; one- and multi-client cold starts; ten-slot spacing/floor clearance; simultaneous claim races and slot reuse after leave/master handoff; failure/retry fallback readability; workstation destruction/rebuild across interrupted reveal; Play Mode visual tuning; Photon avatar placement; and later Quest splash/material/headset behavior."
        ) + "\n"
        write(p, s)

# Remove this one-time helper and workflow from the committed branch.
for rel in ["Tools/_apply_launch_review_fixes.py", ".github/workflows/apply-launch-review-fixes.yml"]:
    path = ROOT / rel
    if path.exists(): path.unlink()
