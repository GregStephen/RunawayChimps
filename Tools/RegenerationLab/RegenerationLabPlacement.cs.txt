using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RunawayChimps.EditorEnvironment
{
    /// <summary>Explicit additive placement only. No scene-open, save, startup or rebuild hooks.</summary>
    public sealed class RegenerationLabPlacement : EditorWindow
    {
        private const string Root = "Assets/RunawayChimps/Environment/RegenerationLab/";
        private const string MenuRoot = "Tools/Runaway Chimps/Environment/";
        private static readonly string[] Names =
        {
            "RGL_TreatmentTrolley", "RGL_ChemicalDeliveryStand", "RGL_SpecimenColdCabinet"
        };
        private static readonly Vector3[] Offsets =
        {
            new Vector3(-1.05f, 0f, 0f), new Vector3(0f, 0f, 0.10f), new Vector3(1.0f, 0f, 0f)
        };
        [SerializeField] private int sceneHandle;
        [SerializeField] private Vector3 floorAnchor;
        [SerializeField] private float yaw;

        [MenuItem(MenuRoot + "Place Regeneration Lab Review...")]
        private static void OpenPlacement()
        {
            var window = GetWindow<RegenerationLabPlacement>("Regeneration Lab");
            window.minSize = new Vector2(420f, 325f);
            window.UseSelectionOrActiveScene();
            window.Show();
        }

        private static bool IsOrdinaryLoadedScene(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded && !EditorSceneManager.IsPreviewScene(scene);
        }

        private void UseSelectionOrActiveScene()
        {
            var selected = Selection.activeGameObject;
            var scene = selected != null ? selected.scene : SceneManager.GetActiveScene();
            if (IsOrdinaryLoadedScene(scene)) sceneHandle = scene.handle;
            if (selected != null && IsOrdinaryLoadedScene(selected.scene))
            {
                floorAnchor = selected.transform.position;
                yaw = selected.transform.eulerAngles.y;
            }
            // No inferred floor height: the explicit world-space field is authoritative.
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Regeneration laboratory / review kit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Adds NEW linked prefab instances only. Nothing existing is rebuilt, " +
                "moved, deleted or saved. Choose clear floor space; this does not validate navigation.", MessageType.Info);
            var scenes = new List<Scene>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene candidate = SceneManager.GetSceneAt(i);
                if (IsOrdinaryLoadedScene(candidate)) scenes.Add(candidate);
            }
            if (scenes.Count == 0)
            {
                EditorGUILayout.HelpBox("Open a normal scene before placing the kit.", MessageType.Warning);
                return;
            }
            int index = scenes.FindIndex(s => s.handle == sceneHandle);
            if (index < 0) index = 0;
            index = EditorGUILayout.Popup("Loaded target scene", index,
                scenes.Select(s => (string.IsNullOrEmpty(s.name) ? "Untitled" : s.name) + " [" + s.handle + "]").ToArray());
            sceneHandle = scenes[index].handle;
            floorAnchor = EditorGUILayout.Vector3Field("Floor anchor (world metres)", floorAnchor);
            yaw = EditorGUILayout.FloatField("Facing yaw (degrees)", yaw);
            if (GUILayout.Button("Use selected object's position and scene")) UseSelectionOrActiveScene();
            EditorGUILayout.LabelField("The anchor is a FLOOR point, not the centre of a room.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("+Z faces the labels. Review footprint is approximately 2.9 x 0.8 m. " +
                "Leave both vent routes, cards and safe boundaries clear.", EditorStyles.wordWrappedLabel);
            bool blocked = EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null || !Finite(floorAnchor) || !Finite(yaw);
            if (blocked)
                EditorGUILayout.HelpBox("Exit Play/Prefab Mode and use finite coordinates before placing.", MessageType.Warning);
            using (new EditorGUI.DisabledScope(blocked))
            {
                if (GUILayout.Button("Place NEW review arrangement", GUILayout.Height(32f)))
                    Place(scenes[index], floorAnchor, yaw);
            }
            if (GUILayout.Button("Validate imported kit assets (read only)")) ValidateImportedAssets();
        }

        // Also guarded at the operation boundary so stale window state cannot mutate the wrong scene.
        internal static void Place(Scene scene, Vector3 anchor, float rotation)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null ||
                !IsOrdinaryLoadedScene(scene) || !Finite(anchor) || !Finite(rotation))
                throw new InvalidOperationException("Choose a loaded ordinary scene and finite placement outside Play/Prefab Mode.");
            GameObject[] prefabs = LoadPrefabs(); // Preflight before creating even the group.
            foreach (GameObject prefab in prefabs) ValidatePrefab(prefab);
            string name = "Regeneration Lab Review";
            var existingNames = new HashSet<string>(scene.GetRootGameObjects().Select(go => go.name));
            for (int suffix = 2; existingNames.Contains(name); suffix++) name = "Regeneration Lab Review (" + suffix + ")";
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Place regeneration lab review");
            try
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, scene);
                Undo.RegisterCreatedObjectUndo(root, "Create review group");
                Undo.RecordObject(root.transform, "Position review group");
                root.transform.SetPositionAndRotation(anchor, Quaternion.Euler(0f, rotation, 0f));
                root.transform.localScale = Vector3.one;
                for (int i = 0; i < prefabs.Length; i++)
                {
                    var instance = PrefabUtility.InstantiatePrefab(prefabs[i], scene) as GameObject;
                    if (instance == null) throw new InvalidOperationException("Failed to instantiate " + Names[i]);
                    Undo.RegisterCreatedObjectUndo(instance, "Create kit prefab instance");
                    Undo.SetTransformParent(instance.transform, root.transform, "Parent kit prefab instance");
                    Undo.RecordObject(instance.transform, "Position kit prefab instance");
                    instance.transform.localPosition = Offsets[i];
                    instance.transform.localRotation = Quaternion.identity;
                    instance.transform.localScale = Vector3.one;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                }
                EditorSceneManager.MarkSceneDirty(scene); // User decides whether/when to save.
                Selection.activeGameObject = root;
                Undo.CollapseUndoOperations(group);
                Debug.Log("Added a NEW regeneration lab review group to " + scene.name +
                    ". Individual props retain prefab links. Scene not saved; inspect clearance before saving.", root);
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(exception);
            }
        }

        [MenuItem(MenuRoot + "Validate Regeneration Lab Kit")]
        private static void ValidateImportedAssets()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Run asset validation outside Play Mode.");
                if (GraphicsSettings.currentRenderPipeline != null)
                    throw new InvalidOperationException("This kit targets the project's Built-in Render Pipeline.");
                var report = new StringBuilder("Regeneration kit imported-asset inspection (NOT a headset/performance pass):\n");
                foreach (GameObject prefab in LoadPrefabs())
                {
                    ValidatePrefab(prefab);
                    long triangles = 0;
                    foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    {
                        Mesh mesh = filter.sharedMesh;
                        for (int i = 0; i < mesh.subMeshCount; i++) triangles += (long)mesh.GetIndexCount(i) / 3;
                    }
                    report.AppendLine(prefab.name + ": " + triangles + " triangles, " +
                        prefab.GetComponentsInChildren<Collider>(true).Length + " static box collider(s).");
                }
                report.Append("PASS: baked mesh data, references, identity transforms, Standard materials and static-only components. " +
                    "Still inspect rendering, labels, scale, Undo/Redo, navigation and target Quest performance.");
                Debug.Log(report.ToString());
            }
            catch (Exception exception) { Debug.LogError("Regeneration kit validation FAILED: " + exception.Message); }
        }

        private static GameObject[] LoadPrefabs()
        {
            return Names.Select(name =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + name + ".prefab");
                if (prefab == null) throw new InvalidOperationException("Missing or unimported prefab: " + name);
                return prefab;
            }).ToArray();
        }

        private static void ValidatePrefab(GameObject prefab)
        {
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Animator>(true).Length != 0 ||
                prefab.GetComponentsInChildren<Light>(true).Length != 0 ||
                prefab.GetComponentsInChildren<AudioSource>(true).Length != 0)
                throw new InvalidOperationException(prefab.name + " must remain a static, script-free prop.");
            foreach (Transform transform in prefab.GetComponentsInChildren<Transform>(true))
                if (transform.localPosition.sqrMagnitude > 0.000001f ||
                    (transform.localScale - Vector3.one).sqrMagnitude > 0.000001f ||
                    Quaternion.Angle(transform.localRotation, Quaternion.identity) > 0.001f)
                    throw new InvalidOperationException(prefab.name + " has a non-identity authored transform.");
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length != 2) throw new InvalidOperationException(prefab.name + " requires body and editable-label meshes.");
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null || renderer.sharedMaterials.Length != mesh.subMeshCount)
                    throw new InvalidOperationException(prefab.name + " has missing mesh/material bindings.");
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uv = mesh.uv;
                if (vertices.Length == 0 || normals.Length != vertices.Length || uv.Length != vertices.Length)
                    throw new InvalidOperationException(mesh.name + " is missing vertex normals or UV0.");
                for (int i = 0; i < vertices.Length; i++)
                    if (!Finite(vertices[i]) || !Finite(normals[i]) || !Finite(uv[i].x) || !Finite(uv[i].y) ||
                        Mathf.Abs(normals[i].magnitude - 1f) > 0.002f)
                        throw new InvalidOperationException(mesh.name + " has invalid mesh data.");
                if (filter.name == "BakedMesh" && Mathf.Abs(mesh.bounds.min.y) > 0.001f)
                    throw new InvalidOperationException(mesh.name + " is not floor-contact authored.");
                foreach (Material material in renderer.sharedMaterials)
                    if (material == null || material.shader == null || material.shader.name != "Standard" ||
                        material.mainTexture == null || material.GetFloat("_Mode") != 0f)
                        throw new InvalidOperationException(mesh.name + " requires assigned opaque textured Standard materials.");
            }
            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0) throw new InvalidOperationException(prefab.name + " lacks its broad collision proxy.");
            foreach (Collider collider in colliders)
            {
                var box = collider as BoxCollider;
                if (box == null || !box.enabled || box.isTrigger || !Finite(box.center) || !Finite(box.size) ||
                    box.size.x <= 0f || box.size.y <= 0f || box.size.z <= 0f)
                    throw new InvalidOperationException(prefab.name + " has invalid static collision.");
            }
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
    }
}
