#!/usr/bin/env python3
"""Structural boundaries for the reuse review. Not a Unity execution test.

Real engine-independent state tests are in CardSystemHarness. Unity Play Mode
fixtures are in Assets/Tests/CardSystem and must be run in Unity separately.
"""
import json
from pathlib import Path


def validate_reuse(root: Path) -> None:
    def text(path):
        return (root / path).read_text(encoding="utf-8-sig")
    def require(condition, message):
        if not condition:
            raise AssertionError(message)
    card = text("Assets/Scripts/KeyCard.cs")
    box = text("Assets/Scripts/KeyBox.cs")
    reader = text("Assets/Scripts/KeycardReaderLightController.cs")
    panel = text("Assets/Scripts/KeycardLockProgressIndicator.cs")
    held = text("Assets/Scripts/HeldItemCollisionMode.cs")
    respawn = text("Assets/Scripts/Utils/RespawnToOriginalSpawn.cs")
    require("CardConsumptionState consumption" in card and "consumption.Commit(box)" in card,
            "Use the tested single-use consumption state")
    require("return card != null && card.TryInsertInto(this);" in box,
            "Public TryAddKey must not be an independent count-only entrance")
    require(box.index("card.CommitInsertion(this)") < box.index("        AcceptKey();"),
            "Commit consumption before observers can re-enter")
    for token in ("RequiresMatchingReader", "card.IsInsertionPendingFor(this)",
                  "reader.CanSubmitCard(card, this)", "card.WasHeldByLocalPlayer", "RegisterReader("):
        require(token in box, "Missing reusable objective boundary: " + token)
    require("if (!box.RequiresMatchingReader)" in card,
            "Direct contact must not bypass a reader in other levels")
    for token in ("KeyBox Objective => keyBox", "objectiveResolutionAttempted = true",
                  "other != card.PhysicalCollider", "keyBox != objective",
                  "TryResolveMatchingCard(card.PhysicalCollider", "TryInsertFromReader(keyBox, this)"):
        require(token in reader, "Missing reader authorization boundary: " + token)
    require("[RequireComponent(typeof(HeldItemCollisionMode))]" in card,
            "New cards must include held collision safety")
    require("manager.UnregisterInteractable((IXRInteractable)grab)" in card and
            "manager.RegisterInteractable((IXRInteractable)grab)" in card,
            "Runtime-added cards must refresh XRI collider registration")
    require("MovementType.VelocityTracking" in card, "Held cards must use physical movement")
    require("HandStillNear(item, player.leftHandFollower" in held and
            "HandStillNear(item, player.rightHandFollower" in held,
            "Release must clear both virtual hand query volumes")
    require("restoreGrabPending" in respawn and "void OnEnable()" in respawn and
            "void OnDisable()" in respawn, "Recovery must survive disable/re-enable")
    require("subscribedSource.ProgressChanged -= HandleProgressChanged" in panel and
            "explicitBinding" in panel, "Panel must unbind its actual subscription source")
    require("if (requiredKeys > progressLights.Length)\n            acceptedKeys = 0;" in panel,
            "Insufficient lamp capacity must not imply completion")
    require("submittedRevision != bindingRevision" in reader and "OverlapBoxNonAlloc(" in reader,
            "Reader rebinding and sleeping-card reactivation must retain recovery guards")
    require("rb.WakeUp();" in respawn and "card.IsInserted" in respawn,
            "Recovery must settle loose cards and never reactivate consumed cards")
    require("restoreSafetyOnEnable" in held, "Reactivation must preserve separation safety")
    editor = text("Assets/Scripts/Editor/CardSystemValidator.cs")
    require("Validate Loaded Scenes" in editor and "Validate Selected Prefab or Hierarchy" in editor and
            "SaveScene(" not in editor and "SaveAssets(" not in editor and "ApplyModifiedProperties(" not in editor,
            "Reusable authoring validation must remain read-only")
    manifest = json.loads(text("Packages/manifest.json"))["dependencies"]
    lock = json.loads(text("Packages/packages-lock.json"))["dependencies"]
    require(manifest["com.unity.xr.interaction.toolkit"] == lock["com.unity.xr.interaction.toolkit"]["version"] == "2.6.4",
            "Revalidate API assumptions when XRI changes from 2.6.4")
    fixtures = text("Assets/Tests/CardSystem/CardSystemPlayModeTests.cs")
    require(fixtures.count("[TestCase(") >= 16 and "[UnityTest]" in fixtures,
            "Retain Unity credential-matrix and lifecycle fixtures")
    print("PASS: reusable-card authorization, transaction, binding, recovery and physics source boundaries.")


if __name__ == "__main__":
    validate_reuse(Path(__file__).resolve().parents[1])
