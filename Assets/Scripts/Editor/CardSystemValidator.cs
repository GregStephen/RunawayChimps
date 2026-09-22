#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>Read-only authoring checks. Never saves scenes or changes components.</summary>
public static class CardSystemValidator
{
    [MenuItem("Tools/Runaway Chimps/Cards/Validate Loaded Scenes")]
    public static void ValidateLoadedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CardValidation] Stop Play Mode before validating authored setup.");
            return;
        }
        var roots = new List<GameObject>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
                roots.AddRange(scene.GetRootGameObjects());
        }
        Validate(roots, false);
    }

    [MenuItem("Tools/Runaway Chimps/Cards/Validate Selected Prefab or Hierarchy")]
    public static void ValidateSelection()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[CardValidation] Stop Play Mode before validating authored setup.");
            return;
        }
        if (Selection.gameObjects.Length == 0)
        {
            Debug.LogWarning("[CardValidation] Select a prefab asset or a scene hierarchy first.");
            return;
        }
        Validate(Selection.gameObjects, true);
    }

    private static T Reference<T>(Component component, string property) where T : Object
    {
        using (var serialized = new SerializedObject(component))
            return serialized.FindProperty(property)?.objectReferenceValue as T;
    }

    private static bool Flag(Component component, string property)
    {
        using (var serialized = new SerializedObject(component))
            return serialized.FindProperty(property)?.boolValue ?? false;
    }

    private static int Number(Component component, string property)
    {
        using (var serialized = new SerializedObject(component))
            return serialized.FindProperty(property)?.intValue ?? 0;
    }

    private static bool ValidCredential(int value) => value >= 1 && value <= 4;

    private static void Validate(IEnumerable<GameObject> roots, bool includeInactive)
    {
        var components = new HashSet<Component>();
        foreach (GameObject root in roots)
        {
            if (root == null) continue;
            foreach (Component component in root.GetComponentsInChildren<Component>(includeInactive))
                if (component != null && (includeInactive || component.gameObject.activeInHierarchy))
                    components.Add(component);
        }
        int errors = 0, warnings = 0, cards = 0, readers = 0, panels = 0;
        System.Action<Object, string> error = (context, message) =>
        {
            errors++;
            Debug.LogError("[CardValidation] " + message, context);
        };
        System.Action<Object, string> warn = (context, message) =>
        {
            warnings++;
            Debug.LogWarning("[CardValidation] " + message, context);
        };

        foreach (Component component in components)
        {
            if (component is KeyCard card)
            {
                cards++;
                if (!ValidCredential((int)card.Credential))
                    error(card, "Assign an explicit supported card credential (1-4); Auto/name parsing is legacy only.");
                if (card.GetComponent<VRKeyCard>() != null)
                    error(card, "Do not combine physical KeyCard with VRKeyCard's collect-and-destroy inventory path.");
                if (card.GetComponentInParent<Photon.Pun.PhotonView>() != null)
                    error(card, "This is a visit-local personal card, not a PhotonView-owned shared prop. Shared cards need a separate approved ownership lifecycle.");
                BoxCollider solid = card.GetComponent<BoxCollider>();
                Rigidbody body = card.GetComponent<Rigidbody>();
                XRGrabInteractable grab = card.GetComponent<XRGrabInteractable>();
                if (solid == null || body == null || grab == null || card.GetComponent<HeldItemCollisionMode>() == null)
                    error(card, "Card needs root BoxCollider, Rigidbody, XRGrabInteractable and HeldItemCollisionMode.");
                if (solid != null && (!solid.enabled || solid.isTrigger))
                    error(card, "The root physical card collider must be enabled and solid.");
                if (body != null && (!body.detectCollisions || body.constraints != RigidbodyConstraints.None))
                    error(card, "Card collision detection must be enabled and Rigidbody constraints must not pin its physical motion.");
                if (grab != null && grab.movementType != XRBaseInteractable.MovementType.VelocityTracking)
                    warn(card, "KeyCard normalizes movement to VelocityTracking on startup; this authored grab setting is different. Verify no interactor overrides it.");
                if (card.GetComponent<RespawnToOriginalSpawn>() == null)
                    warn(card, "No fall-recovery component is authored. Supply RespawnToOriginalSpawn or an explicitly tested replacement.");
                bool visualFound = false;
                foreach (Renderer renderer in card.GetComponentsInChildren<Renderer>(true))
                    if ((renderer is MeshRenderer || renderer is SkinnedMeshRenderer) && renderer.GetComponentInParent<KeyCard>() == card)
                        visualFound = true;
                if (!visualFound)
                    error(card, "No mesh bounds to fit: the default metre-sized physics box would remain.");
                foreach (Collider collider in card.GetComponentsInChildren<Collider>(true))
                {
                    if (collider.GetComponentInParent<KeyCard>() != card) continue;
                    if (!collider.isTrigger && collider != solid)
                        error(collider, "Remove extra solid card colliders; only the root fitted box is authorized to scan.");
                    if (body != null && collider.attachedRigidbody != body)
                        error(collider, "Card child colliders must share its root Rigidbody, not a nested body.");
                    if (collider.isTrigger)
                        foreach (Collider sibling in collider.GetComponents<Collider>())
                            if (!sibling.isTrigger)
                                error(collider, "Put acquisition triggers on a separate child GameObject, not on the solid collider's layer-changing object.");
                }
                Vector3 scale = card.transform.lossyScale;
                if (scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
                    error(card, "Use positive, nonzero physical card scale; mirrored/degenerate physics is unsupported.");
            }
            else if (component is KeycardReaderLightController reader)
            {
                readers++;
                BoxCollider scan = reader.GetComponent<BoxCollider>();
                if (scan == null || !scan.enabled || !scan.isTrigger)
                    error(reader, "Reader requires one enabled trigger BoxCollider on its controller object.");
                if (reader.GetComponents<Collider>().Length != 1)
                    error(reader, "Keep the scan controller object unambiguous: exactly one scan collider.");
                if (!ValidCredential(Number(reader, "expectedCredential")))
                    error(reader, "Assign an explicit supported reader credential (1-4). Reader_Base is a template, not a configured lock.");
                if (Reference<Renderer>(reader, "statusLedRenderer") == null ||
                    Reference<Material>(reader, "standbyMaterial") == null || Reference<Material>(reader, "acceptedMaterial") == null)
                    error(reader, "Assign the reader LED and both status materials.");
                if (!Flag(reader, "submitMatchingCardsToKeyBox")) continue;
                KeyBox readerObjective = Reference<KeyBox>(reader, "keyBox");
                Transform root = Reference<Transform>(reader, "readerRoot") ?? reader.transform.parent;
                if (readerObjective == null && root != null)
                {
                    KeyBox[] nested = root.GetComponentsInChildren<KeyBox>(true);
                    if (nested.Length == 1) readerObjective = nested[0];
                }
                if (readerObjective == null)
                    error(reader, "Gameplay reader needs an explicit objective or exactly one deliberately nested KeyBox. Bind dynamically before presenting cards.");
                else
                {
                    if (reader.gameObject.scene.IsValid() && readerObjective.gameObject.scene != reader.gameObject.scene)
                        error(reader, "Reader and objective must belong to the same scene.");
                    if (!readerObjective.requireMatchingReader && !readerObjective.travelToLevelTwoOnComplete)
                        warn(readerObjective, "Enable Require Matching Reader for reusable locks; do not rely only on a reader's Awake opt-in, particularly with inactive readers.");
                }
            }
            else if (component is KeyBox objectiveComponent)
            {
                if (objectiveComponent.keysNeeded < 1)
                    error(objectiveComponent, "Required card count must be at least one.");
            }
            else if (component is KeycardLockProgressIndicator panel)
            {
                panels++;
                KeyBox panelObjective = Reference<KeyBox>(panel, "keyBox") ?? panel.GetComponentInParent<KeyBox>();
                if (panelObjective == null)
                    warn(panel, "Panel is unbound preview only. Explicitly bind it before using it as objective feedback.");
                if (Reference<Material>(panel, "standbyMaterial") == null || Reference<Material>(panel, "acceptedMaterial") == null)
                    error(panel, "Assign both lamp status materials.");
                using (var serialized = new SerializedObject(panel))
                {
                    SerializedProperty lights = serialized.FindProperty("progressLights");
                    int required = panelObjective != null ? panelObjective.RequiredKeys : Mathf.Max(1, Number(panel, "previewRequiredKeys"));
                    if (lights == null || lights.arraySize < required)
                        error(panel, "The panel cannot represent every required card; expand its authored lamp array.");
                    if (lights == null) continue;
                    var seen = new HashSet<Renderer>();
                    for (int i = 0; i < lights.arraySize; i++)
                    {
                        Renderer lamp = lights.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                        if (lamp == null || !seen.Add(lamp))
                            error(panel, "Lamp slots must reference distinct non-null renderers.");
                        else if (panel.transform.IsChildOf(lamp.transform) || !lamp.transform.IsChildOf(panel.transform))
                            error(panel, "Lamp renderers must be children of this panel, never its root/ancestor or an unrelated hierarchy.");
                    }
                }
            }
        }
        Debug.Log($"[CardValidation] Read-only review: {cards} cards, {readers} readers, {panels} panels; {errors} errors, {warnings} warnings. This does not run Unity physics, XRI input, Photon or headset tests.");
    }
}
#endif
