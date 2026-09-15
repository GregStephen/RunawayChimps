#!/usr/bin/env python3
"""One-time reviewed patch; removed by its branch-scoped apply workflow."""
from pathlib import Path
import subprocess

BASE = "08d44b59d5b789de73b737f579a3a2f4ac4657d3"
PLAYER = "Assets/Scripts/NewGorillaLocomotionScripts/Player.cs"
HELD = "Assets/Scripts/HeldItemCollisionMode.cs"
CARD = "Assets/Scripts/KeyCard.cs"
READER = "Assets/Scripts/KeycardReaderLightController.cs"
BOX = "Assets/Scripts/KeyBox.cs"
PANEL = "Assets/Scripts/KeycardLockProgressIndicator.cs"
CONTRACTS = "Tools/validate_keycard_reader_contracts.py"
WORKFLOW = ".github/workflows/source-validation.yml"
DOCS = ("docs/design-and-lore.md", "docs/repository-improvement-plan.md")
TARGETS = (PLAYER, HELD, CARD, READER, BOX, PANEL, CONTRACTS, WORKFLOW, *DOCS)


def replace(text: str, old: str, new: str, count: int = 1) -> str:
    if text.count(old) != count:
        raise RuntimeError(f"Expected {count} exact patch anchors, found {text.count(old)}: {old[:120]!r}")
    return text.replace(old, new)


def replace_method(text: str, signature: str, replacement: str) -> str:
    if text.count(signature) != 1:
        raise RuntimeError(f"Method anchor changed: {signature}")
    start = text.index(signature)
    opening = text.index("{", start)
    depth = 0
    for end in range(opening, len(text)):
        depth += (text[end] == "{") - (text[end] == "}")
        if depth == 0:
            return text[:start] + replacement.rstrip() + text[end + 1:]
    raise RuntimeError(f"Unbalanced source method: {signature}")


