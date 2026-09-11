using System;
using Photon.Pun;
using Photon.VR.Player;
using peepeecaca;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlayerRigPrefabRepair
{
    private const string PlayerPrefabPath = "Assets/Resources/PhotonVR/Player.prefab";

    // These are the known-good elbow-pole transforms from the player model wiring that
    // existed before the September 9 prefab edit cleared the serialized references.
    private static readonly long[] LeftPoleIds = { 2768699113985102344L, -8727140884738760174L };
    private static readonly long[] RightPoleIds = { 4726460642201202213L, 6996711868271861311L };

    static PlayerRigPrefabRepair()
    {
        EditorApplication.delayCall += RepairAfterDomainLoad;
    }

    [MenuItem("Tools/Runaway Chimps/Repair Player Rig Prefab")]
    public static void RepairFromMenu()
    {
        Repair(logWhenAlreadyHealthy: true);
    }

    private static void RepairAfterDomainLoad()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        Repair(logWhenAlreadyHealthy: false);
    }

    private static void Repair(bool logWhenAlreadyHealthy)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (root == null)
        {
            Debug.LogWarning($"[PlayerRigPrefabRepair] Could not load {PlayerPrefabPath}.");
            return;
        }

        var player = root.GetComponent<PhotonVRPlayer>();
        var view = root.GetComponent<PhotonView>();
        if (player == null || view == null || player.LeftHand == null || player.RightHand == null)
        {
            Debug.LogError("[PlayerRigPrefabRepair] Player prefab is missing PhotonVRPlayer, PhotonView, LeftHand, or RightHand wiring.", root);
            return;
        }

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

        if (leftSolver == null || rightSolver == null || leftHand == null || rightHand == null)
        {
            Debug.LogError("[PlayerRigPrefabRepair] Could not identify both visual hand IK solvers on the Photon player prefab.", root);
            return;
        }

        var leftPole = FindTransformByLocalId(root, LeftPoleIds);
        var rightPole = FindTransformByLocalId(root, RightPoleIds);

        bool changed = false;
        changed |= Assign(ref leftSolver.Target, player.LeftHand);
        changed |= Assign(ref rightSolver.Target, player.RightHand);
        changed |= Assign(ref leftHand.view, view);
        changed |= Assign(ref rightHand.view, view);

        if (leftPole != null)
            changed |= Assign(ref leftSolver.Pole, leftPole);
        else if (leftSolver.Pole == null)
            Debug.LogWarning("[PlayerRigPrefabRepair] Left elbow pole could not be resolved. Hand tracking will still bind, but elbow direction needs inspection.", leftSolver);

        if (rightPole != null)
            changed |= Assign(ref rightSolver.Pole, rightPole);
        else if (rightSolver.Pole == null)
            Debug.LogWarning("[PlayerRigPrefabRepair] Right elbow pole could not be resolved. Hand tracking will still bind, but elbow direction needs inspection.", rightSolver);

        if (rightHand.handType != HandType.Right)
        {
            rightHand.handType = HandType.Right;
            changed = true;
        }

        if (!changed)
        {
            if (logWhenAlreadyHealthy)
                Debug.Log("[PlayerRigPrefabRepair] Photon player hand IK wiring is already healthy.", root);
            return;
        }

        EditorUtility.SetDirty(leftSolver);
        EditorUtility.SetDirty(rightSolver);
        EditorUtility.SetDirty(leftHand);
        EditorUtility.SetDirty(rightHand);
        PrefabUtility.SavePrefabAsset(root);
        AssetDatabase.SaveAssets();

        Debug.Log("[PlayerRigPrefabRepair] Restored Photon player left/right IK targets, elbow poles, PhotonView references, and right-hand side configuration.", root);
    }

    private static Transform FindTransformByLocalId(GameObject root, long[] expectedIds)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(transform, out string _, out long localId))
                continue;

            if (Array.IndexOf(expectedIds, localId) >= 0)
                return transform;
        }

        return null;
    }

    private static bool Assign<T>(ref T field, T value) where T : UnityEngine.Object
    {
        if (field == value)
            return false;

        field = value;
        return true;
    }
}
