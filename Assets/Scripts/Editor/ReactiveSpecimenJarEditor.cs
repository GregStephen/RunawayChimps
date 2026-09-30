using RunawayChimps.Toys;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(ReactiveSpecimenJar))]
public sealed class ReactiveSpecimenJarEditor : Editor
{
    public const string PrefabPath = "Assets/RunawayChimps/Toys/ReactiveSpecimenJar/ReactiveSpecimenJar.prefab";
    private const string HubPath = "Assets/Scenes/Hub_Base.unity";

    [MenuItem("Tools/Runaway Chimps/Toys/Place Reactive Specimen Jar in Hub")]
    public static void PlaceInHub()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetSceneByPath(HubPath);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Open the Hub first",
                "Open Assets/Scenes/Hub_Base.unity, then run this command. No scene was opened, edited or saved.", "OK");
            return;
        }
        Transform anchor = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var existing = root.GetComponentInChildren<ReactiveSpecimenJar>(true);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("Existing specimen jar selected; its placement and tuning were preserved.", existing);
                return;
            }
            foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                if (item.name == "HubReturnSpawn") anchor = item;
        }
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (asset == null || anchor == null)
        {
            Debug.LogError("Specimen jar placement requires its serialized prefab and the HubReturnSpawn marker. Nothing changed.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Place Reactive Specimen Jar");
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Place Reactive Specimen Jar");
        Undo.RecordObject(instance.transform, "Position Reactive Specimen Jar");
        instance.transform.SetPositionAndRotation(
            anchor.position + anchor.right * 1.1f + Vector3.up * 0.95f,
            Quaternion.LookRotation(-anchor.forward, Vector3.up));
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = instance;
        Debug.Log("Specimen jar placed beside the Hub return marker. Inspect clearance, move it as needed, and save manually. Undo is supported.", instance);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var jar = (ReactiveSpecimenJar)target;
        EditorGUILayout.HelpBox("LOCAL PER VIEWER: no shared target or Photon synchronization. Front is +Z. Keep uniform root scale and the authored specimen size/bounds. Preview input is Editor-only.", MessageType.Info);
        EditorGUILayout.LabelField("State / target", jar.CurrentMood + " / " + (jar.SelectedHand < 0 ? "none" : jar.SelectedHand == 0 ? "left" : "right"));
        EditorGUILayout.LabelField("Taps / watches since reset", jar.ReactionCount + " / " + jar.WatchCount);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || !IsSceneInstance(jar)))
            if (GUILayout.Button("Create desktop preview handles (Undoable)")) CreatePreviewHandles(jar);
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !jar.editorPreview))
        {
            if (GUILayout.Button("Preview recoil (visual only; bypasses tap detection)")) jar.PreviewRecoil();
            if (GUILayout.Button("Reset preview state")) jar.ResetState();
        }
        if (Application.isPlaying) Repaint();
    }

    // Prefab Mode and LoadPrefabContents use nonpersistent objects in preview scenes.
    // IsPersistent alone does not protect the shipping prefab from test-only handles.
    private static bool IsSceneInstance(Component jar)
        => jar != null && !EditorUtility.IsPersistent(jar) && jar.gameObject.scene.IsValid() &&
           jar.gameObject.scene.isLoaded && !EditorSceneManager.IsPreviewScene(jar.gameObject.scene);

    private static void CreatePreviewHandles(ReactiveSpecimenJar jar)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !IsSceneInstance(jar)) return;
        if (jar.previewHead != null || jar.previewLeftHand != null || jar.previewRightHand != null)
        { Debug.Log("Existing preview handles preserved. Enable Editor Preview and move/rotate them in Play Mode.", jar); return; }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Create specimen preview handles");
        var root = new GameObject("SpecimenPreview_EditorOnly");
        root.tag = "EditorOnly";
        Undo.RegisterCreatedObjectUndo(root, "Create specimen preview handles");
        SceneManager.MoveGameObjectToScene(root, jar.gameObject.scene);
        Undo.SetTransformParent(root.transform, jar.transform, "Parent specimen preview handles");
        Transform head = Handle(root.transform, "Preview Head - rotate to look away");
        Transform left = Handle(root.transform, "Preview Left Hand");
        Transform right = Handle(root.transform, "Preview Right Hand");
        // RegisterCreatedObjectUndo flushes/stops pending RecordObject recording.
        // Complete ALL creations first, then record poses and the jar's assignments.
        Undo.RegisterFullObjectHierarchyUndo(root, "Position specimen preview handles");
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        head.localPosition = new Vector3(0f, 0.06f, 0.8f);
        head.localRotation = Quaternion.Euler(0f, 180f, 0f);
        head.localScale = Vector3.one;
        left.localPosition = new Vector3(-0.24f, 0f, 0.4f);
        left.localRotation = Quaternion.identity;
        left.localScale = Vector3.one;
        right.localPosition = new Vector3(0.4f, 0f, 0.55f);
        right.localRotation = Quaternion.identity;
        right.localScale = Vector3.one;
        Undo.RecordObject(jar, "Assign specimen preview handles");
        jar.previewHead = head;
        jar.previewLeftHand = left;
        jar.previewRightHand = right;
        jar.editorPreview = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(jar);
        EditorUtility.SetDirty(jar);
        EditorSceneManager.MarkSceneDirty(jar.gameObject.scene);
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(group);
    }

    private static Transform Handle(Transform parent, string label)
    {
        var go = new GameObject(label);
        go.tag = "EditorOnly";
        Undo.RegisterCreatedObjectUndo(go, "Create specimen preview handle");
        Undo.SetTransformParent(go.transform, parent, "Parent specimen preview handle");
        return go.transform;
    }

    private void OnSceneGUI()
    {
        var jar = (ReactiveSpecimenJar)target;
        if (!jar.editorPreview || !IsSceneInstance(jar)) return;
        DrawHandle(jar.previewHead, "Head (+Z is gaze)", 0.055f);
        DrawHandle(jar.previewLeftHand, "Left", jar.handRadius);
        DrawHandle(jar.previewRightHand, "Right", jar.handRadius);
    }
    private static void DrawHandle(Transform handle, string label, float radius)
    {
        if (!IsSceneInstance(handle)) return;
        Handles.Label(handle.position, label);
        Handles.DrawWireDisc(handle.position, Vector3.up, radius);
        Handles.DrawLine(handle.position, handle.position + handle.forward * 0.18f);
        EditorGUI.BeginChangeCheck();
        Vector3 position = Handles.PositionHandle(handle.position, handle.rotation);
        Quaternion rotation = Handles.RotationHandle(handle.rotation, handle.position);
        if (!EditorGUI.EndChangeCheck()) return;
        Undo.RecordObject(handle, "Move specimen preview handle");
        handle.SetPositionAndRotation(position, rotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(handle);
    }
}
