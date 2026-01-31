using UnityEngine;

public class KeyboardController : MonoBehaviour
{
    public ComputerTerminalUI terminal;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip keyClick;
    [Range(0f, 1f)] public float clickVolume = 0.6f;
    public float clickPitchMin = 0.95f;
    public float clickPitchMax = 1.05f;

    public void Press(KeyboardKey.Key key)
    {
        // Sound
        if (audioSource != null && keyClick != null)
        {
            audioSource.pitch = Random.Range(clickPitchMin, clickPitchMax);
            audioSource.PlayOneShot(keyClick, clickVolume);
        }

        // Forward to terminal
        if (terminal != null)
            terminal.OnKeyPressed(key);
    }
}
