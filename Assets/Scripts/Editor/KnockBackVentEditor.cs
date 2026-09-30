#if UNITY_EDITOR
using System;
using Photon.Pun;
using RunawayChimps.Toys.KnockBack;
using RunawayChimps.Travel;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(KnockBackVent))]
public sealed class KnockBackVentEditor : Editor
{
    public const string PrefabPath = "Assets/RunawayChimps/KnockBackVent/KnockBackVent.prefab";
    private const string HubPath = "Assets/Scenes/Hub_Base.unity";
    private const string MenuRoot = "Tools/Runaway Chimps/Toys/";

    [MenuItem(MenuRoot + "Place Knock-Back Vent in Hub")]
    public static void PlaceInHub()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("Exit Play Mode before placing the Knock-Back Vent."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("Missing Knock-Back Vent prefab: " + PrefabPath); return; }
        Scene scene = SceneManager.GetSceneByPath(HubPath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(HubPath, OpenSceneMode.Additive);
        Transform parent = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var existing = root.GetComponentInChildren<KnockBackVent>(true);
            if (existing != null)
            {
                Select(existing.gameObject);
                Debug.Log("Selected the existing Hub Knock-Back Vent; placement and overrides were preserved.", existing);
                return;
            }
            if (root.name == "SpawnRoom") parent = root.transform;
        }
        var context = SectorScene.Find(scene);
        if (context == null || context.arrivalSpawn == null)
        { Debug.LogError("Hub needs its existing SectorScene and arrival spawn. No toy was placed."); return; }

        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Place Knock-Back Vent in Hub");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Place Knock-Back Vent in Hub");
        if (parent != null) Undo.SetTransformParent(instance.transform, parent, "Parent Knock-Back Vent");
        Undo.RecordObject(instance.transform, "Position Knock-Back Vent");
        Transform anchor = context.arrivalSpawn;
        Vector3 origin = anchor.position + Vector3.up * 1.1f + anchor.right * 1.8f;
        Vector3 direction = Vector3.ProjectOnPlane(anchor.forward, Vector3.up).normalized;
        Vector3 position = origin + direction * 1.5f;
        Vector3 normal = -direction;
        float nearest = float.PositiveInfinity;
        Physics.SyncTransforms();
        foreach (var hit in Physics.RaycastAll(origin, direction, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.gameObject.scene != scene || hit.distance >= nearest || Mathf.Abs(hit.normal.y) > 0.2f ||
                Vector3.Dot(hit.normal, -direction) < 0.8f) continue;
            nearest = hit.distance;
            position = hit.point + hit.normal * 0.065f;
            normal = hit.normal;
        }
        instance.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal, Vector3.up));
        PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        Undo.CollapseUndoOperations(undo);
        EditorSceneManager.MarkSceneDirty(scene);
        Select(instance);
        Debug.Log("Placed one editable Knock-Back Vent beside the Hub terminal area. Inspect clearance, then save the Hub yourself. Undo is supported." +
            (float.IsPositiveInfinity(nearest) ? " No wall was found; the fallback test placement needs manual alignment." : ""), instance);
    }

    [MenuItem(MenuRoot + "Play Selected Knock-Back Vent Sample (Offline Play Mode)")]
    public static void PlaySelectedSample()
    {
        var vent = SelectedVent();
        if (vent == null) { Debug.LogWarning("Select a scene instance of KnockBackVent first."); return; }
        vent.PlayEditorSample();
    }

    [MenuItem(MenuRoot + "Cancel Selected Knock-Back Vent Sample")]
    public static void CancelSelectedSample() { SelectedVent()?.CancelEditorSample(); }

    private static KnockBackVent SelectedVent() => Selection.activeGameObject != null
        ? Selection.activeGameObject.GetComponentInParent<KnockBackVent>() : null;

    private static void Select(GameObject value)
    {
        Selection.activeGameObject = value;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var vent = (KnockBackVent)target;
        EditorGUILayout.HelpBox("Local +Z faces the player; reply sources sit behind at -Z. Keep positive uniform root scale. " +
            "Only the first tapper owns a recording. Duplicate IDs in a sector disable both copies. " +
            "The sample requires an active scene vent, offline Play Mode and an existing AudioListener (Scene view audio works without a headset).", MessageType.Info);
        if (Application.isPlaying)
        {
            EditorGUILayout.LabelField("State / authority / tapper", vent.State + " / " + vent.Controller + " / " + vent.Tapper);
            using (new EditorGUI.DisabledScope(PhotonNetwork.InRoom))
            {
                if (GUILayout.Button("Play sample: tap, tap ... tap")) vent.PlayEditorSample();
                if (GUILayout.Button("Cancel sample")) vent.CancelEditorSample();
            }
        }
        else if (GUILayout.Button("Assign unique ID to this copy (Undo supported)"))
        {
            Undo.RecordObject(vent, "Assign Knock-Back Vent identity");
            vent.interactionId = "knock-" + Guid.NewGuid().ToString("N");
            if (PrefabUtility.IsPartOfPrefabInstance(vent))
                PrefabUtility.RecordPrefabInstancePropertyModifications(vent);
            EditorUtility.SetDirty(vent);
        }
    }
}
#endif
