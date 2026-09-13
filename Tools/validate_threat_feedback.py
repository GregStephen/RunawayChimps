#!/usr/bin/env python3
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
errors = []

def require(path, tokens):
    p = ROOT / path
    if not p.exists(): errors.append(f"{path}: missing"); return ""
    text = p.read_text(encoding="utf-8-sig")
    for token in tokens:
        if token not in text: errors.append(f"{path}: missing {token!r}")
    return text

controller = require("Assets/Scripts/ThreatFeedback/ThreatFeedbackController.cs", [
    "Mathf.Max(desired, source.EvaluateLocalThreat())", "SectorTravelService.I.IsBusy", "Time.unscaledDeltaTime"
])
source = require("Assets/Scripts/ThreatFeedback/MonsterThreatSource.cs", [
    "pursuit.TargetActorNumber != PhotonNetwork.LocalPlayer.ActorNumber", "proximity.HasValidSample", "pursuit.ThreatSector != travel.CurrentSector"
])
nav = require("Assets/Scripts/MonsterScripts/MonsterNavigation.cs", [
    "IMonsterPursuitProvider", "TargetActorNumber", "ApplyRemotePursuit(bool chasing, int targetActorNumber)"
])
sync = require("Assets/Scripts/Travel/SectorMonsterSync.cs", [
    "ProtocolVersion = 2", "navigation.TargetActorNumber", "data.Length != 8", "navigation.ApplyRemotePursuit"
])
reactor = require("Assets/Scripts/ProxManager/ProximityReactor.cs", ["HasValidSample", "LastDistance = float.PositiveInfinity"])
shader = require("Assets/Resources/ThreatFeedback/ThreatVignette.shader", ["UNITY_OUTPUT_STEREO", "smoothstep(0.58,1.02", "0.32"])
if "RaiseEvent" in controller or "RaiseEvent" in source:
    errors.append("local threat presentation must not network UI state")
for e in errors: print("ERROR:", e)
if errors: sys.exit(1)
print("PASS: target-aware local threat feedback source contracts.")
print("PASS: Unity 2022.3.55f1, headset, Quest, and two-client Photon validation remain separate and pending.")
