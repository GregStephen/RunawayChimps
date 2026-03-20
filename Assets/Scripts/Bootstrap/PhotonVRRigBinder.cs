using UnityEngine;
using Photon.VR;

public class PhotonVRRigBinder : MonoBehaviour
{
    [Header("Assign these from your Gorilla Rig")]
    public Transform Head;       // Main Camera transform
    public Transform LeftHand;   // LeftHand Controller transform (XR tracked)
    public Transform RightHand;  // RightHand Controller transform (XR tracked)

    private bool _bound;

    private void Awake()
    {
        // Try immediately (Bootstrap case)
        TryBind();
    }

    private void OnEnable()
    {
        // Try again when enabled (scene transitions)
        TryBind();
    }

    private void Update()
    {
        // Keep trying until the manager exists
        if (!_bound) TryBind();
    }

    private void TryBind()
    {
        var mgr = PhotonVRManager.Manager;
        if (mgr == null) return;

        if (Head == null || LeftHand == null || RightHand == null)
        {
            Debug.LogError("[PhotonVRRigBinder] Missing Head/LeftHand/RightHand references.");
            return;
        }

        mgr.Head = Head;
        mgr.LeftHand = LeftHand;
        mgr.RightHand = RightHand;

        _bound = true;
        Debug.Log("[PhotonVRRigBinder] Bound PhotonVRManager to local rig transforms.");
    }
}
