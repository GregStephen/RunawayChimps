using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

public class SmoothTurnController : MonoBehaviour
{
    [Header("Settings")] public float turnSpeed = 60f;
    [Header("References")] public Transform gorillaPlayerBody;
    [Header("Input")] public InputActionProperty turnAction;
    private bool enabledAction;

    private void OnEnable()
    {
        var action = turnAction.action;
        enabledAction = action != null && !action.enabled;
        if (enabledAction) action.Enable();
    }

    private void OnDisable()
    {
        if (enabledAction) turnAction.action?.Disable();
        enabledAction = false;
    }

    private void Update()
    {
        if (gorillaPlayerBody == null) return;
        var action = turnAction.action;
        Vector2 input = Vector2.zero;
        if (action != null && action.bindings.Count > 0) input = action.ReadValue<Vector2>();
        else InputDevices.GetDeviceAtXRNode(XRNode.RightHand)
            .TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out input);
        if (Mathf.Abs(input.x) <= 0.1f) return;
        float amount = input.x * turnSpeed * Time.deltaTime;
        var player = gorillaPlayerBody.GetComponent<GorillaLocomotion.Player>();
        // Rotate around the tracked head and rotate locomotion velocity history with it.
        if (player != null) player.Turn(amount);
        else gorillaPlayerBody.Rotate(0f, amount, 0f, Space.World);
    }
}
