using System;
using System.Collections.Generic;
using System.Linq;
using RunawayChimps.Travel;
using RunawayChimps.Zones;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

public static class SectorTravelValidator
{
    private static readonly string[] Scenes = { "Bootstrap", "Loading", "Hub_Base", "Level1_Containment" };

    [MenuItem("Tools/Runaway Chimps/Validate Sector Travel")]
    public static void Validate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Run sector validation outside Play Mode.");
            return;
        }
        var errors = new List<string>();
        var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        foreach (var duplicate in enabled.GroupBy(path => path).Where(group => group.Count() > 1))
            errors.Add("Duplicate enabled scene: " + duplicate.Key);
        for (int i = 0; i < Scenes.Length; i++)
        {
            string path = "Assets/Scenes/" + Scenes[i] + ".unity";
            if (enabled.Length <= i || enabled[i] != path) errors.Add(path + " must be enabled at index " + i);
            // Preview scenes leave the user's scene setup and unsaved edits untouched.
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try { CheckScene(scene, errors); }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        if (errors.Count == 0)
            Debug.Log("Sector travel references passed. Still test floor clearance, all routes, multiplayer handover, voice, and fades on headsets.");
        else foreach (var error in errors) Debug.LogError("[Sector travel] " + error);
    }

    private static T[] Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

    private static void CheckScene(Scene scene, List<string> errors)
    {
        foreach (var transform in Find<Transform>(scene))
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                errors.Add(scene.name + "/" + transform.name + " has a missing script.");
        if (scene.name == "Bootstrap")
        {
            var services = Find<SectorTravelService>(scene);
            if (services.Length != 1 || services[0].fadeShader == null)
                errors.Add("Bootstrap requires one SectorTravelService with a referenced fade shader.");
            if (Find<LevelStreamService>(scene).Length != 1 || Find<GorillaLocomotion.Player>(scene).Length != 1)
                errors.Add("Bootstrap must contain exactly one loader and one local locomotion player.");
            return;
        }
        if (scene.name == "Loading")
        {
            if (Find<LoadingFlow>(scene).Length != 1 || Find<Canvas>(scene).Length == 0)
                errors.Add("Loading requires its flow and display canvas.");
            return;
        }

        bool hub = scene.name == "Hub_Base";
        var contexts = Find<SectorScene>(scene);
        if (contexts.Length != 1) { errors.Add(scene.name + " requires one SectorScene."); return; }
        var context = contexts[0];
        if (context.sector != (hub ? SectorId.Hub : SectorId.Containment) ||
            context.entryZone != (hub ? ZoneId.Hub : ZoneId.Level1_Antechamber))
            errors.Add(scene.name + " has the wrong sector or safe entry zone.");
        foreach (ArrivalRoute route in Enum.GetValues(typeof(ArrivalRoute)))
        {
            var spawn = context.GetArrival(route);
            if (spawn == null || spawn.gameObject.scene != scene)
                errors.Add(scene.name + " is missing its " + route + " arrival marker.");
        }
        if (hub && (context.doorArrivalSpawn == null || context.doorArrivalSpawn == context.arrivalSpawn))
            errors.Add("Hub must have distinct hallway and computer arrival markers.");
        if (Find<GorillaLocomotion.Player>(scene).Length != 0 || Find<XRInteractionManager>(scene).Length != 0)
            errors.Add(scene.name + " must use Bootstrap's persistent rig and interaction manager.");

        var doors = Find<SectorDoor>(scene);
        if (doors.Length != 1 || doors[0].destinationScene != (hub ? "Level1_Containment" : "Hub_Base"))
            errors.Add(scene.name + " requires its matching return/entrance SectorDoor.");
        foreach (var door in doors)
        {
            if (door.GetComponent<BoxCollider>() == null || !door.GetComponent<BoxCollider>().isTrigger ||
                !door.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger))
                errors.Add(door.name + " requires an interaction trigger and a solid barrier.");
        }
        if (hub)
        {
            var entrance = Find<PhysicalButton>(scene).FirstOrDefault(b => b.transform.root.name == "StartLevel1Button");
            if (entrance == null || entrance.OnPressed == null || entrance.OnPressed.GetPersistentEventCount() != 1 ||
                !(entrance.OnPressed.GetPersistentTarget(0) is SectorDoor) || entrance.OnPressed.GetPersistentMethodName(0) != "Travel")
                errors.Add("The Hub entrance button must call SectorDoor.Travel.");
        }
        else
        {
            var monsters = Find<SectorMonsterSync>(scene);
            if (monsters.Length != 1 || monsters[0].GetComponent<NavMeshAgent>() == null)
                errors.Add("Containment requires one synchronized monster with a NavMeshAgent.");
            var surfaces = Find<MonoBehaviour>(scene).Where(c => c != null && c.GetType().Name == "NavMeshSurface").ToArray();
            if (!surfaces.Any(c => new SerializedObject(c).FindProperty("m_NavMeshData")?.objectReferenceValue != null))
                errors.Add("Containment requires its baked NavMeshSurface data.");
        }
    }
}
