using UnityEngine;

public class SurfaceAudio : MonoBehaviour
{
    public SurfaceAudioProfile profile;

    // Optional: if the collider is on a child but you want the root profile
    public SurfaceAudioProfile GetProfile() => profile;
}
