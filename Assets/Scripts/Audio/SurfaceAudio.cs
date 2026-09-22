using UnityEngine;

[DisallowMultipleComponent]
public class SurfaceAudio : MonoBehaviour
{
    [Tooltip("Applies to this collider and child colliders. A configured child SurfaceAudio overrides its parent.")]
    public SurfaceAudioProfile profile;

    // Optional: if the collider is on a child but you want the root profile
    public SurfaceAudioProfile GetProfile() => profile;

    /// <summary>
    /// Shared by hands and future prop impacts. Empty overrides inherit the next
    /// usable parent profile, then the caller's fallback. Materials need no code enum.
    /// </summary>
    public static SurfaceAudioProfile ResolveProfile(Collider surface, SurfaceAudioProfile fallback)
    {
        for (Transform current = surface != null ? surface.transform : null;
             current != null; current = current.parent)
        {
            SurfaceAudio authored = current.GetComponent<SurfaceAudio>();
            if (authored != null && authored.isActiveAndEnabled &&
                authored.profile != null && authored.profile.HasPlayableClips)
            {
                return authored.profile;
            }
        }

        return fallback != null && fallback.HasPlayableClips ? fallback : null;
    }
}
