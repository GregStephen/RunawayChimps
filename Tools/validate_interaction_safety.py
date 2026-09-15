#!/usr/bin/env python3
"""Interaction source contracts and geometry-math fixtures, NOT Unity/PhysX tests.

Run alongside validate_keycard_reader_contracts.py. Negative fixtures ensure the
checks reject representative regressions rather than merely printing success.
Unity 2022.3.55f1, XRI, Photon and headset acceptance remain separate checks.
"""
from pathlib import Path
import math
import re

ROOT = Path(__file__).resolve().parents[1]
PATHS = {
    "player": "Assets/Scripts/NewGorillaLocomotionScripts/Player.cs",
    "held": "Assets/Scripts/HeldItemCollisionMode.cs",
    "card": "Assets/Scripts/KeyCard.cs",
    "reader": "Assets/Scripts/KeycardReaderLightController.cs",
    "box": "Assets/Scripts/KeyBox.cs",
    "panel": "Assets/Scripts/KeycardLockProgressIndicator.cs",
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def method(source: str, name: str) -> str:
    # Extract a method body, not an unrelated same-named call/comment.
    text = re.sub(r'"(?:\\.|[^"\\])*"', '""', source)
    text = re.sub(r"//[^\n]*|/\*.*?\*/", "", text, flags=re.S)
    pattern = rf"\b(?:public|private|internal)?\s*(?:static\s+)?(?:void|bool|IEnumerator)\s+{name}\s*\([^)]*\)\s*\{{"
    match = re.search(pattern, text)
    require(match is not None, f"Missing method {name}")
    start = match.end() - 1
    depth = 0
    for end in range(start, len(text)):
        depth += (text[end] == "{") - (text[end] == "}")
        if depth == 0:
            return text[start:end + 1]
    raise AssertionError(f"Unbalanced method {name}")


def validate(sources: dict[str, str]) -> None:
    player, held, card, reader, box, panel = (sources[k] for k in PATHS)
    calls = [line for line in player.splitlines()
             if re.search(r"\bPhysics\.(?:SphereCast|Raycast)\(", line)]
    require(len(calls) >= 7, "Missing Gorilla movement/clearance queries")
    require(all("QueryTriggerInteraction.Ignore" in call for call in calls),
            "Every Gorilla movement, anti-clip and unsticking query must ignore triggers")
    cast = method(player, "CollisionsSphereCast")
    require("Mathf.Max(0, innerHit.distance" in cast and
            "Mathf.Max(0, hitInfo.distance" not in cast,
            "Inner collision correction must use the inner hit distance")

    fit = method(card, "FitColliderToVisualBounds")
    require("renderer.localBounds" in fit and "renderer.bounds" not in fit and
            "renderer.transform.TransformPoint" in fit and
            "transform.InverseTransformPoint(worldCorner)" in fit,
            "Fit renderer-local corners directly into card space; never round-trip a world AABB")

    release = method(held, "OnRelease")
    restore = method(held, "RestoreRigCollisionsWhenSeparated")
    require("RestoreLayers();" not in release,
            "Release must not restore a locomotion-blocking layer before separation")
    require("HasLocalRigOverlap()" in restore and
            "RestoreLocalRigCollisions();" in restore and "RestoreLayers();" in restore,
            "Deferred release must restore both owned pairs and layers")
    apply = method(held, "ApplyHeldLayer")
    require("triggerOnly" in apply and "continue;" in apply,
            "Trigger-only grab sensors must retain their authored layers")
    ignore = method(held, "IgnoreLocalRigCollisions")
    require("itemCollider.isTrigger" in ignore and "rigCollider.isTrigger" in ignore and
            "Physics.GetIgnoreCollision(itemCollider, rigCollider)" in ignore,
            "Suppress solids only and preserve pre-existing pair ignores")
    require("localRigContactPairs.Clear();" in ignore and
            "localRigContactPairs.Add(" in ignore and
            "ignoredLocalRigPairs.Count != 0" not in ignore,
            "Re-grab must refresh separation contacts, including already ignored pairs")
    require("localRigContactPairs" in method(held, "HasLocalRigOverlap"),
            "Separation checks must not depend only on collision ignores owned by this component")

    live = method(reader, "HasLiveOverlap")
    require(all(token in live for token in (
        "ScannerActive", "other.isTrigger", "other.enabled",
        "activeInHierarchy", "Physics.ComputePenetration(",
        "Physics.GetIgnoreLayerCollision(", "Physics.GetIgnoreCollision(",
        "other.transform.position", "scanVolume.transform.position")),
        "Scanner overlap must be active, solid-card-only, filter-aware and geometrically current")
    require("HasLiveOverlap(collider)" in method(reader, "HasMatchingOverlap"),
            "Acceptance retry must verify actual overlap, not a cached enter event")
    require("!HasLiveOverlap(collider)" in method(reader, "RemoveInvalidAcceptedColliders"),
            "LED presence must discard stale geometry")
    late = method(reader, "LateUpdate")
    require(late.index("RemoveInvalidAcceptedColliders();") < late.index("RetryPendingSubmissions();"),
            "Prune stale reader overlaps before retrying acceptance")
    require("HasLiveOverlap(other)" in method(reader, "OnTriggerEnter") and
            "!isActiveAndEnabled" in method(reader, "OnTriggerExit") and
            "OnTriggerEnter(other);" in method(reader, "OnTriggerStay"),
            "Disabled readers must fail closed; stay callbacks must recover valid overlap")
    submit = method(reader, "TrySubmitPendingCard")
    require("!ScannerActive" in submit and "!gameplayCard.isActiveAndEnabled" in submit,
            "Submission must reject disabled readers and cards")

    insert = method(card, "TryInsertInto")
    require(all(token in insert for token in (
        "!isActiveAndEnabled", "insertionInProgress", "try", "finally",
        "insertionInProgress = true;", "insertionInProgress = false;")),
        "Card consumption must reject disabled and reentrant submissions")
    add = method(box, "TryAddKey")
    require("!isActiveAndEnabled" in add and "!card.isActiveAndEnabled" in add and
            "acceptingKey" in add, "Inactive or reentrant objectives must not advance")
    notify = method(box, "NotifyProgressChanged")
    require("GetInvocationList()" in notify and "callback(acceptedKeys, requiredKeys);" in notify and
            "catch (Exception" in notify,
            "One broken progress listener must not interrupt card acceptance or later listeners")
    retry = method(box, "UpdateRetryInteraction")
    require("IsComplete" in retry and "isActiveAndEnabled" in retry and
            "!SectorTravelService.I.IsBusy" in retry and "retryInteraction.enabled = available;" in retry,
            "The hidden completion retry target must not compete with unfinished card pickup")
    require("!isActiveAndEnabled" in method(panel, "Subscribe"),
            "A disabled progress panel must not subscribe through Bind")
    require(not any("Physics.queriesHitTriggers =" in source for source in sources.values()),
            "Do not change the global trigger-query setting")


def check_negative_fixtures(sources: dict[str, str]) -> int:
    mutations = (
        ("player", "QueryTriggerInteraction.Ignore", "QueryTriggerInteraction.UseGlobal"),
        ("player", "Mathf.Max(0, innerHit.distance", "Mathf.Max(0, hitInfo.distance"),
        ("card", "renderer.localBounds", "renderer.bounds"),
        ("held", "localRigContactPairs.Add(", "ignoredLocalRigPairs.Add("),
        ("reader", "Physics.ComputePenetration(", "Physics.ComputePenetrationRemoved("),
        ("reader", "HasLiveOverlap(collider)", "collider.enabled"),
        ("box", "callback(acceptedKeys, requiredKeys);", "callback(0, 0);"),
        ("panel", "if (!isActiveAndEnabled || subscribed", "if (subscribed"),
    )
    for key, before, after in mutations:
        require(before in sources[key], f"Negative fixture anchor missing: {before}")
        mutated = dict(sources)
        mutated[key] = mutated[key].replace(before, after, 1)
        try:
            validate(mutated)
        except AssertionError:
            continue
        raise AssertionError(f"Regression checker accepted mutation: {before}")
    return len(mutations)


def check_rotated_bounds_math() -> int:
    # Thin-card X/Z cross section, root rotation and 1x/2x scale. This checks
    # coordinate-space math only, not Unity render bounds or PhysX contacts.
    count = 0
    for scale in (1.0, 2.0):
        for degrees in (0, 15, 30, 45, 67, 90, 137):
            c, s = math.cos(math.radians(degrees)), math.sin(math.radians(degrees))
            corners = [(x, z) for x in (-0.05, 0.05) for z in (-0.003, 0.003)]
            world = [(scale * (c*x - s*z), scale * (s*x + c*z)) for x, z in corners]
            local = [((c*x + s*z)/scale, (-s*x + c*z)/scale) for x, z in world]
            extent = lambda points, i: max(p[i] for p in points) - min(p[i] for p in points)
            require(math.isclose(extent(local, 0), 0.1, abs_tol=1e-9) and
                    math.isclose(extent(local, 1), 0.006, abs_tol=1e-9),
                    "Direct local-corner fit changed dimensions under root rotation")
            if degrees == 45:
                aabb = [(x, z) for x in (min(p[0] for p in world), max(p[0] for p in world))
                        for z in (min(p[1] for p in world), max(p[1] for p in world))]
                old = [((c*x+s*z)/scale, (-s*x+c*z)/scale) for x, z in aabb]
                require(extent(old, 1) > 0.1, "Fixture no longer exposes old world-AABB thickness inflation")
            count += 1
    return count


def main() -> None:
    sources = {key: (ROOT / path).read_text(encoding="utf-8-sig") for key, path in PATHS.items()}
    validate(sources)
    negatives = check_negative_fixtures(sources)
    rotations = check_rotated_bounds_math()
    print(f"PASS: interaction source contracts; {negatives} rejected regression mutations; {rotations} rotated/scaled math fixtures.")
    print("SOURCE/MATH ONLY: Unity compilation, XRI/PhysX, Photon and headset tests are still required.")


if __name__ == "__main__":
    main()
