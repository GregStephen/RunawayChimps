using UnityEngine;
using Photon.VR;

public class PhotonVRRigBinder : MonoBehaviour
{
    [Header("Assign these from your Gorilla Rig")]
    public Transform Head;       // Main Camera transform
    public Transform LeftHand;   // LeftHand Controller transform (XR tracked)
    public Transform RightHand;  // RightHand Controller transform (XR tracked)

    private PhotonVRManager boundManager;
    private bool warnedMissingReferences;

    private void Awake()
    {
        TryBind();
    }

    private void OnEnable()
    {
        TryBind();
    }

    private void Update()
    {
        var manager = PhotonVRManager.Manager;
        if (manager == null)
        {
            boundManager = null;
            return;
        }

        // Rebind not only at startup, but also if the persistent manager was replaced or
        // any tracking reference was cleared/overwritten during reconnect or scene work.
        if (boundManager != manager ||
            manager.Head != Head ||
            manager.LeftHand != LeftHand ||
            manager.RightHand != RightHand)
        {
            TryBind();
        }
    }

    private void TryBind()
    {
        var manager = PhotonVRManager.Manager;
        if (manager == null) return;

        if (Head == null || LeftHand == null || RightHand == null)
        {
            if (!warnedMissingReferences)
            {
                warnedMissingReferences = true;
                Debug.LogError("[PhotonVRRigBinder] Missing Head/LeftHand/RightHand references.", this);
            }
            return;
        }

        manager.Head = Head;
        manager.LeftHand = LeftHand;
        manager.RightHand = RightHand;

        boundManager = manager;
        warnedMissingReferences = false;
        Debug.Log("[PhotonVRRigBinder] Bound PhotonVRManager to local rig transforms.", this);
    }
}
