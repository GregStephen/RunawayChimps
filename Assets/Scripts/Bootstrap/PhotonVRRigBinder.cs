using UnityEngine;
using Photon.VR;

public class PhotonVRRigBinder : MonoBehaviour
{
    [Header("Optional: assign these if you want (recommended).")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    private void Start()
    {
        Bind();
    }

    private void Bind()
    {
        if (PhotonVRManager.Manager == null)
        {
            Debug.LogWarning("[PhotonVRRigBinder] PhotonVRManager.Manager is null.");
            return;
        }

        // If not assigned in inspector, try to find common XR transforms.
        if (head == null)
        {
            var cam = Camera.main;
            if (cam != null) head = cam.transform;
        }

        // Try to auto-find hands if not assigned (best effort).
        if (leftHand == null || rightHand == null)
        {
            // Common names in XR rigs (we'll catch your Gorilla Rig too)
            var all = FindObjectsOfType<Transform>(true);

            foreach (var t in all)
            {
                var n = t.name.ToLowerInvariant();

                if (leftHand == null && (n.Contains("left") && (n.Contains("hand") || n.Contains("controller"))))
                    leftHand = t;

                if (rightHand == null && (n.Contains("right") && (n.Contains("hand") || n.Contains("controller"))))
                    rightHand = t;
            }
        }

        PhotonVRManager.Manager.Head = head;
        PhotonVRManager.Manager.LeftHand = leftHand;
        PhotonVRManager.Manager.RightHand = rightHand;

        Debug.Log($"[PhotonVRRigBinder] Bound rig. Head={head?.name}, Left={leftHand?.name}, Right={rightHand?.name}");
    }
}
