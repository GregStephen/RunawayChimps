using UnityEngine;

public class KeyboardKey : MonoBehaviour
{
    public enum Key
    {
        Up, Down, Left, Right, Enter, Backspace,
        A, B, C, D, E, F, G, H, I, J, K, L, M,
        N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
        Num0, Num1, Num2, Num3, Num4, Num5, Num6, Num7, Num8, Num9,
        Space, Dash, Underscore
    }

    [Header("Key Identity")]
    public Key keyValue;

    private KeyboardController keyboard;

    private void Awake()
    {
        keyboard = GetComponentInParent<KeyboardController>();
        if (keyboard == null)
            Debug.LogWarning($"[KeyboardKey] '{name}' has no KeyboardController in parents.");
    }

    // Hook this up to PhysicalButton.OnPressed in the inspector
    public void OnKeyPressed()
    {
        keyboard?.Press(keyValue);
    }
}
