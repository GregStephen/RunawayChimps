using UnityEngine;

public class KeyboardPressVisual : MonoBehaviour
{
    public Transform pressCap;

    public float returnSpeed = 18f;

    Vector3 _targetWorldPos;
    Quaternion _targetWorldRot;

    void Awake()
    {
        Debug.Log($"[KeyboardPressVisual] pressCap={(pressCap ? pressCap.name : "NULL")}");

        if (pressCap != null)
        {
            _targetWorldPos = pressCap.position;
            _targetWorldRot = pressCap.rotation;
        }
    }

    void Update()
    {
        if (pressCap == null) return;

        pressCap.position = Vector3.Lerp(pressCap.position, _targetWorldPos, Time.deltaTime * returnSpeed);
        pressCap.rotation = Quaternion.Slerp(pressCap.rotation, _targetWorldRot, Time.deltaTime * returnSpeed);
    }

    public void SnapTo(Transform anchor)
    {
        if (pressCap == null || anchor == null) return;

        _targetWorldPos = anchor.position;
        _targetWorldRot = anchor.rotation;

        pressCap.position = _targetWorldPos;
        pressCap.rotation = _targetWorldRot;
    }

    public void PressAt(Transform anchor, Vector3 localPressDir, float depth)
    {
        if (pressCap == null || anchor == null) return;

        // Move cap to key top, then push along anchor's local direction in world space
        _targetWorldRot = anchor.rotation;

        Vector3 worldDir = anchor.TransformDirection(localPressDir.normalized);
        pressCap.position = anchor.position + worldDir * depth;
        pressCap.rotation = _targetWorldRot;

        // Return target should be the anchor top
        _targetWorldPos = anchor.position;
    }
}
