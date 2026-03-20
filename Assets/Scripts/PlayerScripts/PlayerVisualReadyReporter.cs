using System.Collections;
using UnityEngine;

public class PlayerVisualReadyReporter : MonoBehaviour
{
    [Tooltip("How many frames the renderers must remain unchanged before we signal ready.")]
    public int stableFramesRequired = 20;

    [Tooltip("Extra delay after stability (helps shader upload settle).")]
    public float extraSecondsAfterStable = 0.1f;

    private Renderer[] _renderers;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Start()
    {
        StartCoroutine(CoWaitForVisualsToSettle());
    }

    private IEnumerator CoWaitForVisualsToSettle()
    {
        // Give PhotonVR a moment to run its own Start/Awake and apply initial values
        yield return null;
        yield return new WaitForEndOfFrame();

        if (_renderers == null || _renderers.Length == 0)
        {
            AppState.I?.MarkPlayerVisualsReady();
            AppState.I?.TryMarkReady();
            yield break;
        }

        int stable = 0;
        int lastHash = ComputeMaterialsHash();

        while (stable < stableFramesRequired)
        {
            yield return null;

            int hash = ComputeMaterialsHash();
            if (hash == lastHash)
            {
                stable++;
            }
            else
            {
                stable = 0;
                lastHash = hash;
            }
        }

        if (extraSecondsAfterStable > 0f)
            yield return new WaitForSeconds(extraSecondsAfterStable);

        Debug.Log("[PlayerVisualReadyReporter] Visuals stable. Marking ready.");
        AppState.I?.MarkPlayerVisualsReady();
        AppState.I?.TryMarkReady();
    }

    private int ComputeMaterialsHash()
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null) continue;

                // sharedMaterials avoids forcing instancing; we just want to know when swaps happen
                var mats = r.sharedMaterials;
                if (mats == null) { h = h * 31 + 1; continue; }

                h = h * 31 + mats.Length;

                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    h = h * 31 + (mat != null ? mat.GetInstanceID() : 0);

                    // If a texture gets assigned later without swapping material, include it too
                    if (mat != null && mat.mainTexture != null)
                        h = h * 31 + mat.mainTexture.GetInstanceID();
                }
            }
            return h;
        }
    }
}
