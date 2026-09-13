#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
errors = []


def require(path, tokens):
    target = ROOT / path
    if not target.exists():
        errors.append(f"{path}: missing")
        return ""
    text = target.read_text(encoding="utf-8-sig")
    for token in tokens:
        if token not in text:
            errors.append(f"{path}: missing contract token {token!r}")
    return text


contract = require("Assets/Scripts/MonsterScripts/MonsterPursuitState.cs", [
    "public readonly struct MonsterPursuitState",
    "IsPursuing = isPursuing && targetActorNumber > 0",
    "public interface IMonsterPursuitProvider",
    "public interface IMonsterPursuitSyncTarget",
    "event Action<MonsterPursuitState> PursuitChanged",
])
controller = require("Assets/Scripts/ThreatFeedback/ThreatFeedbackController.cs", [
    "strongest = Mathf.Max(strongest, source.EvaluateLocalThreat())",
    "SectorTravelService.I.IsBusy",
    "Travel owns the complete XR view",
    "vignette.SetThreat(0f)",
    "Time.unscaledDeltaTime",
    "public event Action<float> ThreatChanged",
    "ResetPresentation()",
])
source = require("Assets/Scripts/ThreatFeedback/MonsterThreatSource.cs", [
    "state.TargetActorNumber != PhotonNetwork.LocalPlayer.ActorNumber",
    "proximity.IsSampleFresh(MaximumSampleAgeSeconds)",
    "pursuit.ThreatSector != travel.CurrentSector",
    "requiredLocalZone != ZoneId.None",
    "IMonsterPursuitProvider",
])
profile = require("Assets/Scripts/ThreatFeedback/ThreatProfile.cs", [
    "pursuitBaseline",
    "maximumThreat",
    "AnimationCurve response",
    "Mathf.Lerp(baseline, 1f, curved)",
])
installer = require("Assets/Scripts/ThreatFeedback/CrawlerThreatSourceInstaller.cs", [
    'LevelOneScene = "Level1_Containment"',
    "CrawlerMonsterId = 1",
    "sync.monsterId != CrawlerMonsterId",
    "GetComponentsInChildren<MonsterNavigation>(true)",
    "navigation.gameObject.AddComponent<MonsterThreatSource>()",
    "ZoneId.Level1_Vents",
    "beginDistance = startDistance",
])
nav = require("Assets/Scripts/MonsterScripts/MonsterNavigation.cs", [
    "IMonsterPursuitSyncTarget",
    "public MonsterPursuitState PursuitState",
    "public event Action<MonsterPursuitState> PursuitChanged",
    "TargetActorNumber => PursuitState.TargetActorNumber",
    "ApplyRemotePursuit(bool chasing, int targetActorNumber)",
])
sync = require("Assets/Scripts/Travel/SectorMonsterSync.cs", [
    "ProtocolVersion = 4",
    "IMonsterPursuitSyncTarget",
    "pursuit.PursuitChanged += HandlePursuitChanged",
    "pursuitStateTimeout",
    "retiredAuthorityEpochFloor",
    "acceptedAuthorityEpoch",
    "outgoingStateRevision",
    "lastReceivedStateRevision = -1",
    "int revision = ++outgoingStateRevision",
    "data.Length != 10",
    "incomingAuthorityEpoch <= retiredFloor",
    "revision <= lastReceivedStateRevision",
    "A -> B -> A handoff",
    "ApplyControllerElection(owner)",
    "pursuit.ApplyRemotePursuit",
    "A newly elected controller must make a fresh local target decision",
])
reactor = require("Assets/Scripts/ProxManager/ProximityReactor.cs", [
    "HasValidSample",
    "LastDistance = float.PositiveInfinity",
    "LastSampleUnscaledTime",
    "IsSampleFresh(float maximumAgeSeconds)",
])
view = require("Assets/Scripts/ThreatFeedback/ThreatVignetteView.cs", [
    "RenderMode.ScreenSpaceCamera",
    "canvas.worldCamera = camera",
    "Resources.Load<Shader>(\"ThreatFeedback/ThreatVignette\")",
    "graphic.raycastTarget = false",
])
shader = require("Assets/Resources/ThreatFeedback/ThreatVignette.shader", [
    "UNITY_VERTEX_OUTPUT_STEREO",
    "UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX",
    "smoothstep(0.58,1.02",
    "0.32",
])
workflow = require(".github/workflows/source-validation.yml", [
    "python Tools/validate_threat_feedback.py",
])

for local_only in (controller, source, view):
    if "RaiseEvent" in local_only or "PhotonView" in local_only:
        errors.append("local presentation must not network presentation state")
if "AddComponent<MonsterThreatSource>" in nav:
    errors.append("MonsterNavigation must not install the Level 1 presentation adapter")
if "RequireComponent(typeof(MonsterNavigation))" in sync or "private MonsterNavigation" in sync:
    errors.append("SectorMonsterSync must use the generic pursuit contract")
if "FindObjectsOfType" in controller or "FindObjectsOfType" in source:
    errors.append("hot paths must not perform scene-wide object searches")
if "MeshRenderer" in view or "CreatePrimitive" in view:
    errors.append("the local vignette should remain camera-specific UI")
if (ROOT / "Assets/Scripts/ProxManager/ScreenVignette.cs").exists():
    errors.append("obsolete ScreenVignette.cs should be removed")

for error in errors:
    print("ERROR:", error)
if errors:
    print(f"FAILED: {len(errors)} threat-feedback contract issue(s).")
    sys.exit(1)

print("PASS: target-aware local threat feedback source contracts.")
print("PASS: authority epochs plus revisions reject same-frame and repeated-controller stale pursuit state.")
print("PASS: Level 1 runtime adapter is scoped to the authored Crawler monster ID.")
print("PASS: travel immediately suppresses personal threat presentation beneath the black fade.")
print("PASS: Unity 2022.3.55f1, headset, Quest, and two-client Photon validation remain pending.")
