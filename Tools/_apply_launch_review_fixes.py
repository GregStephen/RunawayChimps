#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def patch(path, old, new, label):
    p = ROOT / path
    text = p.read_text(encoding="utf-8-sig")
    if text.count(old) != 1:
        raise RuntimeError(f"{label}: expected one match, found {text.count(old)}")
    p.write_text(text.replace(old, new, 1), encoding="utf-8")

patch(
    "Assets/Scripts/Bootstrap/RigSpawnSnapper.cs",
    "using System.Collections;\n",
    "using System.Collections;\nusing Photon.Pun;\n",
    "RigSpawnSnapper Photon import")

patch(
    "Assets/Scripts/Bootstrap/RigSpawnSnapper.cs",
    '''        Vector3 spawnPosition = default;\n        Quaternion spawnRotation = Quaternion.identity;\n        float slotDeadline = Time.realtimeSinceStartup + Mathf.Max(1f, SpawnSlotWaitSeconds);\n        while (_spawnSlots == null || !_spawnSlots.TryGetLocalSpawnPose(spawnGo.transform, out spawnPosition, out spawnRotation))\n        {''',
    '''        Vector3 spawnPosition = default;\n        Quaternion spawnRotation = Quaternion.identity;\n        // Hub can load before authentication/Photon. The global startup timeout owns that wait;\n        // the shorter slot timeout starts only after this client is actually in the room.\n        while (!PhotonNetwork.InRoom)\n        {\n            if (AppState.I != null && !string.IsNullOrEmpty(AppState.I.LastError))\n            {\n                _snapping = false;\n                yield break;\n            }\n            yield return null;\n        }\n        float slotDeadline = Time.realtimeSinceStartup + Mathf.Max(1f, SpawnSlotWaitSeconds);\n        while (_spawnSlots == null || !_spawnSlots.TryGetLocalSpawnPose(spawnGo.transform, out spawnPosition, out spawnRotation))\n        {''',
    "RigSpawnSnapper room-first slot timeout")

patch(
    "Tools/validate_security_boot.py",
    '''                          "Destroy(collider)", "Shader.Find(\\\"Unlit/Color\\\")", "Shader.Find(\\\"Standard\\\")",\n                          "CAM 04\\\\nSTILL DEAD", "VENT B\\\\nAGAIN?", "IF THEY GET OUT\\\\nI QUIT."],''',
    '''                          "Destroy(collider)", "BaseMaterialResourcePath = \\\"LaunchPresentation/WorkstationBase\\\"",\n                          "Resources.Load<Material>", "CAM 04\\\\nSTILL DEAD", "VENT B\\\\nAGAIN?",\n                          "IF THEY GET OUT\\\\nI QUIT."],''',
    "security validator material contract")

p = ROOT / "Tools/validate_launch_presentation.py"
text = p.read_text(encoding="utf-8-sig")
needle = '''    setup = read("Assets/Scripts/Editor/LaunchPresentationSettings.cs")\n    for token in ['''
replacement = '''    setup = read("Assets/Scripts/Editor/LaunchPresentationSettings.cs")\n    preprocess = setup.split("internal sealed class LaunchPresentationBuildPreprocessor", 1)[-1]\n    if "Apply(saveAssets:" in preprocess:\n        errors.append("Android build preprocessing must validate committed launch settings, not mutate/save project assets.")\n    for token in ['''
if text.count(needle) != 1:
    raise RuntimeError("launch validator build-preprocessor insertion point changed")
p.write_text(text.replace(needle, replacement, 1), encoding="utf-8")

Path(__file__).unlink()
