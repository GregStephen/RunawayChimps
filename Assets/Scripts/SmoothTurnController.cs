using UnityEngine;
using UnityEngine.InputSystem;

public class SmoothTurnController : MonoBehaviour
{
    [Header("Settings")]
    public float turnSpeed = 60f;

    [Header("References")]
    public Transform gorillaPlayerBody;

    [Header("Input")]
    public InputActionProperty turnAction; // Vector2 from right thumbstick

    private void OnEnable()
    {
        turnAction.action.Enable();
    }

    private void OnDisable()
    {
        turnAction.action.Disable();
    }

    private void Update()
    {
        if (gorillaPlayerBody == null)
            return;

        Vector2 input = turnAction.action.ReadValue<Vector2>();
        float x = input.x;

        if (Mathf.Abs(x) > 0.1f)
        {
            float amount = x * turnSpeed * Time.deltaTime;
            gorillaPlayerBody.Rotate(0f, amount, 0f, Space.World);
        }
    }
}
