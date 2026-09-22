"""Source-contract checks only: no Unity compilation or headset execution."""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]


def main() -> int:
    errors: list[str] = []
    flow = (ROOT / "Assets/Scripts/Bootstrap/LoadingFlow.cs").read_text(encoding="utf-8")
    ui = (ROOT / "Assets/Scripts/Loading/SecurityBootPresentation.cs").read_text(encoding="utf-8")
    panel = (ROOT / "Assets/Scripts/Loading/SecurityWorkstationVignette.cs").read_text(encoding="utf-8")
    travel = (ROOT / "Assets/Scripts/Travel/SectorTravelService.cs").read_text(encoding="utf-8")
    menu = (ROOT / "Assets/Scripts/Editor/SecurityBootReviewMenu.cs").read_text(encoding="utf-8")
    tags = (ROOT / "ProjectSettings/TagManager.asset").read_text(encoding="utf-8-sig")

    def require(text: str, tokens: list[str], name: str) -> None:
        for token in tokens:
            if token not in text:
                errors.append(f"{name}: missing {token!r}")

    gate = flow.split("private bool CanEnterHub()", 1)[-1].split("private static bool IsEditorReviewHeld", 1)[0]
    require(gate, ["state.IsReady", "state.HubActive", "state.RigSnapped", "state.PhotonPlayerSpawned",
                   "state.PlayerVisualsReady", "PhotonNetwork.InRoom", "isLoaded",
                   "string.IsNullOrEmpty(state.LastError)", "string.IsNullOrEmpty(loadError)"], "readiness")
    require(flow, ["SectorTravelService.I.IsBusy", "if (!startup) return;", "SecurityBootPresentation.Install",
                   "hubLoad != null && !hubLoad.isDone", "LoadSceneMode.Additive", "GameBootstrap.I.RetryStartup()",
                   "Keyboard.current.rKey.wasPressedThisFrame", "XRNode.LeftHand", "XRNode.RightHand",
                   "if (!ready && Time.realtimeSinceStartup >= deadline", "if (!startup || entering) return;",
                   "PrepareCameraForHubReveal()", "RestoreCameraForReveal()", "RestoreAfterInterruptedEntry()",
                   "minimumIntroSeconds = 1.5f", "Mathf.Clamp(minimumIntroSeconds, 0f, 3f)",
                   "#if UNITY_EDITOR", "#else\n        return false;"], "flow")
    if flow.count("if (!CanEnterHub()) { AbortEntry(); yield break; }") < 3:
        errors.append("Recheck readiness throughout both fades and before activation.")
    if flow.find("PrepareCameraForHubReveal()") > flow.find("SetBackdropOpacity(0f)"):
        errors.append("Hub reveal must combine the Hub mask with the black presentation layer before fading black away.")

    require(ui, ["SecurityWorkstationVignette.Create", "MonitorCanvasRoot",
                 "PresentationLayerName = \"LoadingPresentation\"", "LayerMask.NameToLayer(PresentationLayerName)",
                 "SetLayerRecursively(hostCanvas.gameObject, presentationLayer)",
                 "boundCamera.cullingMask = 1 << presentationLayer",
                 "boundCamera.cullingMask = savedCullingMask | (1 << presentationLayer)",
                 "stages[0] = inRoom", "stages[1] = hubLoaded", "state.RigSnapped",
                 "state.PhotonPlayerSpawned", "state.PlayerVisualsReady", "label.enabled = false", "debug.enabled = false",
                 "label.richText = false", "RETRY: EITHER TRIGGER", "DESKTOP: R", "Destroy(tick)",
                 "boundCamera.cullingMask = savedCullingMask", "boundCamera.clearFlags = savedClearFlags",
                 "boundCamera.backgroundColor = savedBackground", "MaskStartupCamera()",
                 "SetBackdropOpacity(1f - alpha)", "DestroyWorkstationForReveal()",
                 "workstationRetiredForReveal", "workstationRetiredForReveal = true",
                 "workstationRetiredForReveal = false", "legacyStatus.enabled = true"], "presentation")

    if "- LoadingPresentation" not in tags:
        errors.append("ProjectSettings/TagManager.asset must reserve the LoadingPresentation layer.")

    configure = ui.split("private void Configure", 1)[-1].split("private void EnsureWorkstation", 1)[0]
    travel_guard = configure.find("if (!startupMode)")
    travel_return = configure.find("return;", travel_guard) if travel_guard >= 0 else -1
    startup_bind = configure.find("BindStartupCamera();")
    if travel_guard < 0 or travel_return < 0 or startup_bind < 0 or not (travel_guard < travel_return < startup_bind):
        errors.append("Travel must return before startup camera/panel/audio construction.")

    require(panel, ["Security Boot Panel Vignette", "GetComponentInParent<XROrigin>", "DontDestroyOnLoad(root)",
                    "RenderMode.WorldSpace", "Security Boot World Canvas", "TerminalDistance = 2.50f",
                    "rect.localRotation = Quaternion.identity;",
                    "camera.transform.position + forward * TerminalDistance",
                    "root.transform.SetParent(origin.transform, true)"],
            "startup security panel")

    for forbidden in ["GameObject.CreatePrimitive", "BaseMaterialResourcePath", "Desk top", "Night shift mug",
                      "CAM 04\\nSTILL DEAD", "VENT B\\nAGAIN?", "IF THEY GET OUT\\nI QUIT."]:
        if forbidden in panel:
            errors.append(f"Superseded workstation presentation returned: {forbidden!r}")

    distance = re.search(r"TerminalDistance\s*=\s*([0-9.]+)f", panel)
    if not distance or not (2.3 <= float(distance.group(1)) <= 2.7):
        errors.append("Restored green terminal must remain in the corrected comfortable mid-distance range.")
    if "Quaternion.Euler(0f, 180f, 0f)" in panel:
        errors.append("Restored green terminal must not render from the mirrored back face.")

    require(travel, ["origin.Camera.backgroundColor = Color.black", "debug.debugText.text = \"\"",
                     "ShowLoadingScene", "RestoreCamera()"], "existing black travel and recovery")
    require(menu, ["Hold Security Boot For Review", "SessionState.SetBool", "Menu.SetChecked"], "editor review")

    for text, name in ((ui, "SecurityBootPresentation"), (panel, "SecurityWorkstationVignette")):
        for forbidden in ("PhotonNetwork.Join", "PhotonNetwork.Instantiate", "MarkRigSnapped(", "TryMarkReady(",
                          "SceneManager.LoadScene", "new RenderTexture", "allowSceneActivation", "Random.Range"):
            if forbidden in text:
                errors.append(f"{name} must remain presentation-only: {forbidden}")

    for text, name in ((flow, "LoadingFlow"), (ui, "SecurityBootPresentation"),
                       (panel, "SecurityWorkstationVignette")):
        if re.search(r"(?:origin|boundCamera|camera|startupCamera)\.transform\.(?:position|rotation|localPosition|localRotation)\s*=", text):
            errors.append(f"{name} must not move the tracked camera.")

    overscan = (ROOT / "Assets/Scripts/Travel/LoadingCanvasOverscan.cs").read_text(encoding="utf-8")
    require(overscan, ["Overscan = 0.12f", "image.color = Color.black"], "existing XR coverage")
    doc = (ROOT / "docs/security-boot-prototype.md").read_text(encoding="utf-8")
    require(doc, ["historical implementation record", "merged through PR #20", "black-only", "technical validation"],
            "historical security-boot record")

    if errors:
        print("FAIL: security boot source contracts")
        for error in errors:
            print(" -", error)
        return 1
    print("PASS: security boot source contracts (readiness, retry, isolated front-facing green terminal, black travel, editor hold, safe Hub reveal).")
    print("Unity compilation, runtime, Photon and headset validation remain separate.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
