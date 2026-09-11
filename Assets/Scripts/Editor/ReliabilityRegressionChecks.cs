using System;
using System.Collections.Generic;
using System.Reflection;
using Photon.Pun;
using Photon.VR.Player;
using Photon.VR.Saving;
using PlayFab.EconomyModels;
using RunawayChimps.Travel;
using peepeecaca;
using UnityEditor;
using UnityEngine;

// Executable editor regression checks without moving gameplay into new assemblies.
// Run with the menu or -executeMethod ReliabilityRegressionChecks.Run in batch mode.
public static class ReliabilityRegressionChecks
{
    [MenuItem("Tools/Runaway Chimps/Run Reliability Regression Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PhotonNetwork.IsConnected)
            throw new InvalidOperationException("Run these checks outside Play Mode and disconnected from Photon.");
        CheckScriptBindings();
        CheckPlayerRigPrefab();
        CheckInventorySnapshot();
        CheckSavedCosmetics();
        CheckRoomFailureLifecycle();
        CheckIncompleteVentGraph();
        CheckFaceMaterialBounds();
        Require(SectorPresence.ElectController(null, SectorId.Containment) == 0, "No players means no monster controller.");
        Require(SectorPresence.ElectController(Array.Empty<Photon.Realtime.Player>(), SectorId.None) == 0, "Transition sector has no controller.");
        Debug.Log("Reliability regression checks passed. Still run sector validation, startup recovery, two-client and headset checks.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Regression failed: " + message);
    }

    private static void CheckScriptBindings()
    {
        string[] paths = {
            "KeyCard", "VRKeyCard", "Computer/ComputerTerminalUI", "Loading/LoadingDebugText",
            "PlayerScripts/AntiHandPhase", "MaterialScripts/RandomTileRegion", "MonsterScripts/MonsterTouchRespawnPhotonVR"
        };
        foreach (var path in paths)
        {
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/" + path + ".cs");
            Require(script != null && script.GetClass() != null, "Unity must resolve " + path);
        }
    }

    private static void CheckPlayerRigPrefab()
    {
        // Repair the known September 9 serialization regression first. The assertions below
        // ensure the exact historical target/pole/view wiring can actually be restored.
        PlayerRigPrefabRepair.RepairFromMenu();

        var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/PhotonVR/Player.prefab");
        Require(root != null, "Photon player prefab must load.");

        var player = root.GetComponent<PhotonVRPlayer>();
        var view = root.GetComponent<PhotonView>();
        Require(player != null && view != null, "Photon player prefab must contain PhotonVRPlayer and PhotonView.");
        Require(player.LeftHand != null && player.RightHand != null, "Photon player tracked hand transforms must be assigned.");

        FastIKFabric leftSolver = null;
        FastIKFabric rightSolver = null;
        XRHandL leftHand = null;
        XRHandController rightHand = null;

        foreach (var solver in root.GetComponentsInChildren<FastIKFabric>(true))
        {
            var left = solver.GetComponent<XRHandL>();
            if (left != null)
            {
                leftSolver = solver;
                leftHand = left;
                continue;
            }

            var right = solver.GetComponent<XRHandController>();
            if (right != null)
            {
                rightSolver = solver;
                rightHand = right;
            }
        }

        Require(leftSolver != null && rightSolver != null && leftHand != null && rightHand != null,
            "Photon player prefab must expose both visual hand IK solvers.");
        Require(leftSolver.Target == player.LeftHand, "Left arm IK target must be PhotonVRPlayer.LeftHand.");
        Require(rightSolver.Target == player.RightHand, "Right arm IK target must be PhotonVRPlayer.RightHand.");
        Require(leftSolver.Pole != null && rightSolver.Pole != null, "Both arm IK solvers must retain elbow poles.");
        Require(leftHand.view == view && rightHand.view == view, "Both hand animation scripts must reference the player PhotonView.");
        Require(rightHand.handType == HandType.Right, "Right visual hand must read the right XR controller.");
    }

    private static void CheckInventorySnapshot()
    {
        int previous = EconomyState.Coconuts;
        var owned = new HashSet<string>(EconomyState.OwnedItemIds);
        try
        {
            EconomyInventoryLoader.ApplySnapshot(new[] {
                new InventoryItem { Id = "currency", Amount = 5 },
                new InventoryItem { Id = "hat", Amount = 1 },
                new InventoryItem { Id = "currency", Amount = 7 },
                new InventoryItem { Id = "empty", Amount = 0 }, null
            }, "currency");
            Require(EconomyState.Coconuts == 12, "Sum all currency stacks.");
            Require(new HashSet<string>(EconomyState.OwnedItemIds).SetEquals(new[] { "hat" }), "Only positive item stacks grant local ownership.");
            EconomyInventoryLoader.ApplySnapshot(new[] {
                new InventoryItem { Id = "currency", Amount = int.MaxValue },
                new InventoryItem { Id = "currency", Amount = 1 }
            }, "currency");
            Require(EconomyState.Coconuts == int.MaxValue, "Currency display must not overflow negative.");
        }
        finally { EconomyState.Set(previous, owned); }
    }

    private static void CheckSavedCosmetics()
    {
        string key = "RC_TEST_" + Guid.NewGuid().ToString("N");
        try
        {
            PhotonVRValueSaver.SaveDictionary(key, new Dictionary<string, string> { ["Head"] = "Cap" });
            Require(PhotonVRValueSaver.GetDictionary(key)["Head"] == "Cap", "Cosmetics survive save/read.");
            PhotonVRValueSaver.SaveDictionary(key, new Dictionary<string, string>());
            Require(PhotonVRValueSaver.GetDictionary(key).Count == 0, "Empty cosmetics do not create a blank slot.");
            Require(!PlayerPrefs.HasKey(key + "Head"), "Removing a slot deletes its old saved value.");
        }
        finally { PlayerPrefs.DeleteKey(key); PlayerPrefs.DeleteKey(key + "Head"); PlayerPrefs.Save(); }
    }

    private static void CheckRoomFailureLifecycle()
    {
        var go = new GameObject("Room failure regression") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var service = go.AddComponent<RoomSwitchService>();
            typeof(RoomSwitchService).GetField("<IsSwitchingRooms>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(service, true);
            service.OnJoinRandomFailed(32760, "No matching room");
            Require(service.IsSwitchingRooms, "Public fallback creation keeps the request locked.");
            service.OnCreateRoomFailed(32766, "Room name exists");
            Require(!service.IsSwitchingRooms && !string.IsNullOrEmpty(service.LastError), "Creation failure unlocks and reports the request.");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void CheckIncompleteVentGraph()
    {
        var root = new GameObject("Vent graph regression") { hideFlags = HideFlags.HideAndDontSave };
        var foreign = new GameObject("Foreign vent node") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var a = new GameObject("A").AddComponent<VentNode>();
            var b = new GameObject("B").AddComponent<VentNode>();
            a.transform.SetParent(root.transform); b.transform.SetParent(root.transform);
            b.transform.position = Vector3.right * 10;
            a.neighbors.Add(null); a.neighbors.Add(foreign.AddComponent<VentNode>()); a.neighbors.Add(b);
            var graph = root.AddComponent<VentGraph>();
            var nodes = (List<VentNode>)typeof(VentGraph).GetField("nodes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(graph);
            nodes.Clear(); nodes.Add(a); nodes.Add(b);
            Require(Mathf.Approximately(graph.GetPathDistance(a.transform.position, b.transform.position), 10), "Ignore missing/external graph links while preserving valid paths.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(foreign); }
    }

    private static void CheckFaceMaterialBounds()
    {
        var go = new GameObject("Face material regression") { hideFlags = HideFlags.HideAndDontSave };
        Material material = null;
        try
        {
            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            Require(shader != null, "A test shader is available.");
            material = new Material(shader);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { material, material };
            go.AddComponent<PhotonView>();
            var face = go.AddComponent<FaceExpressionController>();
            face.faceRenderer = renderer;
            typeof(FaceExpressionController).GetMethod("SetFace", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(face, new object[] { material });
            Require(renderer.sharedMaterials.Length == 2, "A two-slot renderer must not access slot 2.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
        }
    }
}
