#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RunawayChimps.Toys.PrimateCognitive.Editor
{
    [CustomEditor(typeof(CognitiveMachine))]
    public sealed class CognitiveMachineEditor : UnityEditor.Editor
    {
        public const string PrefabPath = "Assets/RunawayChimps/Toys/PrimateCognitive/Prefabs/PrimateCognitiveEvaluation.prefab";
        private const string HubPath = "Assets/Scenes/Hub_Base.unity";
        private readonly bool[] held = new bool[5];

        [MenuItem("Tools/Runaway Chimps/Toys/Place Cognitive Evaluation in Hub")]
        public static void PlaceInHub()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Place the cognitive machine outside Play Mode.");
                return;
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Missing authored cognitive prefab: " + PrefabPath);
            Scene previous = SceneManager.GetActiveScene();
            Scene hub = SceneManager.GetSceneByPath(HubPath);
            if (!hub.isLoaded) hub = EditorSceneManager.OpenScene(HubPath, OpenSceneMode.Additive);
            var roots = hub.GetRootGameObjects();
            var existing = roots.SelectMany(r => r.GetComponentsInChildren<CognitiveMachine>(true)).FirstOrDefault();
            if (existing != null)
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("Selected the existing cognitive machine; its placement was preserved.", existing);
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Place Cognitive Evaluation");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, hub);
            Undo.RegisterCreatedObjectUndo(instance, "Place Cognitive Evaluation");
            var room = roots.FirstOrDefault(r => r.name == "SpawnRoom");
            if (room != null) Undo.SetTransformParent(instance.transform, room.transform, "Parent cognitive machine");
            // A deliberate test placement to the right of the computer return, outside Hub slots.
            // Keep the prefab's front (-Z) facing the open room. Do not modify any other root.
            Undo.RecordObject(instance.transform, "Position cognitive machine");
            instance.transform.SetPositionAndRotation(FindTestFloor(hub), Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            EditorSceneManager.MarkSceneDirty(hub);
            Undo.CollapseUndoOperations(group);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            Selection.activeGameObject = instance;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log("Placed cognitive evaluation. Inspect floor/route clearance; move freely. Save Hub manually to keep it.", instance);
        }

        private static Vector3 FindTestFloor(Scene hub)
        {
            Physics.SyncTransforms();
            var floor = Physics.RaycastAll(new Vector3(2.4f, 2.5f, -1.2f), Vector3.down, 5f,
                    ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.collider != null && h.collider.gameObject.scene == hub &&
                    h.normal.y > .9f && h.point.y < .5f)
                .OrderByDescending(h => h.point.y).FirstOrDefault();
            if (floor.collider != null) return floor.point + Vector3.up * .005f;
            Debug.LogWarning("No Hub floor found at the cognitive test position. Inspect and adjust the placement before saving.");
            return new Vector3(2.4f, 0f, -1.2f);
        }

        [MenuItem("Tools/Runaway Chimps/Toys/Validate Cognitive Evaluation Prefab")]
        public static void ValidatePrefab()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var machine = root != null ? root.GetComponent<CognitiveMachine>() : null;
            if (machine == null || machine.display == null || machine.audioSource == null || machine.operatorAnchor == null ||
                machine.pads == null || machine.pads.Length != 5 || machine.clips == null || machine.clips.Length != 6)
                throw new InvalidOperationException("Cognitive prefab references are incomplete.");
            for (int i = 0; i < 5; i++)
            {
                var pad = machine.pads[i];
                var collider = pad != null ? pad.GetComponent<BoxCollider>() : null;
                var body = pad != null ? pad.GetComponent<Rigidbody>() : null;
                if (pad == null || pad.index != i || pad.machine != machine || pad.cap == null || pad.capRenderer == null ||
                    pad.capRenderer.sharedMaterial == null || collider == null || !collider.isTrigger ||
                    body == null || !body.isKinematic || body.useGravity)
                    throw new InvalidOperationException("Invalid cognitive pad " + i);
            }
            if (root.transform.localScale != Vector3.one || machine.display.font == null ||
                machine.audioSource.playOnAwake || machine.audioSource.spatialBlend < .99f)
                throw new InvalidOperationException("Cognitive root scale, display font or spatial audio authoring is invalid.");
            foreach (var clip in machine.clips)
                if (clip == null || clip.length <= 0) throw new InvalidOperationException("Missing cognitive audio clip.");
            if (root.GetComponentInChildren<Camera>(true) != null || root.GetComponentInChildren<AudioListener>(true) != null)
                throw new InvalidOperationException("The machine must use the existing rig camera and listener.");
            Debug.Log("Cognitive prefab references passed. Play Mode, Photon and headset acceptance remain separate.", root);
        }

        private void OnEnable() { EditorApplication.update += UpdateContacts; }
        private void OnDisable()
        {
            Array.Clear(held, 0, held.Length);
            UpdateContacts();
            EditorApplication.update -= UpdateContacts;
        }

        private void UpdateContacts()
        {
            var machine = target as CognitiveMachine;
            if (machine == null || !Application.isPlaying) return;
            for (int i = 0; i < held.Length; i++)
                machine.EditorContact(i, machine.editorControls && held[i]);
            if (!machine.editorControls) Array.Clear(held, 0, held.Length);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var machine = (CognitiveMachine)target;
            EditorGUILayout.Space();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode in Hub, enable Editor Controls, and use the held-contact toggles below. Save only your chosen placement. Manual duplicates need a unique ID in every client's scene.", MessageType.Info);
                if (GUILayout.Button("Assign unique ID to this placement"))
                {
                    Undo.RecordObject(machine, "Assign cognitive identity");
                    machine.machineId = "cognitive-" + Guid.NewGuid().ToString("N");
                    PrefabUtility.RecordPrefabInstancePropertyModifications(machine);
                    EditorUtility.SetDirty(machine);
                }
                return;
            }
            EditorGUILayout.HelpBox("Toggle ON to touch; OFF to release. Keep ON through a demonstration to test held-contact rejection. All controls use the production contact gate, ownership, network messages and game rules. Selection loss releases these virtual contacts.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!machine.editorControls))
            {
                for (int i = 0; i < held.Length; i++)
                    held[i] = EditorGUILayout.ToggleLeft(i == 4 ? "HOLD START / RESTART" : "HOLD PAD " + (i + 1), held[i]);
                if (GUILayout.Button("Release all virtual contacts")) Array.Clear(held, 0, held.Length);
            }
            if (machine.State != null)
                EditorGUILayout.LabelField("Live state", machine.State.Phase + " | round " + machine.State.Round + " | operator " + machine.State.Owner);
        }
    }
}
#endif