def main() -> None:
    # Fail instead of overwriting concurrent edits to any reviewed source/doc.
    subprocess.run(["git", "diff", "--exit-code", BASE, "HEAD", "--", *TARGETS], check=True)
    sources = {path: Path(path).read_text(encoding="utf-8-sig") for path in TARGETS}

    text = sources[PLAYER]
    text = replace(text, "Mathf.Max(0, hitInfo.distance - sphereRadius * (1f - precision * precision))",
                   "Mathf.Max(0, innerHit.distance - sphereRadius * (1f - precision * precision))")
    sources[PLAYER] = text

    text = sources[CARD]
    text = replace(text, "    private bool isInserted = false;",
                   "    private bool isInserted = false;\n    private bool insertionInProgress;")
    text = replace(text, "            Bounds worldBounds = renderer.bounds;\n            Vector3 center = worldBounds.center;\n            Vector3 extents = worldBounds.extents;",
                   "            // Do not inverse-transform a world AABB: rotating a thin card\n            // would inflate its collider thickness. Transform renderer-local corners directly.\n            Bounds rendererBounds = renderer.localBounds;\n            Vector3 center = rendererBounds.center;\n            Vector3 extents = rendererBounds.extents;")
    text = replace(text, "                        Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));",
                   "                        Vector3 worldCorner = renderer.transform.TransformPoint(\n                            center + Vector3.Scale(extents, new Vector3(x, y, z)));")
    text = replace_method(text, "    public bool TryInsertInto(KeyBox box)", '''    public bool TryInsertInto(KeyBox box)
    {
        if (!isActiveAndEnabled || isInserted || insertionInProgress || box == null)
            return false;

        // Progress listeners run synchronously. They must not submit this same card
        // to another reader/lock before the first acceptance has finished consuming it.
        insertionInProgress = true;
        try
        {
            if (!box.TryAddKey(this))
                return false;

            isInserted = true;
            Destroy(gameObject);
            return true;
        }
        finally
        {
            insertionInProgress = false;
        }
    }''')
    sources[CARD] = text

    text = sources[HELD]
    text = replace(text, "    readonly List<IgnoredCollisionPair> ignoredLocalRigPairs = new List<IgnoredCollisionPair>();",
                   "    readonly List<IgnoredCollisionPair> ignoredLocalRigPairs = new List<IgnoredCollisionPair>();\n    readonly List<IgnoredCollisionPair> localRigContactPairs = new List<IgnoredCollisionPair>();")
    text = replace(text, "        if (ignoredLocalRigPairs.Count != 0)\n            return;",
                   "        // Rebuild separation contacts on every grab, including an immediate\n        // re-grab while old owned ignores are still active. Never undo pre-existing ignores.\n        localRigContactPairs.Clear();")
    text = replace(text, "                // Only restore pairs this component actually changed.",
                   "                localRigContactPairs.Add(new IgnoredCollisionPair(itemCollider, rigCollider));\n\n                // Only restore pairs this component actually changed.")
    text = replace(text, "        ignoredLocalRigPairs.Clear();", "        ignoredLocalRigPairs.Clear();\n        localRigContactPairs.Clear();")
    text = replace(text, "    void OnRelease(SelectExitEventArgs args)\n    {\n        RestoreLayers();\n",
                   "    void OnRelease(SelectExitEventArgs args)\n    {\n        // Pair ignores do not affect Gorilla's casts. Keep solid colliders on\n        // HeldItem until separation as well; trigger-only grab sensors never moved.\n")
    text = replace(text, "        if (grab == null || !grab.isSelected)\n            RestoreLocalRigCollisions();",
                   "        if (grab == null || !grab.isSelected)\n        {\n            RestoreLocalRigCollisions();\n            RestoreLayers();\n        }")
    text = replace(text, "    bool HasLocalRigOverlap()\n    {\n        foreach (IgnoredCollisionPair pair in ignoredLocalRigPairs)",
                   "    bool HasLocalRigOverlap()\n    {\n        foreach (IgnoredCollisionPair pair in localRigContactPairs)")
    sources[HELD] = text

    text = sources[READER]
    text = replace(text, "    private KeycardCredential resolvedCredential;",
                   "    private BoxCollider scanVolume;\n    private bool ScannerActive => isActiveAndEnabled && scanVolume != null &&\n        scanVolume.enabled && scanVolume.isTrigger && scanVolume.gameObject.activeInHierarchy;\n\n    private KeycardCredential resolvedCredential;")
    text = replace(text, "    private void Awake()\n    {\n        ResolveReferences();",
                   "    private void Awake()\n    {\n        scanVolume = GetComponent<BoxCollider>();\n        ResolveReferences();")
    # The first occurrence is Enter, the second is Exit. Keep the physical-card guard on both.
    anchor = "        if (other == null || other.isTrigger)\n            return;"
    if text.count(anchor) != 2:
        raise RuntimeError("Reader trigger callback guards changed")
    text = text.replace(anchor, "        if (other == null || other.isTrigger || !HasLiveOverlap(other))\n            return;", 1)
    text = replace(text, anchor,
                   "        if (other == null || other.isTrigger || !isActiveAndEnabled)\n            return;\n\n        // A stale physics exit must not erase a newer, still-valid overlap.\n        if (HasLiveOverlap(other))\n            return;")
    text = replace(text, "    private void OnTriggerExit(Collider other)", '''    private void OnTriggerStay(Collider other)
    {
        // Unity can deliver trigger messages to disabled behaviours. Enter's live
        // guard also makes re-enabling while a card is still inside recover safely.
        if (other != null && !acceptedColliders.Contains(other))
            OnTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)''')
    text = replace_method(text, "    private void LateUpdate()", '''    private void LateUpdate()
    {
        if (!ScannerActive)
        {
            ClearOverlapState();
            return;
        }

        RemoveInvalidAcceptedColliders();
        RetryPendingSubmissions();

        if (ScannerActive && showingAccepted)
            RefreshAcceptedVisual();
    }''')
    text = replace(text, "    private void OnDisable()\n    {\n        acceptedColliders.Clear();",
                   "    private void OnDisable()\n    {\n        ClearOverlapState();\n    }\n\n    private void ClearOverlapState()\n    {\n        acceptedColliders.Clear();")
    text = replace(text, "        if (gameplayCard == null || gameplayCard.IsInserted || !HasMatchingOverlap(gameplayCard))",
                   "        if (!ScannerActive || !submitMatchingCardsToKeyBox)\n            return;\n\n        if (gameplayCard == null || !gameplayCard.isActiveAndEnabled ||\n            gameplayCard.IsInserted || !HasMatchingOverlap(gameplayCard))")
    text = replace(text, "        // The accepted card is destroyed by its normal lifecycle, so hold the",
                   "        // A progress listener can disable the reader during acceptance.\n        if (!ScannerActive)\n            return;\n\n        // The accepted card is destroyed by its normal lifecycle, so hold the")
    text = replace(text, "            if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy && pair.Value == gameplayCard)",
                   "            if (pair.Value == gameplayCard && HasLiveOverlap(collider))")
    text = replace(text, "    private static KeycardCredential FindCredentialInHierarchy(Transform root)", '''    private bool HasLiveOverlap(Collider other)
    {
        if (!ScannerActive || other == null || other.isTrigger || !other.enabled ||
            !other.gameObject.activeInHierarchy || other.gameObject.scene != gameObject.scene)
            return false;

        KeyCard card = other.GetComponentInParent<KeyCard>();
        if (card != null && (!card.isActiveAndEnabled || card.IsInserted))
            return false;

        if (Physics.GetIgnoreLayerCollision(scanVolume.gameObject.layer, other.gameObject.layer) ||
            Physics.GetIgnoreCollision(scanVolume, other))
            return false;

        // Trigger exits can lag a transform/respawn or layer change. Check current
        // geometry without requiring a global SyncTransforms or physics simulation.
        return Physics.ComputePenetration(
            scanVolume, scanVolume.transform.position, scanVolume.transform.rotation,
            other, other.transform.position, other.transform.rotation,
            out _, out _);
    }

    private static KeycardCredential FindCredentialInHierarchy(Transform root)''')
    text = replace(text, "            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)",
                   "            if (!HasLiveOverlap(collider))")
    sources[READER] = text

    text = sources[BOX]
    text = replace(text, "    private XRSimpleInteractable retryInteraction;",
                   "    private XRSimpleInteractable retryInteraction;\n    private bool acceptingKey;")
    text = replace(text, "        if (SectorTravelService.I != null)\n            retryInteraction.interactionManager",
                   "        // This invisible target is for failed-travel retry, not ordinary\n        // card pickup. Do not let it compete with cards while the lock is incomplete.\n        retryInteraction.enabled = false;\n        if (SectorTravelService.I != null)\n            retryInteraction.interactionManager")
    text = replace(text, "        if (card == null || card.IsInserted || IsComplete || card.gameObject.scene != gameObject.scene)",
                   "        if (!isActiveAndEnabled || acceptingKey || card == null || !card.isActiveAndEnabled ||\n            card.IsInserted || IsComplete || card.gameObject.scene != gameObject.scene)")
    text = replace(text, "        if (!travelToLevelTwoOnComplete && !IsComplete) AcceptKey();",
                   "        if (isActiveAndEnabled && !acceptingKey && !travelToLevelTwoOnComplete && !IsComplete) AcceptKey();")
    text = replace_method(text, "    private void AcceptKey()", '''    private void AcceptKey()
    {
        acceptingKey = true;
        currentKeys++;
        try
        {
            NotifyProgressChanged();
            if (this != null && isActiveAndEnabled && IsComplete)
            {
                // Progress is authoritative even if a presentation/travel callback
                // fails. Completed Level 1 progress remains available for retry.
                if (travelToLevelTwoOnComplete) RetryCompletion();
                else if (door != null) door.OpenDoor();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
        finally
        {
            acceptingKey = false;
        }
    }

    private void NotifyProgressChanged()
    {
        Action<int, int> listeners = ProgressChanged;
        if (listeners == null)
            return;

        int acceptedKeys = CurrentKeys;
        int requiredKeys = RequiredKeys;
        foreach (Action<int, int> callback in listeners.GetInvocationList())
        {
            try
            {
                callback(acceptedKeys, requiredKeys);
            }
            catch (Exception exception)
            {
                // A broken lamp listener must not prevent consumption, completion,
                // or delivery to the remaining progress listeners.
                Debug.LogException(exception, this);
            }
        }
    }

    private void Update()
    {
        UpdateRetryInteraction();
    }

    private void UpdateRetryInteraction()
    {
        if (retryInteraction == null)
            return;

        bool available = isActiveAndEnabled && travelToLevelTwoOnComplete && IsComplete &&
            gameObject.scene == SceneManager.GetActiveScene() && SectorTravelService.I != null &&
            !SectorTravelService.I.IsBusy && SectorTravelService.I.CurrentSector == SectorId.Containment;
        if (retryInteraction.enabled != available)
            retryInteraction.enabled = available;
    }

    private void OnDisable()
    {
        if (retryInteraction != null)
            retryInteraction.enabled = false;
    }''')
    text = replace(text, "        if (IsComplete && travelToLevelTwoOnComplete && SectorTravelService.I != null)",
                   "        if (isActiveAndEnabled && IsComplete && travelToLevelTwoOnComplete && SectorTravelService.I != null)")
    sources[BOX] = text

    text = sources[PANEL]
    text = replace(text, "        if (subscribed || keyBox == null)",
                   "        if (!isActiveAndEnabled || subscribed || keyBox == null)")
    sources[PANEL] = text

    # Replace obsolete implementation-specific assertions with stronger equivalents.
    text = sources[CONTRACTS]
    text = replace(text, 'and "renderer.bounds" in keycard',
                   'and "renderer.localBounds" in keycard\n        and "renderer.transform.TransformPoint" in keycard\n        and "renderer.bounds" not in keycard')
    text = replace(text, 'and "ProgressChanged?.Invoke(CurrentKeys, RequiredKeys);" in keybox',
                   'and "NotifyProgressChanged();" in keybox\n        and "callback(acceptedKeys, requiredKeys);" in keybox\n        and "listeners.GetInvocationList()" in keybox')
    sources[CONTRACTS] = text
    text = sources[WORKFLOW]
    text = replace(text, "      - name: Validate PR15 review hardening",
                   "      - name: Validate interaction safety boundaries\n        run: python Tools/validate_interaction_safety.py\n\n      - name: Validate PR15 review hardening")
    sources[WORKFLOW] = text

    design = '''

## September 14 interaction review after the trigger-safe patch

**Confirmed request and validation boundary:** Greg requested review of PR #23 and its related interaction code after the trigger-safe fix at `08d44b5`. The previously reported sticky hands, invisible obstruction and unreliable pickup remain failed runtime validation until retested. The earlier one-card 0/2 -> 1/2 success remains a narrow historical result, not proof of this new head. No objective, credential, grip-input, personal-progress, Photon-ownership or world-wall collision rule is changed by this review.

**Source findings and implemented corrections on `feature/keycard-reader-light-feedback`:**

- A rotated card was fitted by inverse-transforming an already axis-aligned world renderer box, inflating the thin dimension. Fitting now transforms renderer-local bounds corners directly into card space; existing physical padding, lightweight mass, continuous handling and separate trigger-only pickup affordance remain.
- Release restored the solid card's original layer before it cleared the local rig. Pairwise collision ignores cannot protect Gorilla sphere/ray queries, so both the held solid layer and owned collision ignores now remain until separation. Trigger-only pickup sensors retain their authored layers throughout. Re-grab refreshes separation contacts, including pairs already ignored by another system, without taking ownership of those pre-existing ignores.
- Reader retries and green presence trusted cached enter events. They now require enabled reader/card state and current solid-card overlap with the actual scan box, including layer/pair filtering. Stale overlaps are pruned before retries; OnTriggerStay can reacquire a valid card after re-enabling; disabled callbacks cannot submit, and disabling during a progress callback cannot relight the reader. The brief green acknowledgement after successful consumption is retained.
- The legacy completion-retry XRSimpleInteractable was selectable while the lock was incomplete, potentially competing with card acquisition near the reader. It is now enabled only for a completed local Level 1 lock while travel is idle, and disabled with its KeyBox. Failed-travel retry remains available; this does not invent a new retry button or grip binding.
- Disabled or reentrant card/objective submissions fail without advancing progress. Synchronous progress listeners are isolated from authoritative acceptance, so one listener exception cannot leave a counted card unconsumed or prevent other listeners receiving progress. A disabled lock panel cannot subscribe through Bind.
- The Gorilla collision solver's inner correction used the outer hit distance. It now uses the inner hit distance while retaining all seven explicit trigger-ignoring queries and existing world-surface behavior.

**Supersedes:** restoring solid-item layers immediately on release is no longer the active rule. Earlier reasoning that a trigger-only reader rules out a movement obstruction remains superseded by the trigger-query finding above. Do not remove legitimate wall/gate colliders or globally disable trigger queries to compensate.

**Implemented regression protection / pending validation:** `Tools/validate_interaction_safety.py` is added to read-only Source Integrity. It checks these boundaries, includes eight intentionally broken source fixtures and fourteen rotated/scaled coordinate-math fixtures; these are not Unity or PhysX tests. Source results must be recorded against the tested commit. Unity 2022.3.55f1 import/compile, XRI 2.5.4 pickup/drop/re-grab and hand switching, reader enable/disable/teleport cases, ordinary floor/wall movement, complete two-card travel/retry, two-client Photon and Quest/headset comfort/performance remain pending.
'''
    plan = '''

## September 14 PR #23 interaction-path review

**Scope / confirmed status:** review the `08d44b5` trigger-safe follow-up and its card, held-prop, locomotion, reader, lock-progress and completion paths on the existing `feature/keycard-reader-light-feedback` branch. Greg's latest hand-sticking/forcefield/repeated-pickup report is still an unresolved runtime test. The historical 1/2 scan success must not be used as validation of the full flow or new code.

**Implemented source corrections:** direct renderer-local-to-card-space collider fitting removes rotation-dependent AABB inflation; release defers both solid-layer and owned-pair restoration until physical separation while preserving grab triggers; immediate re-grab refreshes solid contact tracking without undoing pre-existing ignores; reader acceptance/presence requires current enabled, filter-valid solid-card penetration of its scan box rather than stale enter events; stale overlaps are removed before retry and stay callbacks recover re-enabled readers; disabled and reentrant submissions are rejected; progress-listener exceptions are isolated; disabled panels cannot subscribe through Bind. The legacy hidden XRSimpleInteractable on the completion box is now available only for completed, idle, local Level 1 travel retry instead of competing with unfinished card pickup. The Gorilla inner spherecast correction now uses innerHit.distance rather than the previous outer hit's distance.

**Regression protection:** retain all existing keycard/scene/credential/binding contracts, replacing the obsolete world-bounds and direct-event-invocation assertions with local-corner fitting and isolated event delivery assertions. Add `Tools/validate_interaction_safety.py` to the existing read-only Source Integrity workflow. Eight negative source mutations verify representative regressions are rejected; fourteen 1x/2x rotated-coordinate fixtures check bounds mathematics. No global physics setting, world collider, input binding, scene asset, key count, card credential or networking architecture is changed. The temporary branch-scoped patch applicator is removed after successful application; it is not part of the permanent validation workflow.

**Source-validation boundary:** the apply job must run first-party C# syntax/reference checks, repository integrity, Level 1 and keycard contracts, the new interaction contracts/negative fixtures/math fixtures, PR15/security/threat contracts, Python compilation and whitespace before publishing the correction. A successful source result is not Unity compilation or gameplay validation; record the actual run and final branch revision separately.

**Pending acceptance:** repeatedly touch and withdraw either hand at the reader, preserving real wall/floor contact; drop and immediately re-grab both cards flat, rotated, near walls, near the body and with hand changes; no movement-blocking grab trigger, release-frame kick or hidden incomplete-lock selection target; move/respawn a pending card out of the reader before retry and verify it cannot count or leave a stale presence light; disable/re-enable the reader/card/KeyBox/panel during overlap and submission; confirm only the real card reaches the scan zone; confirm wrong-card and duplicate rejection, 0/2 -> 1/2 -> 2/2 exactly-once consumption, ordinary travel and failed-travel retry; then two-client personal independence and Quest/headset behavior. Any remaining barrier needs the exact contacted collider identified in Play Mode, not larger interaction volumes or globally disabled world collision.
'''
    for path, addition in zip(DOCS, (design, plan)):
        sources[path] = sources[path].rstrip() + addition + "\n"

    for path, content in sources.items():
        Path(path).write_text(content.rstrip() + "\n", encoding="utf-8")
    print("Applied only the reviewed interaction sources, contracts, read-only workflow and maintained docs.")


if __name__ == "__main__":
    main()
